using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Data access contract for friendship records.
/// </summary>
public interface IFriendshipRepository
{
    /// <summary>
    /// Inserts a friendship record with the supplied sender/receiver IDs.
    /// </summary>
    Task<Friendship> CreateRequestAsync(int senderId, int receiverId);

    /// <summary>
    /// Loads a record by its PK.
    /// </summary>
    Task<Friendship?> GetByIdAsync(int id);

    /// <summary>
    /// Searches for a record between two users regardless of direction.
    /// </summary>
    Task<Friendship?> GetBetweenAsync(int userA, int userB);

    /// <summary>
    /// Loads accepted friendships for a user.
    /// </summary>
    Task<IEnumerable<Friendship>> GetUserFriendshipsAsync(int userId);

    /// <summary>
    /// Loads pending inbound requests.
    /// </summary>
    Task<IEnumerable<Friendship>> GetPendingReceivedAsync(int userId);

    /// <summary>
    /// Marks a request as accepted and stamps AcceptedAt.
    /// </summary>
    Task<bool> AcceptRequestAsync(int friendshipId, int acceptedById);

    /// <summary>
    /// Marks a request as rejected.
    /// </summary>
    Task<bool> RejectRequestAsync(int friendshipId, int rejectedById);

    /// <summary>
    /// Deletes the friendship record.
    /// </summary>
    Task<bool> DeleteAsync(int friendshipId);

    /// <summary>
    /// Checks if a record exists in the specified direction.
    /// </summary>
    Task<bool> ExistsRequestAsync(int senderId, int receiverId);
}
