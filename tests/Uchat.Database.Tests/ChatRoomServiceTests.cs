using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Database.Entities;
using Database.Repositories.Interfaces;
using Database.Services.Chat;
using Xunit;

namespace Database.Tests;

public class ChatRoomServiceTests
{
    [Fact]
    public async Task CreateChatAsync_WhenCreatorMissing_ReturnsFailure()
    {
        var chatRepo = new Mock<IChatRoomRepository>();
        var userRepo = new Mock<IUserRepository>();
        var logger = new Mock<ILogger<ChatRoomService>>();

        userRepo.Setup(repo => repo.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((User)null);

        var service = new ChatRoomService(
            chatRepo.Object,
            userRepo.Object,
            new TestTransactionRunner(),
            logger.Object);

        var result = await service.CreateChatAsync(new CreateChatDto
        {
            CreatorId = 1,
            Name = "Team",
            Type = ChatRoomType.Private
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("Creator not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task AddMemberAsync_WhenChatMissing_ReturnsFailure()
    {
        var chatRepo = new Mock<IChatRoomRepository>();
        var userRepo = new Mock<IUserRepository>();
        var logger = new Mock<ILogger<ChatRoomService>>();

        chatRepo.Setup(repo => repo.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((ChatRoom)null);

        var service = new ChatRoomService(
            chatRepo.Object,
            userRepo.Object,
            new TestTransactionRunner(),
            logger.Object);

        var result = await service.AddMemberAsync(1, 1, 2);

        Assert.False(result.IsSuccess);
        Assert.Equal("Chat room not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_WhenActorIsNotOwner_ReturnsFailure()
    {
        var chatRepo = new Mock<IChatRoomRepository>();
        var userRepo = new Mock<IUserRepository>();
        var logger = new Mock<ILogger<ChatRoomService>>();

        chatRepo.Setup(repo => repo.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new ChatRoom
            {
                Members = new List<ChatRoomMember>
                {
                    new() { UserId = 1, Role = ChatRoomRole.Member },
                    new() { UserId = 2, Role = ChatRoomRole.Member }
                }
            });

        var service = new ChatRoomService(
            chatRepo.Object,
            userRepo.Object,
            new TestTransactionRunner(),
            logger.Object);

        var result = await service.UpdateMemberRoleAsync(1, 1, 2, ChatRoomRole.Admin);

        Assert.False(result.IsSuccess);
        Assert.Equal("Only the owner can change roles.", result.ErrorMessage);
    }
}
