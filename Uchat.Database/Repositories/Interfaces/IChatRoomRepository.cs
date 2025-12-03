using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Repository surface for CRUD operations over chat rooms and memberships.
/// </summary>
public interface IChatRoomRepository
{
    /// <summary>
    /// Persists a new chat room with the current UTC timestamp.
    /// </summary>
    Task<ChatRoom> CreateAsync(ChatRoom chatRoom);

    /// <summary>
    /// Persists a membership record with the supplied join metadata.
    /// </summary>
    Task AddMemberAsync(ChatRoomMember member);

    /// <summary>
    /// Includes chat members when the room is loaded.
    /// </summary>
    Task<ChatRoom?> GetByIdAsync(int id);

    /// <summary>
    /// Loads rooms for a specific user (joins handled via the repository).
    /// </summary>
    Task<IEnumerable<ChatRoom>> GetUserChatRoomsAsync(int userId);

    /// <summary>
    /// Updates the role for a particular membership row (returns false if not found).
    /// </summary>
    Task<bool> UpdateMemberRoleAsync(int chatRoomId, int userId, ChatRoomRole role);

    /// <summary>
    /// Removes a membership row from the chat (hard delete).
    /// </summary>
    Task<bool> RemoveMemberAsync(int chatRoomId, int userId);
}
