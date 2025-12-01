using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Uchat.Database.Context;
using Uchat.Database.Entities;
using Uchat.Database.Extensions;
using Uchat.Database.LiteDB;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Database.Services.Messaging;

/// <summary>
/// Service for managing chat messages across SQLite (metadata) and LiteDB (content).
/// Ensures transactional consistency between both databases.
/// </summary>
public sealed class MessageService : IMessageService
{
    private readonly UchatDbContext _context;
    private readonly LiteDbContext _liteDbContext;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<MessageService> _logger;

    private const int MaxMessageLength = 5000;
    private const int MaxAttachments = 10;

    public MessageService(
        UchatDbContext context,
        LiteDbContext liteDbContext,
        IMessageRepository messageRepository,
        ILogger<MessageService> logger)
    {
        _context = context;
        _liteDbContext = liteDbContext;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<MessagingResult> SendMessageAsync(MessageCreateDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        // Validate message content
        var validationError = ValidateMessage(dto);
        if (validationError != null)
        {
            return MessagingResult.Failure(validationError);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        LiteDbMessage? liteMessage = null;
        string? messageId = null;

        try
        {
            var chatRoom = await _context.ChatRooms
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

            // Check if sender has permission to send messages
            if (!senderMember.CanSendMessages())
            {
                return MessagingResult.Failure("You don't have permission to send messages in this chat.");
            }

            var sender = await _context.Users.FindAsync(new object[] { dto.SenderId }, cancellationToken: cancellationToken);
            if (sender == null)
            {
                return MessagingResult.Failure($"Sender {dto.SenderId} not found.");
            }

            liteMessage = BuildLiteDbMessage(dto, sender);
            
            // Сохранение в LiteDB
            if (string.IsNullOrEmpty(liteMessage.Id))
            {
                liteMessage.Id = ObjectId.NewObjectId().ToString();
            }
            liteMessage.SentAt = DateTime.UtcNow;
            
            var messages = _liteDbContext.Messages;
            messages.Insert(liteMessage);
            messageId = liteMessage.Id;

            chatRoom.LastActivityAt = liteMessage.SentAt;
            chatRoom.TotalMessagesCount++;

            await UpdateContactStatsAsync(chatRoom.Members.Select(m => m.UserId), dto.SenderId, liteMessage.SentAt, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return MessagingResult.SuccessResult(messageId, liteMessage.SentAt);
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

    private static LiteDbMessage BuildLiteDbMessage(MessageCreateDto dto, User sender)
    {
        return new LiteDbMessage
        {
            ChatId = dto.ChatRoomId,
            Sender = new MessageSender
            {
                UserId = sender.Id,
                Username = sender.Username,
                DisplayName = sender.DisplayName,
                AvatarUrl = sender.AvatarUrl
            },
            Content = dto.Content ?? string.Empty,
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "text" : dto.Type,
            Attachments = dto.Attachments != null
                ? dto.Attachments.ToList()
                : new List<MessageAttachment>(),
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
        contact.MessageCount++;
    }

    private async Task TryDeleteMessageAsync(string id)
    {
        try
        {
            await _messageRepository.DeleteMessageAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete orphaned message {MessageId}", id);
        }
    }

    private static string? ValidateMessage(MessageCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content) && (dto.Attachments == null || dto.Attachments.Count == 0))
        {
            return "Message must have either content or attachments.";
        }

        if (dto.Content != null && dto.Content.Length > MaxMessageLength)
        {
            return $"Message content exceeds maximum length of {MaxMessageLength} characters.";
        }

        if (dto.Attachments != null && dto.Attachments.Count > MaxAttachments)
        {
            return $"Message cannot have more than {MaxAttachments} attachments.";
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

            // Check if user is the message author
            var isAuthor = message.Sender.UserId == userId;

            if (!isAuthor)
            {
                // Check if user has permission to delete messages in this chat
                var chatRoom = await _context.ChatRooms
                    .Include(cr => cr.Members)
                    .FirstOrDefaultAsync(cr => cr.Id == message.ChatId, cancellationToken);

                if (chatRoom == null)
                {
                    return MessagingResult.Failure("Chat room not found.");
                }

                var member = chatRoom.Members.FirstOrDefault(m => m.UserId == userId);
                if (member == null || !member.CanDeleteMessages())
                {
                    return MessagingResult.Failure("You don't have permission to delete this message.");
                }
            }

            // Perform soft delete
            var success = await _messageRepository.DeleteMessageAsync(messageId);
            if (!success)
            {
                return MessagingResult.Failure("Failed to delete message.");
            }

            _logger.LogInformation("Message {MessageId} deleted by user {UserId}", messageId, userId);
            return MessagingResult.SuccessResult(messageId, DateTime.UtcNow);
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
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark messages as read in chat {ChatId}", chatId);
            return 0;
        }
    }
}
