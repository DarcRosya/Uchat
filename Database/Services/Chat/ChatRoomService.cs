using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Database.Entities;
using Database.Repositories.Interfaces;
using Database.Services.Shared;

namespace Database.Services.Chat;

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
            return ChatResult.Failure("Creator must be specified.");
        }

        if (dto.Type != ChatRoomType.DirectMessage && string.IsNullOrWhiteSpace(dto.Name))
        {
            return ChatResult.Failure("Chat name is required for group chats.");
        }

        if (dto.Type == ChatRoomType.Topic && !dto.ParentChatRoomId.HasValue)
        {
            return ChatResult.Failure("Topic chats must reference a parent room.");
        }

        var creator = await _userRepository.GetByIdAsync(dto.CreatorId);
        if (creator == null)
        {
            return ChatResult.Failure("Creator not found.");
        }

        var initialMemberIds = dto.InitialMemberIds?
            .Where(id => id > 0 && id != dto.CreatorId)
            .Distinct()
            .ToList() ?? new List<int>();

        if (dto.Type == ChatRoomType.DirectMessage && initialMemberIds.Count != 1)
        {
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
            return ChatResult.Failure($"Users not found: {string.Join(", ", missingUsers)}");
        }

        if (dto.ParentChatRoomId.HasValue)
        {
            var parent = await _chatRoomRepository.GetByIdAsync(dto.ParentChatRoomId.Value);
            if (parent == null)
            {
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
                return ChatResult.Success(loaded ?? created);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat room for creator {CreatorId}", dto.CreatorId);
            return ChatResult.Failure("Unable to create chat room.");
        }
    }

    public async Task<Result> AddMemberAsync(int chatRoomId, int actorUserId, int memberUserId, ChatRoomRole role = ChatRoomRole.Member)
    {
        var chatRoom = await _chatRoomRepository.GetByIdAsync(chatRoomId);
        if (chatRoom == null)
        {
            return Result.Failure("Chat room not found.");
        }

        if (chatRoom.Type == ChatRoomType.DirectMessage)
        {
            return Result.Failure("Cannot add members to a direct message chat.");
        }

        var actor = chatRoom.Members.FirstOrDefault(m => m.UserId == actorUserId);
        if (actor == null)
        {
            return Result.Failure("Actor is not a member of the chat.");
        }

        if (!IsOwnerOrAdmin(actor))
        {
            return Result.Failure("Only owners or admins can add members.");
        }

        if (chatRoom.Members.Any(m => m.UserId == memberUserId))
        {
            return Result.Failure("User is already a member.");
        }

        if (role == ChatRoomRole.Owner)
        {
            return Result.Failure("Cannot assign ownership via this action.");
        }

        var user = await _userRepository.GetByIdAsync(memberUserId);
        if (user == null)
        {
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

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add member {MemberId} to chat {ChatRoomId}", memberUserId, chatRoomId);
            return Result.Failure("Unable to add the member.");
        }
    }

    public async Task<Result> RemoveMemberAsync(int chatRoomId, int actorUserId, int memberUserId)
    {
        var chatRoom = await _chatRoomRepository.GetByIdAsync(chatRoomId);
        if (chatRoom == null)
        {
            return Result.Failure("Chat room not found.");
        }

        var actor = chatRoom.Members.FirstOrDefault(m => m.UserId == actorUserId);
        if (actor == null)
        {
            return Result.Failure("Actor is not a member of the chat.");
        }

        if (!IsOwnerOrAdmin(actor))
        {
            return Result.Failure("Only owners or admins can remove members.");
        }

        var target = chatRoom.Members.FirstOrDefault(m => m.UserId == memberUserId);
        if (target == null)
        {
            return Result.Failure("Target user is not part of the chat.");
        }

        if (target.Role == ChatRoomRole.Owner)
        {
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

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove member {MemberId} from chat {ChatRoomId}", memberUserId, chatRoomId);
            return Result.Failure("Unable to remove member.");
        }
    }

    public async Task<Result> UpdateMemberRoleAsync(int chatRoomId, int actorUserId, int memberUserId, ChatRoomRole newRole)
    {
        var chatRoom = await _chatRoomRepository.GetByIdAsync(chatRoomId);
        if (chatRoom == null)
        {
            return Result.Failure("Chat room not found.");
        }

        var actor = chatRoom.Members.FirstOrDefault(m => m.UserId == actorUserId);
        if (actor == null || actor.Role != ChatRoomRole.Owner)
        {
            return Result.Failure("Only the owner can change roles.");
        }

        var target = chatRoom.Members.FirstOrDefault(m => m.UserId == memberUserId);
        if (target == null)
        {
            return Result.Failure("Target user is not part of the chat.");
        }

        if (target.Role == ChatRoomRole.Owner)
        {
            return Result.Failure("Owner role cannot be changed here.");
        }

        if (newRole == ChatRoomRole.Owner)
        {
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

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update role for member {MemberId} in chat {ChatRoomId}", memberUserId, chatRoomId);
            return Result.Failure("Unable to update member role.");
        }
    }

    private static bool IsOwnerOrAdmin(ChatRoomMember member)
        => member.Role == ChatRoomRole.Owner || member.Role == ChatRoomRole.Admin;
}
