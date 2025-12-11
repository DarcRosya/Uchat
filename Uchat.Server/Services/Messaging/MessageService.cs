using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.MongoDB;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.Services.Redis;
using Uchat.Server.Services.Unread;
using Uchat.Shared.DTOs;
using SQLitePCL;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Uchat.Server.Hubs;

namespace Uchat.Server.Services.Messaging;

public sealed class MessageService : IMessageService
{
    private readonly UchatDbContext _context;
    private readonly MongoDbContext _mongoDbContext;
    private readonly IMessageRepository _messageRepository;
    private readonly IRedisService _redisService;
    private readonly ILogger<MessageService> _logger;
    private readonly IMongoCollection<MongoMessage> _messages;
    private readonly IUnreadCounterService _unreadCounterService;
    private readonly IHubContext<ChatHub> _hubContext;

    private const int MaxMessageLength = 1500;
    private static readonly JsonSerializerOptions CachedMessageSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly TimeSpan LastMessageCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SortedSetEntryTtl = TimeSpan.FromHours(24);

    public MessageService(
        UchatDbContext context,
        MongoDbContext mongoDbContext,
        IMessageRepository messageRepository,
        IRedisService redisService,
        IUnreadCounterService unreadCounterService,
        IHubContext<ChatHub> hubContext,
        ILogger<MessageService> logger)
    {
        _context = context;
        _mongoDbContext = mongoDbContext;
        _messageRepository = messageRepository;
        _redisService = redisService;
        _unreadCounterService = unreadCounterService;
        _hubContext = hubContext;
        _logger = logger;
        _messages = mongoDbContext.Messages;
    }

    public async Task<MessagingResult> SendMessageAsync(MessageCreateDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        var validationError = ValidateMessage(dto);
        if (validationError != null)
        {
            return MessagingResult.Failure(validationError);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        MongoMessage? mongoMessage = null;
        string? messageId = null;
        int? resurrectedUserId = null;

        try
        {
            var chatRoom = await _context.ChatRooms
                .IgnoreQueryFilters()
                .Include(cr => cr.Members)
                .FirstOrDefaultAsync(cr => cr.Id == dto.ChatRoomId, cancellationToken);

            if (chatRoom == null)
            {
                return MessagingResult.Failure($"ChatRoom {dto.ChatRoomId} not found.");
            }

            var senderMember = chatRoom.Members.FirstOrDefault(m => m.UserId == dto.SenderId);
            if (senderMember == null)
            {
                return MessagingResult.Failure("Sender is not a member of the chat.");
            }

            if (chatRoom.Type == ChatRoomType.DirectMessage)
            {
                var otherMember = chatRoom.Members.FirstOrDefault(m => m.UserId != dto.SenderId);
                
                if (otherMember != null && otherMember.IsDeleted)
                {
                    _logger.LogInformation("Resurrecting user {UserId} in chat {ChatId}", otherMember.UserId, dto.ChatRoomId);
                    
                    otherMember.IsDeleted = false;
                    otherMember.ClearedHistoryAt = DateTime.UtcNow.AddSeconds(-5); 
                    
                    resurrectedUserId = otherMember.UserId; 
                }
            }

            var sender = await _context.Users.FindAsync(new object[] { dto.SenderId }, cancellationToken: cancellationToken);
            if (sender == null)
            {
                return MessagingResult.Failure($"Sender {dto.SenderId} not found.");
            }

            mongoMessage = BuildMongoMessage(dto, sender);

            if (!string.IsNullOrEmpty(dto.ReplyToMessageId))
            {
                var originalMessage = await _mongoDbContext.Messages
                    .Find(m => m.Id == dto.ReplyToMessageId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (originalMessage != null)
                {
                    // Save the name of the person we are responding to
                    mongoMessage.ReplyToSenderName = originalMessage.Sender.DisplayName ?? originalMessage.Sender.Username;
                    
                    // It is also useful to explicitly save the text (if BuildMongoMessage did not do so).
                    mongoMessage.ReplyToContent = originalMessage.Content;
                }
            }
            
            if (string.IsNullOrEmpty(mongoMessage.Id))
            {
                mongoMessage.Id = ObjectId.GenerateNewId().ToString();
            }
            mongoMessage.SentAt = DateTime.UtcNow;
            
            var messages = _mongoDbContext.Messages;
            await messages.InsertOneAsync(mongoMessage, null, cancellationToken);
            messageId = mongoMessage.Id;

            chatRoom.LastActivityAt = mongoMessage.SentAt;

            var previewContent = dto.Content;
            if (previewContent != null && previewContent.Length > 100) 
            {
                previewContent = previewContent.Substring(0, 30) + "...";
            }

            chatRoom.LastMessageContent = previewContent; 
            chatRoom.LastMessageAt = mongoMessage.SentAt;

            await UpdateContactStatsAsync(chatRoom.Members.Select(m => m.UserId), dto.SenderId, mongoMessage.SentAt, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            await CacheLastMessageAsync(chatRoom.Id, mongoMessage);
            await UpdateChatSortedSetAsync(chatRoom.Id, mongoMessage.SentAt);
            await _unreadCounterService.IncrementUnreadAsync(
                chatRoom.Id,
                chatRoom.Members.Select(m => m.UserId),
                dto.SenderId);

            return MessagingResult.SuccessResult(
                messageId, 
                mongoMessage.SentAt, 
                resurrectedUserId: resurrectedUserId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MessageService failed for chat {ChatId}", dto.ChatRoomId);
            await transaction.RollbackAsync(cancellationToken);

            if (!string.IsNullOrEmpty(messageId))
            {
                await TryDeleteMessageAsync(messageId);
            }

            return MessagingResult.Failure(ex.Message, needsReconciliation: !string.IsNullOrEmpty(messageId));
        }
    }

    public async Task<MessageDto?> GetMessageByIdAsync(string messageId)
    {
        var mongoMessage = await _messageRepository.GetMessageByIdAsync(messageId);
        if (mongoMessage == null)
            return null;

        MessageReplyDto? replyToDto = null;
        if (!string.IsNullOrEmpty(mongoMessage.ReplyToMessageId))
        {
            var replyToMessage = await _messageRepository.GetMessageByIdAsync(mongoMessage.ReplyToMessageId);
            if (replyToMessage != null)
            {
                replyToDto = new MessageReplyDto
                {
                    Id = replyToMessage.Id,
                    Content = replyToMessage.Content,
                    SenderName = replyToMessage.Sender.Username
                };
            }
        }

        return new MessageDto
        {
            Id = mongoMessage.Id,
            ChatRoomId = mongoMessage.ChatId,
            Sender = new MessageSenderDto
            {
                UserId = mongoMessage.Sender.UserId,
                Username = mongoMessage.Sender.Username,
                DisplayName = mongoMessage.Sender.DisplayName,
                AvatarUrl = mongoMessage.Sender.AvatarUrl
            },
            Content = mongoMessage.Content,
            Type = mongoMessage.Type,
            ReplyToMessageId = mongoMessage.ReplyToMessageId,
            SentAt = mongoMessage.SentAt,
            EditedAt = mongoMessage.EditedAt,
            IsDeleted = mongoMessage.IsDeleted,
            ReplyTo = replyToDto
        };
    }

    public async Task<Dictionary<int, MessageDto>> GetLastMessagesForChatsBatch(Dictionary<int, DateTime?> chatsWithClearDates)
    {
        if (chatsWithClearDates == null || !chatsWithClearDates.Any())
                return new Dictionary<int, MessageDto>();
        
        var result = new Dictionary<int, MessageDto>();

        var cachedData = await TryGetCachedLastMessagesAsync(chatsWithClearDates.Keys);

        foreach (var kvp in cachedData)
        {
            int chatId = kvp.Key;
            MessageDto msg = kvp.Value;

            if (chatsWithClearDates.TryGetValue(chatId, out DateTime? clearedAt))
            {
                if (clearedAt == null || msg.SentAt > clearedAt.Value)
                {
                    result[chatId] = msg;
                }
            }
        }

        var missingChats = chatsWithClearDates
            .Where(c => !result.ContainsKey(c.Key))
            .ToDictionary(c => c.Key, c => c.Value);


        if (missingChats.Any())
        {
            var mongoResults = await FetchLastMessagesFromMongoWithDatesAsync(missingChats);
            foreach (var kvp in mongoResults)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    public async Task<PaginatedMessagesDto> GetMessagesAsync(int chatId, int userId, int limit = 50, DateTime? before = null)
    {
        var member = await _context.ChatRoomMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => 
            m.ChatRoomId == chatId && 
            m.UserId == userId && 
            !m.IsDeleted &&         
            !m.IsPending,         
            cancellationToken: CancellationToken.None 
        );

        if (member == null) return new PaginatedMessagesDto();

        DateTime? minDate = member.ClearedHistoryAt;

        var mongoMessages = await _messageRepository.GetChatMessagesAsync(chatId, minDate, limit + 1, before);
        
        bool hasMore = mongoMessages.Count > limit;
        if (hasMore)
        {
            mongoMessages.RemoveAt(mongoMessages.Count - 1);
        }

        var replyToIds = mongoMessages
            .Where(m => !string.IsNullOrEmpty(m.ReplyToMessageId))
            .Select(m => m.ReplyToMessageId!)
            .Distinct()
            .ToList();

        var replyToMessages = new Dictionary<string, MessageReplyDto>();
        if (replyToIds.Any())
        {
            var replyMongoMessages = await _messageRepository.GetMessagesByIdsAsync(replyToIds);
            foreach (var replyMsg in replyMongoMessages)
            {
                replyToMessages[replyMsg.Id] = new MessageReplyDto
                {
                    Id = replyMsg.Id,
                    Content = replyMsg.Content,
                    SenderName = replyMsg.Sender.Username   
                };
            }
        }

        var messageDtos = mongoMessages.Select(m => new MessageDto
        {
            Id = m.Id,
            ChatRoomId = m.ChatId,
            Sender = new MessageSenderDto
            {
                UserId = m.Sender.UserId,
                Username = m.Sender.Username,
                DisplayName = m.Sender.DisplayName,
                AvatarUrl = m.Sender.AvatarUrl
            },
            Content = m.Content,
            Type = m.Type,
            ReplyToMessageId = m.ReplyToMessageId,
            SentAt = m.SentAt,
            EditedAt = m.EditedAt,
            IsDeleted = m.IsDeleted,
            ReplyTo = !string.IsNullOrEmpty(m.ReplyToMessageId) && replyToMessages.ContainsKey(m.ReplyToMessageId)
                ? replyToMessages[m.ReplyToMessageId]
                : null
        }).ToList();

        DateTime? nextCursor = hasMore && messageDtos.Any()
            ? messageDtos.Last().SentAt
            : null;

        return new PaginatedMessagesDto
        {
            Messages = messageDtos,
            Pagination = new PaginationInfo
            {
                NextCursor = nextCursor,
                HasMore = hasMore,
                Count = messageDtos.Count
            }
        };
    }

    private async Task CacheLastMessageAsync(int chatId, MongoMessage message)
    {
        if (!_redisService.IsAvailable || message == null)
        {
            return;
        }

        try
        {
            var payload = BuildCachedLastMessage(message);
            var serialized = JsonSerializer.Serialize(payload, CachedMessageSerializerOptions);
            await _redisService.SetHashAsync(RedisCacheKeys.ChatLastMessagesKey, chatId.ToString(), serialized, LastMessageCacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to cache last message for chat {ChatId}", chatId);
        }
    }

    private async Task UpdateChatSortedSetAsync(int chatId, DateTime lastActivity)
    {
        if (!_redisService.IsAvailable)
        {
            return;
        }

        var memberIds = await _context.ChatRoomMembers
            .Where(m => m.ChatRoomId == chatId)
            .Select(m => m.UserId)
            .ToListAsync();

        if (!memberIds.Any())
        {
            return;
        }

        var score = new DateTimeOffset(lastActivity.ToUniversalTime()).ToUnixTimeSeconds();

        var tasks = memberIds.Select(userId => _redisService.UpdateSortedSetAsync(
            RedisCacheKeys.GetUserChatSortedSetKey(userId),
            chatId.ToString(),
            score,
            SortedSetEntryTtl));

        await Task.WhenAll(tasks);
    }

    private async Task RemoveChatFromSortedSetsAsync(int chatId)
    {
        if (!_redisService.IsAvailable)
        {
            return;
        }

        var memberIds = await _context.ChatRoomMembers
            .Where(m => m.ChatRoomId == chatId)
            .Select(m => m.UserId)
            .ToListAsync();

        if (!memberIds.Any())
        {
            return;
        }

        var tasks = memberIds.Select(userId => _redisService.RemoveSortedSetMemberAsync(
            RedisCacheKeys.GetUserChatSortedSetKey(userId),
            chatId.ToString()));

        await Task.WhenAll(tasks);
    }

    private async Task<Dictionary<int, MessageDto>> TryGetCachedLastMessagesAsync(IEnumerable<int> chatIds)
    {
        var result = new Dictionary<int, MessageDto>();
        if (!_redisService.IsAvailable)
        {
            return result;
        }

        foreach (var chatId in chatIds.Distinct())
        {
            var cachedValue = await _redisService.GetHashAsync(RedisCacheKeys.ChatLastMessagesKey, chatId.ToString());
            if (string.IsNullOrEmpty(cachedValue))
            {
                continue;
            }

            var dto = DeserializeCachedLastMessage(cachedValue);
            if (dto != null)
            {
                result[chatId] = dto;
            }
        }

        return result;
    }

    private async Task<Dictionary<int, MessageDto>> FetchLastMessagesFromMongoAsync(List<int> chatIds)
    {
        var filter = Builders<MongoMessage>.Filter.And(
            Builders<MongoMessage>.Filter.In(m => m.ChatId, chatIds),
            Builders<MongoMessage>.Filter.Eq(m => m.IsDeleted, false)
        );

        var aggregation = _messages.Aggregate()
            .Match(filter)
            .SortByDescending(m => m.SentAt)
            .Group(m => m.ChatId, g => new
            {
                ChatId = g.Key,
                LastMessage = g.First()
            });

        var results = await aggregation.ToListAsync();
        var dict = new Dictionary<int, MessageDto>();

        foreach (var item in results)
        {
            var msg = item.LastMessage;
            var dto = new MessageDto
            {
                Id = msg.Id,
                ChatRoomId = msg.ChatId,
                Content = msg.Content,
                Type = msg.Type,
                SentAt = msg.SentAt,
                EditedAt = msg.EditedAt,
                IsDeleted = msg.IsDeleted,
                ReplyToMessageId = msg.ReplyToMessageId,
                Sender = new MessageSenderDto { UserId = msg.Sender.UserId }
            };

            dict[item.ChatId] = dto;
            await CacheLastMessageAsync(item.ChatId, msg);
        }

        return dict;
    }

    private static MessageDto? DeserializeCachedLastMessage(string json)
    {
        try
        {
            var cached = JsonSerializer.Deserialize<CachedLastMessage?>(json, CachedMessageSerializerOptions);
            if (cached == null)
            {
                return null;
            }

            return new MessageDto
            {
                Id = cached.MessageId,
                ChatRoomId = cached.ChatId,
                Content = cached.Content,
                Type = cached.Type,
                SentAt = cached.SentAt,
                EditedAt = cached.EditedAt,
                IsDeleted = cached.IsDeleted,
                Sender = new MessageSenderDto
                {
                    UserId = cached.SenderId,
                    Username = cached.SenderUsername,
                    AvatarUrl = cached.SenderAvatarUrl
                }
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CachedLastMessage BuildCachedLastMessage(MongoMessage message) => new()
    {
        MessageId = message.Id,
        ChatId = message.ChatId,
        Content = message.Content ?? string.Empty,
        Type = string.IsNullOrWhiteSpace(message.Type) ? "text" : message.Type,
        SenderId = message.Sender.UserId,
        SenderUsername = message.Sender.Username,
        SenderAvatarUrl = message.Sender.AvatarUrl,
        SentAt = message.SentAt,
        EditedAt = message.EditedAt,
        IsDeleted = message.IsDeleted
    };

    private async Task RefreshChatLastMessageCacheAsync(int chatId)
    {
        if (!_redisService.IsAvailable)
        {
            return;
        }

        var latest = await _messageRepository.GetChatMessagesAsync(chatId, null, 1);
        if (latest.Count > 0)
        {
            var latestMessage = latest[0];
            await CacheLastMessageAsync(chatId, latestMessage);
            await UpdateChatSortedSetAsync(chatId, latestMessage.SentAt);
            return;
        }

        await _redisService.HashDeleteAsync(RedisCacheKeys.ChatLastMessagesKey, chatId.ToString());
        await RemoveChatFromSortedSetsAsync(chatId);
    }

    private sealed record CachedLastMessage
    {
        public string MessageId { get; init; } = string.Empty;
        public int ChatId { get; init; }
        public string Content { get; init; } = string.Empty;
        public string Type { get; init; } = "text";
        public int SenderId { get; init; }
        public string? SenderUsername { get; init; }
        public string? SenderAvatarUrl { get; init; }
        public DateTime SentAt { get; init; }
        public DateTime? EditedAt { get; init; }
        public bool IsDeleted { get; init; }
    }

    private static MongoMessage BuildMongoMessage(MessageCreateDto dto, User sender)
    {
        return new MongoMessage
        {
            ChatId = dto.ChatRoomId,
            Sender = new MessageSender
            {
                UserId = sender.Id,
                Username = sender.Username,
                DisplayName = sender.DisplayName,
            },
            Content = dto.Content ?? string.Empty,
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "text" : dto.Type,
            ReplyToMessageId = dto.ReplyToMessageId,
            SentAt = DateTime.UtcNow
        };
    }

    private async Task UpdateContactStatsAsync(IEnumerable<int> participantIds, int senderId, DateTime sentAt, CancellationToken cancellationToken)
    {
        var distinctParticipants = participantIds
            .Where(id => id != senderId)
            .Distinct()
            .ToList();

        foreach (var participantId in distinctParticipants)
        {
            await UpdateContactAsync(senderId, participantId, sentAt, cancellationToken);
            await UpdateContactAsync(participantId, senderId, sentAt, cancellationToken);
        }
    }

    private async Task UpdateContactAsync(int ownerId, int contactUserId, DateTime sentAt, CancellationToken cancellationToken)
    {
        var contact = await _context.Contacts
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.ContactUserId == contactUserId,
                cancellationToken);

        if (contact == null)
        {
            return;
        }

        contact.LastMessageAt = sentAt;
    }

    private async Task TryDeleteMessageAsync(string id)
    {
        try
        {
            await _messageRepository.DeleteMessagePermanentlyAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete orphaned message {MessageId}", id);
        }
    }

    private static string? ValidateMessage(MessageCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
        {
            return "Message content cannot be empty.";
        }

        if (dto.Content.Length > MaxMessageLength)
        {
            return $"Message content exceeds maximum length of {MaxMessageLength} characters.";
        }

        return null;
    }

    public async Task<MessagingResult> DeleteMessageAsync(string messageId, int userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentNullException(nameof(messageId));
        }

        try
        {
            var message = await _messageRepository.GetMessageByIdAsync(messageId);
            if (message == null)
            {
                return MessagingResult.Failure("Message not found.");
            }

            if (message.IsDeleted)
            {
                return MessagingResult.Failure("Message is already deleted.");
            }

            var isAuthor = message.Sender.UserId == userId;

            if (!isAuthor)
            {
                var chat = await _context.ChatRooms
                    .Include(cr => cr.Members)
                    .FirstOrDefaultAsync(cr => cr.Id == message.ChatId, cancellationToken);

                if (chat == null)
                {
                    return MessagingResult.Failure("Chat room not found.");
                }

                var member = chat.Members.FirstOrDefault(m => m.UserId == userId);
                if (member == null)
                {
                    return MessagingResult.Failure("You don't have permission to delete this message.");
                }
            }

            var success = await _messageRepository.DeleteMessagePermanentlyAsync(messageId);
            if (!success) return MessagingResult.Failure("Failed to delete message.");

            var clearedReplies = await _messageRepository.ClearReplyReferencesAsync(messageId);
    

            var chatRoom = await _context.ChatRooms.FindAsync(new object[] { message.ChatId }, cancellationToken);
            string newPreviewContent = "";
            DateTime newTime = DateTime.UtcNow;

            if (chatRoom != null)
            {
                var newLastMessage = await _messageRepository.GetLastMessageAsync(message.ChatId, excludeMessageId: messageId);

                if (newLastMessage != null)
                {
                    // Сообщение найдено
                    newPreviewContent = newLastMessage.Content;
                    if (!string.IsNullOrEmpty(newPreviewContent) && newPreviewContent.Length > 30) 
                        newPreviewContent = newPreviewContent.Substring(0, 30) + "...";
                    
                    newTime = newLastMessage.SentAt;
                }
                else
                {
                    newPreviewContent = "";
                    
                    newTime = chatRoom.CreatedAt; 
                }

                chatRoom.LastMessageContent = newPreviewContent;
                chatRoom.LastMessageAt = newTime;

                await _context.SaveChangesAsync(cancellationToken);

                try 
                {
                    await _redisService.HashDeleteAsync(RedisCacheKeys.ChatLastMessagesKey, message.ChatId.ToString());
                    double newScore = newTime.Ticks; 
                    
                    foreach(var member in chatRoom.Members)
                    {
                        await _redisService.KeyDeleteAsync($"user:{member.UserId}:chats");
                        
                        var sortedSetKey = RedisCacheKeys.GetUserChatSortedSetKey(member.UserId);
                        await _redisService.UpdateSortedSetAsync(sortedSetKey, message.ChatId.ToString(), newScore);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Redis cleanup failed: {ex.Message}");
                }
            }

            return MessagingResult.SuccessResult(
                messageId: messageId, 
                sentAt: DateTime.UtcNow, 
                resurrectedUserId: null, 
                clearedReplyMessageIds: clearedReplies,
                newLastMessageContent: newPreviewContent,
                newLastMessageTime: newTime
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message {MessageId}", messageId);
            return MessagingResult.Failure(ex.Message);
        }
    }

    public async Task<MessagingResult> EditMessageAsync(string messageId, int userId, string newContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentNullException(nameof(messageId));
        }

        if (string.IsNullOrWhiteSpace(newContent))
        {
            return MessagingResult.Failure("Message content cannot be empty.");
        }

        if (newContent.Length > MaxMessageLength)
        {
            return MessagingResult.Failure($"Message content exceeds maximum length of {MaxMessageLength} characters.");
        }

        try
        {
            var message = await _messageRepository.GetMessageByIdAsync(messageId);
            if (message == null)
            {
                return MessagingResult.Failure("Message not found.");
            }

            if (message.IsDeleted)
            {
                return MessagingResult.Failure("Cannot edit a deleted message.");
            }

            // Only the message author can edit
            if (message.Sender.UserId != userId)
            {
                return MessagingResult.Failure("You can only edit your own messages.");
            }

            // Perform edit
            var success = await _messageRepository.EditMessageAsync(messageId, newContent);
            if (!success)
            {
                return MessagingResult.Failure("Failed to edit message.");
            }

            await RefreshChatLastMessageCacheAsync(message.ChatId);

            _logger.LogInformation("Message {MessageId} edited by user {UserId}", messageId, userId);
            return MessagingResult.SuccessResult(messageId, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit message {MessageId}", messageId);
            return MessagingResult.Failure(ex.Message);
        }
    }
    
    public async Task<long> MarkMessagesAsReadUntilAsync(int chatId, int userId, DateTime untilTimestamp, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify user is a member of the chat
            var chatRoom = await _context.ChatRooms
                .Include(cr => cr.Members)
                .FirstOrDefaultAsync(cr => cr.Id == chatId, cancellationToken);

            if (chatRoom == null)
            {
                _logger.LogWarning("Chat room {ChatId} not found", chatId);
                return 0;
            }

            var member = chatRoom.Members.FirstOrDefault(m => m.UserId == userId);
            if (member == null)
            {
                _logger.LogWarning("User {UserId} is not a member of chat {ChatId}", userId, chatId);
                return 0;
            }

            // Mark messages as read
            var count = await _messageRepository.MarkAsReadUntilAsync(chatId, userId, untilTimestamp);
            
            _logger.LogInformation("{Count} messages marked as read in chat {ChatId} by user {UserId}", count, chatId, userId);
            if (count > 0)
                {
                    await _unreadCounterService.RecalculateUnreadAsync(chatId, userId);
                }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark messages as read in chat {ChatId}", chatId);
            return 0;
        }
    }

    private async Task<Dictionary<int, MessageDto>> FetchLastMessagesFromMongoWithDatesAsync(Dictionary<int, DateTime?> chatsWithClearDates)
    {
        var builder = Builders<MongoMessage>.Filter;
        var filtersList = new List<FilterDefinition<MongoMessage>>();

        foreach (var chat in chatsWithClearDates)
        {
            var chatId = chat.Key;
            var minDate = chat.Value;

            var chatFilter = builder.Eq(m => m.ChatId, chatId) & 
                            builder.Eq(m => m.IsDeleted, false);

            if (minDate.HasValue)
            {
                chatFilter &= builder.Gt(m => m.SentAt, minDate.Value);
            }

            filtersList.Add(chatFilter);
        }

        if (!filtersList.Any()) return new Dictionary<int, MessageDto>();

        var globalFilter = builder.Or(filtersList);

        var aggregation = _messages.Aggregate()
            .Match(globalFilter)            
            .SortByDescending(m => m.SentAt) 
            .Group(m => m.ChatId, g => new 
            { 
                ChatId = g.Key, 
                LastMessage = g.First()      
            });

        var results = await aggregation.ToListAsync();
        var dict = new Dictionary<int, MessageDto>();

        foreach (var item in results)
        {
            var msg = item.LastMessage;
            
            var dto = new MessageDto
            {
                Id = msg.Id,
                ChatRoomId = msg.ChatId,
                Content = msg.Content,
                Type = msg.Type, 
                SentAt = msg.SentAt,
                EditedAt = msg.EditedAt,
                IsDeleted = msg.IsDeleted,
                ReplyToMessageId = msg.ReplyToMessageId,
                Sender = new MessageSenderDto { UserId = msg.Sender.UserId },
            };

            dict[item.ChatId] = dto;

            await CacheLastMessageAsync(item.ChatId, msg);
        }

        return dict;
    }
}
