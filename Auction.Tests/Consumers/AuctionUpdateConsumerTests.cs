using Xunit;
using Moq;
using MassTransit;
using Microsoft.Extensions.Logging;
using AUCTION.Consumers;
using AUCTION.Hubs;
using AUCTION.Messages;
using AUCTION.Data.Dto.Response;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;
public class AuctionUpdateConsumerTests
{
    private readonly Mock<IAuctionHubService> _hubMock = new();
    private readonly Mock<ILogger<AuctionUpdateConsumer>> _loggerMock = new();

    private readonly AuctionUpdateConsumer _consumer;

    public AuctionUpdateConsumerTests()
    {
        _consumer = new AuctionUpdateConsumer(
            _hubMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Consume_Should_Send_Data_To_All_Users()
    {
        // Arrange
        var users = new List<int> { 1, 2, 3 };

        var auction = new AuctionUpdateConsumerDto
        {
            Id = 10,
            ProductId = 100,
            ProductName = "Test Product",
            StartingPrice = 500,
            MinBidIncrement = 10,
            StartDate = TimeHelper.Now(),
            EndDate = TimeHelper.Now().AddMinutes(10),
            Status = "Active",
            productDescription = "desc",
            users = users
        };

        var message = new AuctionUpdated(auction);

        var contextMock = new Mock<ConsumeContext<AuctionUpdated>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        foreach (var user in users)
        {
            _hubMock.Verify(x =>
                x.SendAddObject(
                    user,
                    It.Is<object>(o =>
                        o.ToString().Contains("Test Product") &&
                        o.ToString().Contains("100")
                    )
                ),
                Times.Once
            );
        }
    }

    [Fact]
    public async Task Consume_Should_Not_Call_Hub_When_No_Users()
    {
        // Arrange
        var auction = new AuctionUpdateConsumerDto
        {
            Id = 10,
            ProductId = 100,
            ProductName = "Test Product",
            users = new List<int>() // empty
        };

        var message = new AuctionUpdated(auction);

        var contextMock = new Mock<ConsumeContext<AuctionUpdated>>();
        contextMock.Setup(x => x.Message).Returns(message);
        

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _hubMock.Verify(x =>
            x.SendAddObject(It.IsAny<int>(), It.IsAny<object>()),
            Times.Never
        );
    }
}

