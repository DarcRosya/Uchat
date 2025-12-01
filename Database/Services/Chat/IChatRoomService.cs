using System.Threading.Tasks;
using Database.Entities;
using Database.Services.Shared;

namespace Database.Services.Chat;

/// <summary>
/// Service contract that encapsulates chat life-cycle operations and permission enforcement.
/// </summary>
public interface IChatRoomService
{
    Task<ChatResult> CreateChatAsync(CreateChatDto dto);

    Task<Result> AddMemberAsync(int chatRoomId, int actorUserId, int memberUserId, ChatRoomRole role = ChatRoomRole.Member);

    Task<Result> RemoveMemberAsync(int chatRoomId, int actorUserId, int memberUserId);

    Task<Result> UpdateMemberRoleAsync(int chatRoomId, int actorUserId, int memberUserId, ChatRoomRole newRole);
}
