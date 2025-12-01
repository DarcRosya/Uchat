using System.Threading.Tasks;
using Uchat.Database.Entities;
using Uchat.Server.Services.Shared;

namespace Uchat.Server.Services.Friendships;

/// <summary>
/// Encapsulates friendship workflows while enforcing actor validation.
/// </summary>
public interface IFriendshipService
{
    Task<Result<Friendship>> SendFriendRequestAsync(int senderId, int receiverId);

    Task<Result> AcceptRequestAsync(int friendshipId, int actorId);

    Task<Result> RejectRequestAsync(int friendshipId, int actorId);

    Task<Result> RemoveFriendshipAsync(int friendshipId, int actorId);
}
