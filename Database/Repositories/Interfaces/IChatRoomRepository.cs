using System.Collections.Generic;
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

    Task<List<ChatRoomMember>> GetPendingMembershipsAsync(int userId);

    /// <summary>
    /// Persists a membership record with the supplied join metadata.
    /// </summary>
    Task AddMemberAsync(ChatRoomMember member);

    /// <summary>
    /// Includes chat members when the room is loaded.
    /// </summary>
    Task<ChatRoom?> GetByIdAsync(int id);
    
    /// <summary>
    /// Finds chat room by exact name (for system chats like "Global Chat").
    /// </summary>
    Task<ChatRoom?> GetByNameAsync(string name);

    /// <summary>
    /// Loads rooms for a specific user (joins handled via the repository).
    /// </summary>
    Task<List<ChatRoomMember>> GetUserChatMembershipsAsync(int userId);

    Task UpdateAsync(ChatRoom chatRoom);

    Task<ChatRoomMember?> GetMemberAsync(int chatRoomId, int userId);

    Task UpdateMemberAsync(ChatRoomMember member);

    Task RemoveMemberEntityAsync(ChatRoomMember member);

    /// <summary>
    /// Removes a membership row from the chat (hard delete).
    /// </summary>
    Task<bool> RemoveMemberAsync(int chatRoomId, int userId);

    /// <summary>
    /// Loads chat rooms by their identifiers (includes members).
    /// </summary>
    Task<List<ChatRoomMember>> GetMembersForUserByChatIdsAsync(int userId, IEnumerable<int> chatIds);
}
