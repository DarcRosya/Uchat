using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Репозиторий для работы с групповыми чатами (ChatRoom и ChatRoomMember)
/// </summary>
public interface IChatRoomRepository
{
    Task<ChatRoom> CreateAsync(ChatRoom chatRoom);
    Task<ChatRoom?> GetByIdAsync(int id);
    Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(int userId);
    Task AddMemberAsync(ChatRoomMember member);
    Task<bool> RemoveMemberAsync(int chatRoomId, int userId);
    Task<bool> UpdateMemberRoleAsync(int chatRoomId, int userId, ChatRoomRole role);
}
