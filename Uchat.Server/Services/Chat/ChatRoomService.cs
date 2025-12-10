using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Uchat.Server.Services.Messaging;
using Uchat.Shared.DTOs;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.Services.Redis;
using Uchat.Server.Services.Unread;
using Microsoft.AspNetCore.SignalR;
using Uchat.Server.Hubs;

namespace Uchat.Server.Services.Chat;

public sealed class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IMessageService _messageService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ChatRoomService> _logger;
    private readonly IRedisService _redisService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IUnreadCounterService _unreadCounterService;

    private static readonly TimeSpan ChatListSortedSetTtl = TimeSpan.FromHours(24);

    public ChatRoomService(
        IChatRoomRepository chatRoomRepository,
        IUserRepository userRepository,
        IMessageService messageService,
        ILogger<ChatRoomService> logger,
        IRedisService redisService,
        IHubContext<ChatHub> hubContext,
        IUnreadCounterService unreadCounterService)
    {
        _chatRoomRepository = chatRoomRepository;
        _messageService = messageService;
        _userRepository = userRepository;
        _logger = logger;
        _redisService = redisService;
        _hubContext = hubContext;
        _unreadCounterService = unreadCounterService;
    }

    public async Task<List<ChatRoomDto>> GetUserChatsAsync(int userId)
    {
        List<ChatRoomMember> members;
        
        var sortedKey = RedisCacheKeys.GetUserChatSortedSetKey(userId);
        var cachedIdsRaw = await _redisService.GetSortedKeysAsync(sortedKey);

        var chatIdsFromRedis = cachedIdsRaw
                .Select(idStr => int.TryParse(idStr, out var id) ? id : (int?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

        if (chatIdsFromRedis.Any())
        {
            var unsortedMembers = await _chatRoomRepository.GetMembersForUserByChatIdsAsync(userId, chatIdsFromRedis);
            var memberLookup = unsortedMembers.ToDictionary(m => m.ChatRoomId);
            
            members = new List<ChatRoomMember>();
            foreach (var id in chatIdsFromRedis)
            {
                if (memberLookup.TryGetValue(id, out var member)) members.Add(member);
            }
        }
        // СЦЕНАРИЙ Б: КЭША НЕТ (Redis пуст или упал)
        else
        {
            members = await _chatRoomRepository.GetUserChatMembershipsAsync(userId);
            if (members.Any()) await RebuildUserChatSortedSetAsync(userId, members);
        }

        if (!members.Any()) return new List<ChatRoomDto>();

        // ---------------------------------------------------------
        // 2. ПАРАЛЛЕЛЬНАЯ ЗАГРУЗКА ДАННЫХ
        // ---------------------------------------------------------
        
        // А) Словари для MessageService
        var clearDatesDict = members.ToDictionary(m => m.ChatRoomId, m => m.ClearedHistoryAt);
        var allChatIds = members.Select(m => m.ChatRoomId).ToList();

        // Б) Собираем ID собеседников для личных чатов, чтобы узнать их Имена/Аватарки
        var partnerIds = members
            .Where(m => m.ChatRoom.Type == ChatRoomType.DirectMessage)
            .SelectMany(m => m.ChatRoom.Members) // Берем всех участников чата
            .Where(m => m.UserId != userId)      // Исключаем себя
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        // В) Запускаем задачи параллельно
        var messagesTask = _messageService.GetLastMessagesForChatsBatch(clearDatesDict);
        var unreadTask = _unreadCounterService.GetUnreadCountsAsync(userId, allChatIds);
        var usersTask = _userRepository.GetUsersByIdsAsync(partnerIds);

        await Task.WhenAll(messagesTask, unreadTask, usersTask);

        var lastMessages = messagesTask.Result;
        var unreadCounts = unreadTask.Result;
        var usersDict = usersTask.Result.ToDictionary(u => u.Id); // Dictionary<int, User>

        // ---------------------------------------------------------
        // 3. СБОРКА DTO
        // ---------------------------------------------------------
        var result = members.Select(m => 
        {
            var chat = m.ChatRoom;
            
            // --- 3.1. Данные собеседника ---
            string chatName = chat.Name ?? "Unknown";

            if (chat.Type == ChatRoomType.DirectMessage)
            {
                // Находим ID партнера в списке участников чата
                var partnerId = chat.Members.FirstOrDefault(x => x.UserId != userId)?.UserId;
                
                if (partnerId.HasValue && usersDict.TryGetValue(partnerId.Value, out var partnerUser))
                {
                    chatName = partnerUser.DisplayName ?? partnerUser.Username;
                }
            }

            // --- 3.2. Последнее сообщение ---
            string lastContent = "";
            DateTime? lastDate = chat.LastActivityAt ?? chat.CreatedAt;

            if (lastMessages.TryGetValue(chat.Id, out var msgDto))
            {
                lastContent = msgDto.Content;
                lastDate = msgDto.SentAt;
            }

            // --- 3.3. Непрочитанные ---
            int unread = unreadCounts.TryGetValue(chat.Id, out var count) ? count : 0;

            return new ChatRoomDto
            {
                Id = chat.Id,
                Type = chat.Type.ToString(),
                Name = chatName,
                UnreadCount = unread, // Заполнили реальный счетчик
                LastMessageContent = lastContent,
                LastMessageAt = lastDate,
                CreatedAt = chat.CreatedAt,
                MemberCount = chat.Members.Count,
                ParticipantIds = chat.Members
                    .Select(m => m.UserId)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList()
                /*
                MemberCount = chat.Members.Count,
                IsPinned = m.IsPinned,
                PinnedAt = m.PinnedAt
                */
            };
        }).ToList();

        return result
            .OrderByDescending(x => x.IsPinned)        
            .ThenByDescending(x => x.PinnedAt)            
            .ThenByDescending(x => x.LastMessageAt ?? DateTime.MinValue) 
            .ToList();
    }

    public async Task<ChatResult> GetChatDetailsAsync(int chatId, int userId)
    {
        var chat = await _chatRoomRepository.GetByIdAsync(chatId);
        
        if (chat == null) 
            return ChatResult.NotFound();

        // Проверка доступа
        bool isMember = chat.Members.Any(crm => crm.UserId == userId && !crm.IsDeleted && !crm.IsPending);
        if (!isMember && chat.Type != ChatRoomType.Public)
        {
            return ChatResult.Forbidden();
        }

        chat.Members = chat.Members
            .Where(m => !m.IsDeleted && !m.IsPending)
            .ToList();

        return ChatResult.Success(chat);
    }

    public async Task<ChatResult> CreateChatAsync(
        int creatorId, 
        string name, 
        ChatRoomType type, 
        IEnumerable<int>? initialMemberIds)
    {
        // Валидация
        if (type != ChatRoomType.DirectMessage && string.IsNullOrWhiteSpace(name))
            return ChatResult.Failure("Chat name is required.");

        var creator = await _userRepository.GetByIdAsync(creatorId);
        if (creator == null) 
            return ChatResult.Failure("Creator not found.");

        // Создаём чат
        var chatRoom = new ChatRoom
        {
            CreatorId = creatorId,
            Name = name,
            Type = type,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            var created = await _chatRoomRepository.CreateAsync(chatRoom);
            
            // Добавляем создателя
            await _chatRoomRepository.AddMemberAsync(new ChatRoomMember 
            { 
                ChatRoomId = created.Id, 
                UserId = creatorId,
                JoinedAt = DateTime.UtcNow
            });

            // Добавляем остальных участников
            if (initialMemberIds != null)
            {
                foreach(var memberId in initialMemberIds.Where(id => id != creatorId))
                {
                    await _chatRoomRepository.AddMemberAsync(new ChatRoomMember 
                    { 
                        ChatRoomId = created.Id, 
                        UserId = memberId,
                        JoinedAt = DateTime.UtcNow,
                        InvitedById = creatorId
                    });
                }
            }
            
            if (_redisService.IsAvailable)
            {
                var sortedKey = RedisCacheKeys.GetUserChatSortedSetKey(creatorId);
                var score = new DateTimeOffset(created.CreatedAt).ToUnixTimeSeconds();
                
                // Добавляем ID нового чата в список чатов пользователя в Redis
                await _redisService.UpdateSortedSetAsync(
                    sortedKey, 
                    created.Id.ToString(), 
                    score, 
                    TimeSpan.FromHours(24) // Ваш TTL
                );
            }
            // =========================================================

            var fullChat = await _chatRoomRepository.GetByIdAsync(created.Id);
            return ChatResult.Success(fullChat!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat");
            return ChatResult.Failure("Failed to create chat: " + ex.Message);
        }
    }

    public async Task<ChatResult> AcceptInviteAsync(int chatId, int userId)
    {
        // Используем репозиторий для поиска
        var member = await _chatRoomRepository.GetMemberAsync(chatId, userId);

        // Проверяем существование и статус
        if (member == null || !member.IsPending) 
            return ChatResult.NotFound();

        member.IsPending = false; // Становится полноценным участником
        member.JoinedAt = DateTime.UtcNow;
        
        // Сохраняем через репозиторий
        await _chatRoomRepository.UpdateMemberAsync(member);

        if (_redisService.IsAvailable)
        {
            var sortedKey = RedisCacheKeys.GetUserChatSortedSetKey(userId);
            
            var score = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            
            await _redisService.UpdateSortedSetAsync(
                sortedKey, 
                chatId.ToString(), 
                score, 
                TimeSpan.FromHours(24)
            );
        }

        var user = await _userRepository.GetByIdAsync(userId);
        await _hubContext.Clients.Group(chatId.ToString())
            .SendAsync("MemberJoined", chatId, user.Username); // Отправляем ChatId и Имя

        var chat = await _chatRoomRepository.GetByIdAsync(chatId);
        return ChatResult.Success(chat!);
    }

    public async Task<ChatResult> RejectInviteAsync(int chatId, int userId)
    {
        // Используем репозиторий для поиска
        var member = await _chatRoomRepository.GetMemberAsync(chatId, userId);

        if (member == null || !member.IsPending) 
            return ChatResult.NotFound();

        // Удаляем заявку через репозиторий
        await _chatRoomRepository.RemoveMemberEntityAsync(member);

        return ChatResult.Success(null);
    }

    public async Task<ChatResult> JoinPublicChatByNameAsync(string chatName, int userId)
    {
        // 1. Ищем чат по имени (Точное совпадение, без учета регистра)
        // Важно: Убедись, что в репозитории есть метод GetByNameAsync или используй логику ниже
        var chat = await _chatRoomRepository.GetByNameAsync(chatName);

        if (chat == null)
        {
            return ChatResult.NotFound(); // Группа не найдена
        }

        // 2. Проверяем тип чата
        if (chat.Type != ChatRoomType.Public)
        {
            // Нельзя просто так вступить в приватную группу по имени
            return ChatResult.Forbidden(); 
        }

        // 3. Проверяем, может пользователь уже внутри?
        bool isAlreadyMember = chat.Members.Any(m => m.UserId == userId && !m.IsDeleted);
        if (isAlreadyMember)
        {
            // Если уже участник - просто возвращаем чат как успех
            return ChatResult.Success(chat);
        }

        try
        {
            // 4. Добавляем пользователя
            var newMember = new ChatRoomMember
            {
                ChatRoomId = chat.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow,
                IsPending = false, // В публичную группу вступаем сразу, без приглашения
                InvitedById = null // Зашел сам
            };

            await _chatRoomRepository.AddMemberAsync(newMember);

            // 5. Обновляем Redis (КЭШ), чтобы чат появился у пользователя в списке GetUserChats
            if (_redisService.IsAvailable)
            {
                var sortedKey = RedisCacheKeys.GetUserChatSortedSetKey(userId);
                // Ставим текущее время как время активности, чтобы чат был сверху
                var score = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                
                await _redisService.UpdateSortedSetAsync(
                    sortedKey, 
                    chat.Id.ToString(), 
                    score, 
                    TimeSpan.FromHours(24)
                );
            }

            // Возвращаем обновленный объект чата
            // (лучше дернуть GetById, чтобы подтянулись все свежие данные включая нового мембера)
            var updatedChat = await _chatRoomRepository.GetByIdAsync(chat.Id);
            return ChatResult.Success(updatedChat!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join public chat");
            return ChatResult.Failure("Database error while joining chat.");
        }
    }

    public async Task<ChatResult> SetGroupPinAsync(int userId, int chatRoomId, bool isPinned)
    {
        try
        {
            var member = await _chatRoomRepository.GetMemberAsync(chatRoomId, userId);

            if (member == null || member.IsDeleted || member.IsPending)
            {
                return ChatResult.Failure("User is not an active member of this chat.");
            }

            member.IsPinned = isPinned;
            member.PinnedAt = isPinned ? DateTime.UtcNow : null;

            await _chatRoomRepository.UpdateMemberAsync(member);

            var chatRoom = await _chatRoomRepository.GetByIdAsync(chatRoomId);
            
            if (chatRoom == null) return ChatResult.NotFound();

            return ChatResult.Success(chatRoom);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting pin status");
            return ChatResult.Failure($"Failed to update pin status: {ex.Message}");
        }
    }

    public async Task<ChatResult> UpdateChatNameAsync(int chatId, int userId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return ChatResult.Failure("Chat name cannot be empty.");
        }

        var chat = await _chatRoomRepository.GetByIdAsync(chatId);
        
        if (chat == null) 
            return ChatResult.NotFound();

        if (chat.Type == ChatRoomType.DirectMessage)
        {
            return ChatResult.Failure("Cannot rename a direct message chat.");
        }

        var member = chat.Members.FirstOrDefault(m => m.UserId == userId && !m.IsDeleted && !m.IsPending);
        if (member == null)
        {
            return ChatResult.Forbidden();
        }

        try 
        {
            chat.Name = newName.Trim();
            await _chatRoomRepository.UpdateAsync(chat);

            if (_redisService.IsAvailable)
            {
                try 
                {
                    var score = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

                    var redisTasks = chat.Members
                        .Where(m => !m.IsDeleted && !m.IsPending)
                        .Select(m => 
                        {
                            var sortedKey = RedisCacheKeys.GetUserChatSortedSetKey(m.UserId);
                            
                            return _redisService.UpdateSortedSetAsync(
                                sortedKey, 
                                chat.Id.ToString(), 
                                score, 
                                TimeSpan.FromHours(24) // TTL
                            );
                        });

                    await Task.WhenAll(redisTasks);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Redis update failed during chat rename");
                }
            }

            await _hubContext.Clients.Group(chatId.ToString())
                .SendAsync("ChatNameUpdated", chatId, chat.Name);

            return ChatResult.Success(chat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename chat in Database");
            return ChatResult.Failure("Database error while renaming chat.");
        }
    }

    public async Task<ChatResult> AddMemberAsync(int chatId, int actorUserId, int memberUserId)
    {
        var chat = await _chatRoomRepository.GetByIdAsync(chatId);
        if (chat == null) 
            return ChatResult.NotFound();

        // Проверяем, что актёр - участник чата
        bool isActorMember = chat.Members.Any(m => m.UserId == actorUserId);
        if (!isActorMember)
            return ChatResult.Forbidden();

        var existingMember = chat.Members.FirstOrDefault(m => m.UserId == memberUserId);

        try
        {
            if (existingMember != null)
            {

                if (!existingMember.IsDeleted)
                {
                    return ChatResult.Failure("User is already a member");
                }

                // Он удален -> ВОСКРЕШАЕМ (Update)
                existingMember.IsDeleted = false;      
                existingMember.IsPending = true;       
                existingMember.InvitedById = actorUserId; 
                existingMember.JoinedAt = DateTime.UtcNow; 
                
                existingMember.ClearedHistoryAt = DateTime.UtcNow; 

                await _chatRoomRepository.UpdateMemberAsync(existingMember);
            }
            else
            {
                await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                {
                    ChatRoomId = chatId,
                    UserId = memberUserId,
                    JoinedAt = DateTime.UtcNow,
                    InvitedById = actorUserId,
                    IsPending = true,
                    IsDeleted = false
                });
            }

            var inviter = await _userRepository.GetByIdAsync(actorUserId);

            var notificationData = new 
            {
                contactId = chatId,       
                chatRoomId = chatId,         
                friendUsername = inviter.Username, 
                friendDisplayName = chat.Name,     
                Type = "GroupInvite",            
                GroupName = chat.Name,
                InviterUsername = inviter.Username
            };

            await _hubContext.Clients.User(memberUserId.ToString())
                .SendAsync("GroupInviteReceived", notificationData);

            return ChatResult.Success(chat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add member");
            return ChatResult.Failure("Failed to add member: " + ex.Message);
        }
    }

    public async Task<List<ChatRoomDto>> GetPendingGroupInvitesAsync(int userId)
    {
        // Ищем записи, где пользователь добавлен, но IsPending == true
        var pendingMembers = await _chatRoomRepository.GetPendingMembershipsAsync(userId);
        var result = pendingMembers.Select(m => new ChatRoomDto
        {
            Id = m.ChatRoomId,
            Name = m.ChatRoom.Name,
            Type = m.ChatRoom.Type.ToString(),
            // Используем поле LastMessageContent, чтобы передать имя пригласившего (хак, но удобно для DTO)
            LastMessageContent = m.InvitedBy?.Username ?? "Unknown" 
        }).ToList();

        return result;
    }

    public async Task<ChatResult> RemoveMemberAsync(int chatId, int actorUserId, int memberUserId)
    {
        var chat = await _chatRoomRepository.GetByIdAsync(chatId);
        if (chat == null) 
            return ChatResult.NotFound();

        // Проверяем права (создатель или сам пользователь)
        if (chat.CreatorId != actorUserId && actorUserId != memberUserId)
            return ChatResult.Forbidden();

        try
        {
            // 1. Получаем данные пользователя ДО удаления (чтобы знать его Username)
            var userToRemove = await _userRepository.GetByIdAsync(memberUserId);
            
            // 2. Удаляем из БД
            await _chatRoomRepository.RemoveMemberAsync(chatId, memberUserId);

            // 3. !!! НОВОЕ: Уведомляем группу через SignalR !!!
            // Мы отправляем: ID чата и Имя пользователя, который вышел
            if (userToRemove != null)
            {
                await _hubContext.Clients.Group(chatId.ToString())
                    .SendAsync("MemberLeft", chatId, userToRemove.Username);
            }

            return ChatResult.Success(chat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove member");
            return ChatResult.Failure("Failed to remove member: " + ex.Message);
        }
    }

    public async Task<ChatResult> IsUserInChatAsync(int userId, int chatId)
    {
        if (await _chatRoomRepository.GetByIdAsync(chatId) is not { } chat)
        {
            return ChatResult.NotFound();
        }

        bool isMember = chat.Members.Any(m => m.UserId == userId);

        if (!isMember)
        {
            return ChatResult.Forbidden(); 
        }

        return ChatResult.Success(chat);
    }

    private async Task RebuildUserChatSortedSetAsync(int userId, IEnumerable<ChatRoomMember> members)
    {
        if (!_redisService.IsAvailable)
        {
            return;
        }

        var sortedKey = RedisCacheKeys.GetUserChatSortedSetKey(userId);
        
        // Берем дату активности из самого чата
        var tasks = members.Select(member =>
        {
            var chat = member.ChatRoom;
            // Если LastActivityAt null, берем CreatedAt
            var score = new DateTimeOffset(chat.LastActivityAt ?? chat.CreatedAt).ToUnixTimeSeconds();
            
            return _redisService.UpdateSortedSetAsync(
                sortedKey, 
                chat.Id.ToString(), 
                score, 
                RedisCacheKeys.ChatListSortedSetTtl // Используем константу TTL
            );
        });

        await Task.WhenAll(tasks);
    }
}