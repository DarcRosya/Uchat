using System.Collections.Generic;
using System.Threading.Tasks;
using Uchat.Database.Entities;
using Uchat.Shared.DTOs;

namespace Uchat.Server.Services.Chat;

public interface IChatRoomService
{
    Task<ChatResult> CreateChatAsync(int creatorId, string name, ChatRoomType type, IEnumerable<int>? initialMemberIds);

    Task<List<ChatRoomDto>> GetPendingGroupInvitesAsync(int userId);
    Task<List<ChatRoomDto>> GetUserChatsAsync(int userId);
    Task<ChatResult> GetChatDetailsAsync(int chatId, int userId);

    Task<ChatResult> AcceptInviteAsync(int chatId, int userId);
    Task<ChatResult> RejectInviteAsync(int chatId, int userId);
    Task<ChatResult> JoinPublicChatByNameAsync(string chatName, int userId);
    Task<ChatResult> AddMemberAsync(int chatId, int actorUserId, int memberUserId);
    Task<ChatResult> SetGroupPinAsync(int userId, int chatRoomId, bool isPinned);
    
    Task<ChatResult> RemoveMemberAsync(int chatId, int actorUserId, int memberUserId);

    Task<ChatResult> IsUserInChatAsync(int userId, int chatId);
}
