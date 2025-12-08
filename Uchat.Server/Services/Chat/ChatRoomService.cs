using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;

namespace Uchat.Server.Services.Chat;

public sealed class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ChatRoomService> _logger;

    public ChatRoomService(
        IChatRoomRepository chatRoomRepository,
        IUserRepository userRepository,
        ILogger<ChatRoomService> logger)
    {
        _chatRoomRepository = chatRoomRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<List<ChatRoomMember>> GetUserChatMembershipsAsync(int userId)
    {
        return await _chatRoomRepository.GetUserChatMembershipsAsync(userId);
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
}