using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Uchat.Database.Entities;
using Uchat.Database.Repositories.Interfaces;
using Uchat.Database.Services.Friendships;
using Xunit;

namespace Uchat.Database.Tests;

public class FriendshipServiceTests
{
    [Fact]
    public async Task SendFriendRequestAsync_WhenUsersMatch_ReturnsFailure()
    {
        var friendshipRepo = new Mock<IFriendshipRepository>();
        var userRepo = new Mock<IUserRepository>();
        var contactRepo = new Mock<IContactRepository>();
        var logger = new Mock<ILogger<FriendshipService>>();

        var service = new FriendshipService(
            friendshipRepo.Object,
            userRepo.Object,
            contactRepo.Object,
            new TestTransactionRunner(),
            logger.Object);

        var result = await service.SendFriendRequestAsync(5, 5);

        Assert.False(result.IsSuccess);
        Assert.Equal("Cannot send a friend request to yourself.", result.ErrorMessage);
    }

    [Fact]
    public async Task AcceptRequestAsync_WhenActorIsNotReceiver_ReturnsFailure()
    {
        var friendshipRepo = new Mock<IFriendshipRepository>();
        var userRepo = new Mock<IUserRepository>();
        var contactRepo = new Mock<IContactRepository>();
        var logger = new Mock<ILogger<FriendshipService>>();

        friendshipRepo.Setup(repo => repo.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new Friendship { SenderId = 1, ReceiverId = 2, Status = FriendshipStatus.Pending });

        var service = new FriendshipService(
            friendshipRepo.Object,
            userRepo.Object,
            contactRepo.Object,
            new TestTransactionRunner(),
            logger.Object);

        var result = await service.AcceptRequestAsync(1, 3);

        Assert.False(result.IsSuccess);
        Assert.Equal("Only the receiver can accept the request.", result.ErrorMessage);
    }
}
