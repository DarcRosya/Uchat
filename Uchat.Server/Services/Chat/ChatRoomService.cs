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

namespace Uchat.Server.Services.Chat;

public sealed class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IMessageService _messageService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ChatRoomService> _logger;
    private readonly IRedisService _redisService;

    private static readonly TimeSpan ChatListSortedSetTtl = TimeSpan.FromHours(24);

    public ChatRoomService(
        IChatRoomRepository chatRoomRepository,
        IUserRepository userRepository,
        IMessageService messageService,
        ILogger<ChatRoomService> logger,
        IRedisService redisService)
    {
        _chatRoomRepository = chatRoomRepository;
        _messageService = messageService;
        _userRepository = userRepository;
        _logger = logger;
        _redisService = redisService;
    }

    public async Task<List<ChatRoomDto>> GetUserChatsAsync(int userId)
    {
        List<ChatRoomMember> members;
        
        var sortedKey = RedisCacheKeys.GetUserChatSortedSetKey(userId);
        var cachedIds = await _redisService.GetSortedKeysAsync(sortedKey);

        var chatIds = cachedIds
            .Select(idStr => int.TryParse(idStr, out var id) ? id : (int?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        if (chatIds.Any())
        {
            // Загружаем из БД только нужные мембершипы
            var unsortedMembers = await _chatRoomRepository.GetMembersForUserByChatIdsAsync(userId, chatIds);
            
            // ВАЖНО: БД вернет данные в разнобой. Нам нужно восстановить порядок, который был в Redis.
            var memberLookup = unsortedMembers.ToDictionary(m => m.ChatRoomId);
            
            members = new List<ChatRoomMember>();
            foreach (var id in chatIds)
            {
                // Если чат есть в Redis, но удален из БД — пропускаем (самовосстановление)
                if (memberLookup.TryGetValue(id, out var member))
                {
                    members.Add(member);
                }
            }
        }
        // 3. СЦЕНАРИЙ Б: КЭША НЕТ (Redis пуст или упал)
        else
        {
            // Грузим всё из базы (уже с сортировкой)
            members = await _chatRoomRepository.GetUserChatMembershipsAsync(userId);
            
            // И сразу восстанавливаем кэш на будущее (фоновая задача)
            if (members.Any())
            {
                await RebuildUserChatSortedSetAsync(userId, members);
            }
        }

        // 4. ПОДГОТОВКА ДЛЯ ПОЛУЧЕНИЯ СООБЩЕНИЙ
        // Создаем словарь: ID Чата -> Когда МЫ очистили историю
        var chatsWithClearDates = members.ToDictionary(
            m => m.ChatRoomId, 
            m => m.ClearedHistoryAt
        );

        // 5. Загружаем последние сообщения (с учетом очистки истории!)
        // Этот метод мы исправляли в прошлом шаге (в MessageService)
        var lastMessages = await _messageService.GetLastMessagesForChatsBatch(chatsWithClearDates);

        // 6. Маппинг в DTO
        var result = members.Select(m => 
        {
            var chat = m.ChatRoom;
            
            // Ищем последнее сообщение для этого чата
            lastMessages.TryGetValue(chat.Id, out var lastMsg);

            // Если сообщения нет (оно скрыто историей или чат новый)
            string lastContent = lastMsg?.Content ?? "";
            DateTime? lastDate = lastMsg?.SentAt;

            // Определяем название и аватарку чата (для личных чатов берем собеседника)
            string chatName = chat.Name;
            bool isOnline = false;

            if (chat.Type == ChatRoomType.DirectMessage)
            {
                var otherMember = chat.Members.FirstOrDefault(x => x.UserId != userId);
                if (otherMember != null)
                {
                    // Тут можно подтянуть данные юзера, если они не загружены
                    // (обычно они есть в Include(cr => cr.Members).ThenInclude(m => m.User))
                    // chatName = otherMember.User.DisplayName...
                }
            }

            return new ChatRoomDto
            {
                Id = chat.Id,
                Type = chat.Type.ToString(),
                Name = chatName,
                UnreadCount = 0, // Тут вызов _unreadCounterService.GetUnreadCount(...)
                LastMessageContent = lastContent,
                LastMessageAt = lastDate,
            };
        }).ToList();

        return result;
    }

    public async Task<ChatResult> GetChatDetailsAsync(int chatId, int userId)
    {
        var chat = await _chatRoomRepository.GetByIdAsync(chatId);
        
        if (chat == null) 
            return ChatResult.NotFound();

        // Проверка доступа
        bool isMember = chat.Members.Any(crm => crm.UserId == userId && !crm.IsDeleted);
        if (!isMember && chat.Type != ChatRoomType.Public)
        {
            return ChatResult.Forbidden();
        }

        return ChatResult.Success(chat);
    }

    public async Task<ChatResult> CreateChatAsync(
        int creatorId, 
        string name, 
        ChatRoomType type, 
        string? description, 
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
            Description = description,
            CreatedAt = DateTime.UtcNow,
            TotalMessagesCount = 0
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
            
            // Перезагружаем с участниками
            var fullChat = await _chatRoomRepository.GetByIdAsync(created.Id);
            return ChatResult.Success(fullChat!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat");
            return ChatResult.Failure("Failed to create chat: " + ex.Message);
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

        // Проверяем, что новый участник ещё не в чате
        bool isAlreadyMember = chat.Members.Any(m => m.UserId == memberUserId);
        if (isAlreadyMember)
            return ChatResult.Failure("User is already a member");

        try
        {
            await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
            {
                ChatRoomId = chatId,
                UserId = memberUserId,
                JoinedAt = DateTime.UtcNow,
                InvitedById = actorUserId
            });

            return ChatResult.Success(chat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add member");
            return ChatResult.Failure("Failed to add member: " + ex.Message);
        }
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
            await _chatRoomRepository.RemoveMemberAsync(chatId, memberUserId);
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