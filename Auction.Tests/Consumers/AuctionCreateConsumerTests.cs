using Xunit;
using Moq;
using MassTransit;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using AUCTION.Consumers;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using Messaging.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AUCTION.Data;

public class AuctionCreateConsumerTests
{
    private readonly Mock<IAuctionRepository> _repoMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<IUserHubService> _hubMock = new();
    private readonly Mock<IPublishEndpoint> _publishMock = new();
    private readonly Mock<ILogger<AuctionCreateConsumer>> _loggerMock = new();

    private readonly AuctionDbContext _ctx;
    private readonly AuctionCreateConsumer _consumer;

    public AuctionCreateConsumerTests()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // unique DB per test
            .Options;

        _ctx = new AuctionDbContext(options);

        _consumer = new AuctionCreateConsumer(
            _repoMock.Object,
            _mapperMock.Object,
            _hubMock.Object,
            _publishMock.Object,
            _loggerMock.Object,
            _ctx
        );
    }

    [Fact]
    public async Task Consume_Should_Update_Auction_When_Exists_And_Same_User()
    {
        var message = new AuctionCreatedFromVerifyService(
            ProductId: 1,
            ProductName: "Test",
            StartDate: TimeHelper.Now(),
            EndDate: TimeHelper.Now().AddDays(1),
            MinBidIncrement: 10,
            Description: "desc",
            verifierId: 2,
            userId: 5,
            ReservePrice: 100,
            StartingPrice: 50
        );

        var auction = new Auction
        {
            ProductId = 1,
            CreatedByUserId = 5,
            Bids = new List<Bid> { new Bid() }
        };

        _repoMock.Setup(x => x.GetbyProductId(1)).ReturnsAsync(auction);

        var contextMock = new Mock<ConsumeContext<AuctionCreatedFromVerifyService>>();
        contextMock.Setup(x => x.Message).Returns(message);

        await _consumer.Consume(contextMock.Object);

        Assert.Equal(AuctionStatus.Upcoming, auction.Status);
        Assert.Empty(auction.Bids);

        _repoMock.Verify(x => x.UpdateAsync(auction), Times.Once);
        _repoMock.Verify(x => x.SaveChangesAsync(), Times.Once);

        _publishMock.Verify(x =>
            x.Publish(It.IsAny<ProductAddAuctionDate>(), default),
            Times.Once
        );
    }

    [Fact]
    public async Task Consume_Should_Do_Nothing_When_User_Not_Match()
    {
        var message = new AuctionCreatedFromVerifyService(
            ProductId: 1,
            ProductName: "Test",
            StartDate: TimeHelper.Now(),
            EndDate: TimeHelper.Now().AddDays(1),
            MinBidIncrement: 10,
            Description: "desc",
            verifierId: 2,
            userId: 99,
            ReservePrice: 100,
            StartingPrice: 50
        );

        var auction = new Auction
        {
            ProductId = 1,
            CreatedByUserId = 5
        };

        _repoMock.Setup(x => x.GetbyProductId(1)).ReturnsAsync(auction);

        var contextMock = new Mock<ConsumeContext<AuctionCreatedFromVerifyService>>();
        contextMock.Setup(x => x.Message).Returns(message);

        await _consumer.Consume(contextMock.Object);

        _repoMock.Verify(x => x.UpdateAsync(It.IsAny<Auction>()), Times.Never);
        _repoMock.Verify(x => x.SaveChangesAsync(), Times.Never);
        _publishMock.Verify(x => x.Publish(It.IsAny<object>(), default), Times.Never);
    }

    [Fact]
    public async Task Consume_Should_Create_New_Auction_When_Not_Exists()
    {
        var message = new AuctionCreatedFromVerifyService(
            ProductId: 2,
            ProductName: "New Product",
            StartDate: TimeHelper.Now(),
            EndDate: TimeHelper.Now().AddDays(2),
            MinBidIncrement: 5,
            Description: "new",
            verifierId: 1,
            userId: 10,
            ReservePrice: 200,
            StartingPrice: 100
        );

        var mappedAuction = new Auction { ProductId = 2 };

        _repoMock.Setup(x => x.GetbyProductId(2)).ReturnsAsync((Auction?)null);
        _mapperMock.Setup(x => x.Map<Auction>(message)).Returns(mappedAuction);

        var contextMock = new Mock<ConsumeContext<AuctionCreatedFromVerifyService>>();
        contextMock.Setup(x => x.Message).Returns(message);

        await _consumer.Consume(contextMock.Object);

        _repoMock.Verify(x => x.AddAsync(mappedAuction), Times.Once);
        _repoMock.Verify(x => x.SaveChangesAsync(), Times.Once);

        _hubMock.Verify(x =>
            x.BroadCastCreatMessage(10, "UserCreated SuccessFully"),
            Times.Once
        );

        _publishMock.Verify(x =>
            x.Publish(It.IsAny<ProductAddAuctionDate>(), default),
            Times.Once
        );
    }
}
