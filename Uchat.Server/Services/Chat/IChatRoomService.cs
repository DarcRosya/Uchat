using System.Collections.Generic;
using System.Threading.Tasks;
using Uchat.Database.Entities;

namespace Uchat.Server.Services.Chat;

public interface IChatRoomService
{
    Task<List<ChatRoom>> GetUserChatsAsync(int userId);
    Task<ChatResult> GetChatDetailsAsync(int chatId, int userId);
    Task<ChatResult> CreateChatAsync(int creatorId, string name, ChatRoomType type, string? description, IEnumerable<int>? initialMemberIds);
    Task<ChatResult> AddMemberAsync(int chatId, int actorUserId, int memberUserId);
    Task<ChatResult> RemoveMemberAsync(int chatId, int actorUserId, int memberUserId);
    Task<ChatResult> IsUserInChatAsync(int userId, int chatId);
}
