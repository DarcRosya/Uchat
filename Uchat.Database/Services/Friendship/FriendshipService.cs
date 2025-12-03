using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.Services.Shared;

namespace Uchat.Database.Services.Friendships;

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
                LogBad("FRIENDSHIP", "Both users must be specified.");
                return Result<Friendship>.Failure("Both users must be specified.");
        }

        if (senderId == receiverId)
        {
                LogBad("FRIENDSHIP", "Cannot send a friend request to yourself.");
                return Result<Friendship>.Failure("Cannot send a friend request to yourself.");
        }

        var sender = await _userRepository.GetByIdAsync(senderId);
        if (sender == null)
        {
                LogBad("FRIENDSHIP", "Sender not found.");
                return Result<Friendship>.Failure("Sender not found.");
        }

        var receiver = await _userRepository.GetByIdAsync(receiverId);
        if (receiver == null)
        {
                LogBad("FRIENDSHIP", "Receiver not found.");
                return Result<Friendship>.Failure("Receiver not found.");
        }

        var existing = await _friendshipRepository.GetBetweenAsync(senderId, receiverId);
        if (existing != null)
        {
            if (existing.Status == FriendshipStatus.Pending)
            {
                LogBad("FRIENDSHIP", "Friend request is already pending.");
                return Result<Friendship>.Failure("Friend request is already pending.");
            }

            if (existing.Status == FriendshipStatus.Accepted)
            {
                LogBad("FRIENDSHIP", "Users are already friends.");
                return Result<Friendship>.Failure("Users are already friends.");
            }

            if (existing.Status == FriendshipStatus.Blocked)
            {
                LogBad("FRIENDSHIP", "Friendship is blocked.");
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
                _logger.LogInformation("Friend request {FriendshipId} created from {Sender} to {Receiver}", request.Id, senderId, receiverId);
                return Result<Friendship>.Success(request);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create friend request from {Sender} to {Receiver}", senderId, receiverId);
            _logger.LogError(ex, "BAD FRIEND REQUEST CREATE from {Sender} to {Receiver}", senderId, receiverId);
            return Result<Friendship>.Failure("Unable to send friend request.");
        }
    }

    public async Task<Result> AcceptRequestAsync(int friendshipId, int actorId)
    {
        var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);
        if (friendship == null)
        {
                LogBad("FRIENDSHIP", "Friend request not found.");
                return Result.Failure("Friend request not found.");
        }

        if (friendship.ReceiverId != actorId)
        {
                LogBad("FRIENDSHIP", "Only the receiver can accept the request.");
                return Result.Failure("Only the receiver can accept the request.");
        }

        if (friendship.Status != FriendshipStatus.Pending)
        {
                LogBad("FRIENDSHIP", "Only pending requests can be accepted.");
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
                _logger.LogInformation("Friend request {FriendshipId} accepted", friendshipId);
                return Result.Success();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept friend request {FriendshipId}", friendshipId);
            _logger.LogError(ex, "BAD FRIEND REQUEST ACCEPT for {FriendshipId}", friendshipId);
            return Result.Failure("Unable to accept the request.");
        }
    }

    public async Task<Result> RejectRequestAsync(int friendshipId, int actorId)
    {
        var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);
        if (friendship == null)
        {
                LogBad("FRIENDSHIP", "Friend request not found.");
                return Result.Failure("Friend request not found.");
        }

        if (friendship.ReceiverId != actorId)
        {
                LogBad("FRIENDSHIP", "Only the receiver can reject the request.");
                return Result.Failure("Only the receiver can reject the request.");
        }

        if (friendship.Status != FriendshipStatus.Pending)
        {
                LogBad("FRIENDSHIP", "Only pending requests can be rejected.");
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
            _logger.LogError(ex, "BAD FRIEND REQUEST REJECT for {FriendshipId}", friendshipId);
            return Result.Failure("Unable to reject the request.");
        }
    }

    public async Task<Result> RemoveFriendshipAsync(int friendshipId, int actorId)
    {
        var friendship = await _friendshipRepository.GetByIdAsync(friendshipId);
        if (friendship == null)
        {
                LogBad("FRIENDSHIP", "Friendship not found.");
                return Result.Failure("Friendship not found.");
        }

        if (actorId != friendship.SenderId && actorId != friendship.ReceiverId)
        {
                LogBad("FRIENDSHIP", "Only participants can remove a friendship.");
                return Result.Failure("Only participants can remove a friendship.");
        }

        if (friendship.Status != FriendshipStatus.Accepted)
        {
                LogBad("FRIENDSHIP", "Only accepted friendships can be removed.");
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
                _logger.LogInformation("Friendship {FriendshipId} removed", friendshipId);
                return Result.Success();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove friendship {FriendshipId}", friendshipId);
            _logger.LogError(ex, "BAD FRIENDSHIP REMOVAL for {FriendshipId}", friendshipId);
            return Result.Failure("Unable to remove friendship.");
        }
    }

    private void LogBad(string area, string detail)
    {
        _logger.LogWarning("BAD {Area}: {Detail}", area.ToUpperInvariant(), detail.ToUpperInvariant());
    }
}
