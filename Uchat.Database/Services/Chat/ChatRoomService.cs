using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.Services.Shared;

namespace Uchat.Database.Services.Chat;

public sealed class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRunner _transactionRunner;
    private readonly ILogger<ChatRoomService> _logger;

    public ChatRoomService(
        IChatRoomRepository chatRoomRepository,
        IUserRepository userRepository,
        ITransactionRunner transactionRunner,
        ILogger<ChatRoomService> logger)
    {
        _chatRoomRepository = chatRoomRepository;
        _userRepository = userRepository;
        _transactionRunner = transactionRunner;
        _logger = logger;
    }

    public async Task<ChatResult> CreateChatAsync(CreateChatDto dto)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        if (dto.CreatorId <= 0)
        {
            LogBad("CHAT CREATION", "Creator must be specified.");
            return ChatResult.Failure("Creator must be specified.");
        }

        if (dto.Type != ChatRoomType.DirectMessage && string.IsNullOrWhiteSpace(dto.Name))
        {
            LogBad("CHAT CREATION", "Chat name is required for group chats.");
            return ChatResult.Failure("Chat name is required for group chats.");
        }

        if (dto.Type == ChatRoomType.Topic && !dto.ParentChatRoomId.HasValue)
        {
            LogBad("CHAT CREATION", "Topic chats must reference a parent room.");
            return ChatResult.Failure("Topic chats must reference a parent room.");
        }

        var creator = await _userRepository.GetByIdAsync(dto.CreatorId);
        if (creator == null)
        {
            LogBad("CHAT CREATION", "Creator not found.");
            return ChatResult.Failure("Creator not found.");
        }

        var initialMemberIds = dto.InitialMemberIds?
            .Where(id => id > 0 && id != dto.CreatorId)
            .Distinct()
            .ToList() ?? new List<int>();

        if (dto.Type == ChatRoomType.DirectMessage && initialMemberIds.Count != 1)
        {
            LogBad("CHAT CREATION", "Direct message chats require exactly one other participant.");
            return ChatResult.Failure("Direct message chats require exactly one other participant.");
        }

        var participantIds = new List<int> { dto.CreatorId };
        participantIds.AddRange(initialMemberIds);

        var existingUsers = (await _userRepository.GetUsersByIdsAsync(participantIds))
            .Select(u => u.Id)
            .ToHashSet();

        var missingUsers = participantIds.Except(existingUsers).ToList();
        if (missingUsers.Count > 0)
        {
            LogBad("CHAT CREATION", $"Users not found: {string.Join(", ", missingUsers)}");
            return ChatResult.Failure($"Users not found: {string.Join(", ", missingUsers)}");
        }

        if (dto.ParentChatRoomId.HasValue)
        {
            var parent = await _chatRoomRepository.GetByIdAsync(dto.ParentChatRoomId.Value);
            if (parent == null)
            {
                LogBad("CHAT CREATION", "Parent chat room not found.");
                return ChatResult.Failure("Parent chat room not found.");
            }
        }

        var chatRoom = new ChatRoom
        {
            Name = dto.Name,
            Description = dto.Description,
            IconUrl = dto.IconUrl,
            Type = dto.Type,
            CreatorId = dto.CreatorId,
            ParentChatRoomId = dto.ParentChatRoomId,
            MaxMembers = dto.MaxMembers
        };

        try
        {
            return await _transactionRunner.RunAsync(async () =>
            {
                var created = await _chatRoomRepository.CreateAsync(chatRoom);
                var joinedAt = DateTime.UtcNow;

                await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                {
                    ChatRoomId = created.Id,
                    UserId = dto.CreatorId,
                    Role = ChatRoomRole.Owner,
                    JoinedAt = joinedAt
                });

                foreach (var memberId in initialMemberIds)
                {
                    await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                    {
                        ChatRoomId = created.Id,
                        UserId = memberId,
                        Role = ChatRoomRole.Member,
                        JoinedAt = joinedAt
                    });
                }

                var loaded = await _chatRoomRepository.GetByIdAsync(created.Id);
                _logger.LogInformation("Chat room {ChatRoomId} created with {ParticipantCount} participants", created.Id, participantIds.Count);
                return ChatResult.Success(loaded ?? created);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BAD CHAT CREATION EXCEPTION for creator {CreatorId}", dto.CreatorId);
            return ChatResult.Failure("Unable to create chat room.");
        }
    }

    public async Task<Result> AddMemberAsync(int chatRoomId, int actorUserId, int memberUserId, ChatRoomRole role = ChatRoomRole.Member)
    {
        var chatRoom = await _chatRoomRepository.GetByIdAsync(chatRoomId);
        if (chatRoom == null)
        {
            LogBad("MEMBER MANAGEMENT", "Chat room not found.");
            return Result.Failure("Chat room not found.");
        }

        if (chatRoom.Type == ChatRoomType.DirectMessage)
        {
            LogBad("MEMBER MANAGEMENT", "Cannot add members to a direct message chat.");
            return Result.Failure("Cannot add members to a direct message chat.");
        }

        var actor = chatRoom.Members.FirstOrDefault(m => m.UserId == actorUserId);
        if (actor == null)
        {
            LogBad("MEMBER MANAGEMENT", "Actor is not a member of the chat.");
            return Result.Failure("Actor is not a member of the chat.");
        }

        if (!IsOwnerOrAdmin(actor))
        {
            LogBad("MEMBER MANAGEMENT", "Only owners or admins can add members.");
            return Result.Failure("Only owners or admins can add members.");
        }

        if (chatRoom.Members.Any(m => m.UserId == memberUserId))
        {
            LogBad("MEMBER MANAGEMENT", "User is already a member.");
            return Result.Failure("User is already a member.");
        }

        if (role == ChatRoomRole.Owner)
        {
            LogBad("MEMBER MANAGEMENT", "Cannot assign ownership via this action.");
            return Result.Failure("Cannot assign ownership via this action.");
        }

        var user = await _userRepository.GetByIdAsync(memberUserId);
        if (user == null)
        {
            LogBad("MEMBER MANAGEMENT", "User not found.");
            return Result.Failure("User not found.");
        }

        try
        {
            await _transactionRunner.RunAsync(async () =>
            {
                await _chatRoomRepository.AddMemberAsync(new ChatRoomMember
                {
                    ChatRoomId = chatRoomId,
                    UserId = memberUserId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow
                });
            });

            _logger.LogInformation("Member {MemberId} added to chat {ChatRoomId} as {Role}", memberUserId, chatRoomId, role);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BAD MEMBER ADDITION for member {MemberId} in chat {ChatRoomId}", memberUserId, chatRoomId);
            return Result.Failure("Unable to add the member.");
        }
    }

    public async Task<Result> RemoveMemberAsync(int chatRoomId, int actorUserId, int memberUserId)
    {
        var chatRoom = await _chatRoomRepository.GetByIdAsync(chatRoomId);
        if (chatRoom == null)
        {
            LogBad("MEMBER MANAGEMENT", "Chat room not found.");
            return Result.Failure("Chat room not found.");
        }

        var actor = chatRoom.Members.FirstOrDefault(m => m.UserId == actorUserId);
        if (actor == null)
        {
            LogBad("MEMBER MANAGEMENT", "Actor is not a member of the chat.");
            return Result.Failure("Actor is not a member of the chat.");
        }

        if (!IsOwnerOrAdmin(actor))
        {
            return Result.Failure("Only owners or admins can remove members.");
        }

        var target = chatRoom.Members.FirstOrDefault(m => m.UserId == memberUserId);
        if (target == null)
        {
            LogBad("MEMBER MANAGEMENT", "Target user is not part of the chat.");
            return Result.Failure("Target user is not part of the chat.");
        }

        if (target.Role == ChatRoomRole.Owner)
        {
            LogBad("MEMBER MANAGEMENT", "Owner cannot be removed.");
            return Result.Failure("Owner cannot be removed.");
        }

        try
        {
            await _transactionRunner.RunAsync(async () =>
            {
                var removed = await _chatRoomRepository.RemoveMemberAsync(chatRoomId, memberUserId);
                if (!removed)
                {
                    throw new InvalidOperationException("Member removal failed.");
                }
            });

            _logger.LogInformation("Member {MemberId} removed from chat {ChatRoomId}", memberUserId, chatRoomId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BAD MEMBER REMOVAL for member {MemberId} in chat {ChatRoomId}", memberUserId, chatRoomId);
            return Result.Failure("Unable to remove member.");
        }
    }

    public async Task<Result> UpdateMemberRoleAsync(int chatRoomId, int actorUserId, int memberUserId, ChatRoomRole newRole)
    {
        var chatRoom = await _chatRoomRepository.GetByIdAsync(chatRoomId);
        if (chatRoom == null)
        {
            LogBad("MEMBER MANAGEMENT", "Chat room not found.");
            return Result.Failure("Chat room not found.");
        }

        var actor = chatRoom.Members.FirstOrDefault(m => m.UserId == actorUserId);
        if (actor == null || actor.Role != ChatRoomRole.Owner)
        {
            LogBad("MEMBER MANAGEMENT", "Only the owner can change roles.");
            return Result.Failure("Only the owner can change roles.");
        }

        var target = chatRoom.Members.FirstOrDefault(m => m.UserId == memberUserId);
        if (target == null)
        {
            LogBad("MEMBER MANAGEMENT", "Target user is not part of the chat.");
            return Result.Failure("Target user is not part of the chat.");
        }

        if (target.Role == ChatRoomRole.Owner)
        {
            LogBad("MEMBER MANAGEMENT", "Owner role cannot be changed here.");
            return Result.Failure("Owner role cannot be changed here.");
        }

        if (newRole == ChatRoomRole.Owner)
        {
            LogBad("MEMBER MANAGEMENT", "Ownership must be transferred through a dedicated workflow.");
            return Result.Failure("Ownership must be transferred through a dedicated workflow.");
        }

        try
        {
            await _transactionRunner.RunAsync(async () =>
            {
                var updated = await _chatRoomRepository.UpdateMemberRoleAsync(chatRoomId, memberUserId, newRole);
                if (!updated)
                {
                    throw new InvalidOperationException("Role update failed.");
                }
            });

            _logger.LogInformation("Member {MemberId} role updated to {Role} in chat {ChatRoomId}", memberUserId, newRole, chatRoomId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BAD MEMBER ROLE UPDATE for member {MemberId} in chat {ChatRoomId}", memberUserId, chatRoomId);
            return Result.Failure("Unable to update member role.");
        }
    }

    private void LogBad(string area, string detail)
    {
        _logger.LogWarning("BAD {Area}: {Detail}", area.ToUpperInvariant(), detail.ToUpperInvariant());
    }

    private static bool IsOwnerOrAdmin(ChatRoomMember member)
        => member.Role == ChatRoomRole.Owner || member.Role == ChatRoomRole.Admin;
}
