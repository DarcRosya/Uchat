using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Server.Services.Redis;

namespace Uchat.Server.Services.Chat;

public sealed class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ChatRoomService> _logger;
    private readonly IRedisService _redisService;

    private static readonly TimeSpan ChatListSortedSetTtl = TimeSpan.FromHours(24);

    public ChatRoomService(
        IChatRoomRepository chatRoomRepository,
        IUserRepository userRepository,
        ILogger<ChatRoomService> logger,
        IRedisService redisService)
    {
        _chatRoomRepository = chatRoomRepository;
        _userRepository = userRepository;
        _logger = logger;
        _redisService = redisService;
    }

    public async Task<List<ChatRoom>> GetUserChatsAsync(int userId)
    {
        var sortedKey = RedisCacheKeys.GetUserChatSortedSetKey(userId);
        var cachedIds = (await _redisService.GetSortedKeysAsync(sortedKey))
            .Select(candidate => int.TryParse(candidate, out var parsed) ? parsed : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (cachedIds.Any())
        {
            var cachedChats = await _chatRoomRepository.GetChatRoomsByIdsAsync(cachedIds);
            var chatLookup = cachedChats.ToDictionary(c => c.Id);
            if (cachedIds.All(id => chatLookup.ContainsKey(id)))
            {
                var ordered = cachedIds.Select(id => chatLookup[id]).ToList();
                return ordered;
            }
        }

        var chats = (await _chatRoomRepository.GetUserChatRoomsAsync(userId)).ToList();
        if (chats.Any())
        {
            await RebuildUserChatSortedSetAsync(userId, chats);
        }

        return chats;
    }

    public async Task<ChatResult> GetChatDetailsAsync(int chatId, int userId)
    {
        var chat = await _chatRoomRepository.GetByIdAsync(chatId);
        
        if (chat == null) 
            return ChatResult.NotFound();

        // Проверка доступа
        bool isMember = chat.Members.Any(m => m.UserId == userId);
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

    private async Task RebuildUserChatSortedSetAsync(int userId, IEnumerable<ChatRoom> chats)
    {
        if (!_redisService.IsAvailable)
        {
            return;
        }

        var sortedKey = RedisCacheKeys.GetUserChatSortedSetKey(userId);
        var tasks = chats.Select(chat =>
        {
            var score = new DateTimeOffset(chat.LastActivityAt ?? chat.CreatedAt).ToUnixTimeSeconds();
            return _redisService.UpdateSortedSetAsync(sortedKey, chat.Id.ToString(), score, ChatListSortedSetTtl);
        });

        await Task.WhenAll(tasks);
    }
}