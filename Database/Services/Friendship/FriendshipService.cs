using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Database.Entities;
using Database.Repositories.Interfaces;
using Database.Services.Shared;

namespace Database.Services.Friendships;

public sealed class FriendshipService : IFriendshipService
{
    private readonly IFriendshipRepository _friendshipRepository;
    private readonly IUserRepository _userRepository;
    private readonly IContactRepository _contactRepository;
    private readonly ITransactionRunner _transactionRunner;
    private readonly ILogger<FriendshipService> _logger;

    public FriendshipService(
        IFriendshipRepository friendshipRepository,
        IUserRepository userRepository,
        IContactRepository contactRepository,
        ITransactionRunner transactionRunner,
        ILogger<FriendshipService> logger)
    {
        _friendshipRepository = friendshipRepository;
        _userRepository = userRepository;
        _contactRepository = contactRepository;
        _transactionRunner = transactionRunner;
        _logger = logger;
    }

    public async Task<Result<Friendship>> SendFriendRequestAsync(int senderId, int receiverId)
    {
        if (senderId <= 0 || receiverId <= 0)
        {
            return Result<Friendship>.Failure("Both users must be specified.");
        }

        if (senderId == receiverId)
        {
            return Result<Friendship>.Failure("Cannot send a friend request to yourself.");
        }

        var sender = await _userRepository.GetByIdAsync(senderId);
        if (sender == null)
        {
            return Result<Friendship>.Failure("Sender not found.");
        }

        var receiver = await _userRepository.GetByIdAsync(receiverId);
        if (receiver == null)
        {
            return Result<Friendship>.Failure("Receiver not found.");
        }

        var existing = await _friendshipRepository.GetBetweenAsync(senderId, receiverId);
        if (existing != null)
        {
            if (existing.Status == FriendshipStatus.Pending)
            {
                return Result<Friendship>.Failure("Friend request is already pending.");
            }

            if (existing.Status == FriendshipStatus.Accepted)
            {
                return Result<Friendship>.Failure("Users are already friends.");
            }

            if (existing.Status == FriendshipStatus.Blocked)
            {
                return Result<Friendship>.Failure("Friendship is blocked.");
            }

            if (existing.Status == FriendshipStatus.Rejected)
            {
                try
                {
                    return await _transactionRunner.RunAsync(async () =>
                    {
                        await _friendshipRepository.DeleteAsync(existing.Id);
                        var recreated = await _friendshipRepository.CreateRequestAsync(senderId, receiverId);
                        return Result<Friendship>.Success(recreated);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to recreate rejected friendship between {Sender} and {Receiver}", senderId, receiverId);
                    return Result<Friendship>.Failure("Unable to send friend request at the moment.");
                }
            }
        }

        try
        {
            return await _transactionRunner.RunAsync(async () =>
            {
                var request = await _friendshipRepository.CreateRequestAsync(senderId, receiverId);
                return Result<Friendship>.Success(request);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create friend request from {Sender} to {Receiver}", senderId, receiverId);
            return Result<Friendship>.Failure("Unable to send friend request.");
        }
    }

    public async Task<Result> AcceptRequestAsync(int friendshipId, int actorId)
    {
        var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);
        if (friendship == null)
        {
            return Result.Failure("Friend request not found.");
        }

        if (friendship.ReceiverId != actorId)
        {
            return Result.Failure("Only the receiver can accept the request.");
        }

        if (friendship.Status != FriendshipStatus.Pending)
        {
            return Result.Failure("Only pending requests can be accepted.");
        }

        try
        {
            return await _transactionRunner.RunAsync(async () =>
            {
                var updated = await _friendshipRepository.AcceptRequestAsync(friendshipId, actorId);
                if (!updated)
                {
                    throw new InvalidOperationException("Failed to mark request as accepted.");
                }

                await _contactRepository.AddContactAsync(friendship.SenderId, friendship.ReceiverId);
                await _contactRepository.AddContactAsync(friendship.ReceiverId, friendship.SenderId);
                return Result.Success();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept friend request {FriendshipId}", friendshipId);
            return Result.Failure("Unable to accept the request.");
        }
    }

    public async Task<Result> RejectRequestAsync(int friendshipId, int actorId)
    {
        var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);
        if (friendship == null)
        {
            return Result.Failure("Friend request not found.");
        }

        if (friendship.ReceiverId != actorId)
        {
            return Result.Failure("Only the receiver can reject the request.");
        }

        if (friendship.Status != FriendshipStatus.Pending)
        {
            return Result.Failure("Only pending requests can be rejected.");
        }

        try
        {
            return await _transactionRunner.RunAsync(async () =>
            {
                var updated = await _friendshipRepository.RejectRequestAsync(friendshipId, actorId);
                if (!updated)
                {
                    throw new InvalidOperationException("Failed to mark request as rejected.");
                }

                return Result.Success();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject friend request {FriendshipId}", friendshipId);
            return Result.Failure("Unable to reject the request.");
        }
    }

    public async Task<Result> RemoveFriendshipAsync(int friendshipId, int actorId)
    {
        var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);
        if (friendship == null)
        {
            return Result.Failure("Friendship not found.");
        }

        if (actorId != friendship.SenderId && actorId != friendship.ReceiverId)
        {
            return Result.Failure("Only participants can remove a friendship.");
        }

        if (friendship.Status != FriendshipStatus.Accepted)
        {
            return Result.Failure("Only accepted friendships can be removed.");
        }

        try
        {
            return await _transactionRunner.RunAsync(async () =>
            {
                var deleted = await _friendshipRepository.DeleteAsync(friendshipId);
                if (!deleted)
                {
                    throw new InvalidOperationException("Failed to delete friendship record.");
                }

                await _contactRepository.RemoveContactAsync(friendship.SenderId, friendship.ReceiverId);
                await _contactRepository.RemoveContactAsync(friendship.ReceiverId, friendship.SenderId);
                return Result.Success();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove friendship {FriendshipId}", friendshipId);
            return Result.Failure("Unable to remove friendship.");
        }
    }
}
