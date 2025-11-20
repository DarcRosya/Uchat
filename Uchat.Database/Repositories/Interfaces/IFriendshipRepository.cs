using Uchat.Database.Entities;

namespace Uchat.Database.Repositories.Interfaces;

/// <summary>
/// Репозиторий для управления дружбой (Friendship)
/// </summary>
public interface IFriendshipRepository
{
    Task<Friendship> CreateRequestAsync(int senderId, int receiverId);
    Task<Friendship?> GetByIdAsync(int id);
    Task<IEnumerable<Friendship>> GetUserFriendshipsAsync(int userId); // accepted
    Task<IEnumerable<Friendship>> GetPendingReceivedAsync(int userId); // pending where user is receiver
    Task<bool> AcceptRequestAsync(int friendshipId, int acceptedById);
    Task<bool> RejectRequestAsync(int friendshipId, int rejectedById);
    Task<bool> DeleteAsync(int friendshipId);
    Task<bool> ExistsRequestAsync(int senderId, int receiverId);
}
