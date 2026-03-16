using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using AUCTION.Services;
using FluentAssertions;
using Messaging.Contracts;

namespace AUCTION.Tests.Unit.Services;

public class AuctionServiceTests
{
    // ── Shared mocks ──────────────────────────────────────────────────────────
    private readonly Mock<IAuctionRepository>           _auctionRepo    = new();
    private readonly Mock<IBidRepository>               _bidRepo        = new();
    private readonly Mock<IWatchlistRepository>         _watchlistRepo  = new();
    private readonly Mock<IRedisService>                _redis          = new();
    private readonly Mock<IPublishEndpoint>             _publish        = new();
    private readonly Mock<IAuctionHubService>           _hub            = new();
    
    private readonly Mock<ILogger<AuctionService>>      _logger         = new();

    private AuctionService Build() => new(
        _auctionRepo.Object,
        _bidRepo.Object,
        _watchlistRepo.Object,
        _redis.Object,
        _publish.Object,
        _hub.Object,
        _logger.Object);

    // Helper: suppress publish/hub calls we don't care about in a given test
    private void AllowPublish()
        => _publish.Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

    private void AllowHub()
    {
        _hub.Setup(h => h.BroadcastAuctionStarted(It.IsAny<int>())).Returns(Task.CompletedTask);
        _hub.Setup(h => h.BroadcastAuctionClosed(It.IsAny<int>(), It.IsAny<object>())).Returns(Task.CompletedTask);
        _hub.Setup(h => h.BroadcastBidPlaced(It.IsAny<int>(), It.IsAny<object>())).Returns(Task.CompletedTask);
    }

    // ── CreateAuction ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAuction_ValidRequest_ReturnsCreated_AndPublishesEvent()
    {
        var svc = Build();
        AllowPublish();

        _auctionRepo.Setup(r => r.AddAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var req = new CreateAuctionRequest
        {
            ProductId       = SeedData.ProductId1,
            StartingPrice   = 200m,
            MinBidIncrement = 10m,
            StartDate       = DateTime.UtcNow.AddHours(1),
            EndDate         = DateTime.UtcNow.AddDays(1)
        };

        var result = await svc.CreateAuctionAsync(req, SeedData.UserId1, SeedData.VerifyId1);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.StartingPrice.Should().Be(200m);

        _auctionRepo.Verify(r => r.AddAsync(It.IsAny<Auction>()), Times.Once);
        _publish.Verify(p =>
            p.Publish(It.IsAny<AuctionCreated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAuction_StartDateInPast_Fails()
    {
        var svc = Build();
        var req = new CreateAuctionRequest
        {
            ProductId     = SeedData.ProductId1,
            StartingPrice = 100m,
            StartDate     = DateTime.UtcNow.AddHours(-1),   // past
            EndDate       = DateTime.UtcNow.AddDays(1)
        };

        var result = await svc.CreateAuctionAsync(req, SeedData.UserId1, SeedData.VerifyId1);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("future");
        _auctionRepo.Verify(r => r.AddAsync(It.IsAny<Auction>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuction_EndBeforeStart_Fails()
    {
        var svc = Build();
        var req = new CreateAuctionRequest
        {
            ProductId     = SeedData.ProductId1,
            StartingPrice = 100m,
            StartDate     = DateTime.UtcNow.AddDays(2),
            EndDate       = DateTime.UtcNow.AddDays(1)      // before start
        };

        var result = await svc.CreateAuctionAsync(req, SeedData.UserId1, SeedData.VerifyId1);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("after start");
    }

    [Fact]
    public async Task CreateAuction_NegativePrice_Fails()
    {
        var svc = Build();
        var req = new CreateAuctionRequest
        {
            ProductId     = SeedData.ProductId1,
            StartingPrice = -50m,
            StartDate     = DateTime.UtcNow.AddHours(1),
            EndDate       = DateTime.UtcNow.AddDays(1)
        };

        var result = await svc.CreateAuctionAsync(req, SeedData.UserId1, SeedData.VerifyId1);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("positive");
    }

    // ── CancelAuction ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAuction_ByOwner_Succeeds_AndPublishesEvent()
    {
        var auction = SeedData.UpcomingAuction(1);
        var svc     = Build();
        AllowPublish();
        AllowHub();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await svc.CancelAuctionAsync(1, SeedData.UserId1);

        result.Success.Should().BeTrue();
        _publish.Verify(p =>
            p.Publish(It.IsAny<AuctionCancelled>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAuction_ByNonOwner_ReturnsForbidden()
    {
        var auction = SeedData.UpcomingAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

        var result = await svc.CancelAuctionAsync(1, SeedData.UserId2);  // wrong user

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CancelAuction_WhenLive_ReturnsBadRequest()
    {
        var auction = SeedData.LiveAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

        var result = await svc.CancelAuctionAsync(1, SeedData.UserId1);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CancelAuction_NotFound_Returns404()
    {
        var svc = Build();
        _auctionRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Auction?)null);

        var result = await svc.CancelAuctionAsync(99, SeedData.UserId1);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    // ── UpdateAuction ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAuction_BeforeStart_NoBids_Succeeds()
    {
        var auction = SeedData.UpcomingAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _bidRepo.Setup(r => r.GetBidCountAsync(1)).ReturnsAsync(0);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await svc.UpdateAuctionAsync(1,
            new UpdateAuctionRequest { StartingPrice = 300m }, SeedData.UserId1);

        result.Success.Should().BeTrue();
        result.Data!.StartingPrice.Should().Be(300m);
    }

    [Fact]
    public async Task UpdateAuction_ChangePriceWithExistingBids_Fails()
    {
        var auction = SeedData.UpcomingAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _bidRepo.Setup(r => r.GetBidCountAsync(1)).ReturnsAsync(3);  // bids exist

        var result = await svc.UpdateAuctionAsync(1,
            new UpdateAuctionRequest { StartingPrice = 300m }, SeedData.UserId1);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateAuction_ByNonOwner_ReturnsForbidden()
    {
        var auction = SeedData.UpcomingAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

        var result = await svc.UpdateAuctionAsync(1,
            new UpdateAuctionRequest { StartingPrice = 300m }, SeedData.UserId2);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    // ── CloseAuction ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseAuction_WithHighestBid_SetsWinner_PublishesBothEvents()
    {
        var auction = SeedData.LiveAuction(1);
        var winBid  = SeedData.ActiveBid(1, SeedData.UserId2, 500m);
        winBid.Id   = 55;
        var svc     = Build();
        AllowPublish();
        AllowHub();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(winBid);
        _bidRepo.Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<Bid>>())).Returns(Task.CompletedTask);
        _bidRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _redis.Setup(r => r.DeleteAuctionCacheAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        var result = await svc.CloseAuctionAsync(1);

        result.Success.Should().BeTrue();
        result.Data!.WinnerUserId.Should().Be(SeedData.UserId2);
        result.Data.FinalPrice.Should().Be(500m);

        // Both AuctionClosed AND AuctionWinnerDeclared must fire
        _publish.Verify(p =>
            p.Publish(It.IsAny<AuctionClosed>(), It.IsAny<CancellationToken>()), Times.Once);
        _publish.Verify(p =>
            p.Publish(It.IsAny<AuctionWinnerDeclared>(), It.IsAny<CancellationToken>()), Times.Once);
        _redis.Verify(r => r.DeleteAuctionCacheAsync(1), Times.Once);
    }

    [Fact]
    public async Task CloseAuction_NoBids_ClosesWithNoWinner_NoWinnerDeclaredEvent()
    {
        var auction = SeedData.LiveAuction(1);
        var svc     = Build();
        AllowPublish();
        AllowHub();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((Bid?)null);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _redis.Setup(r => r.DeleteAuctionCacheAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        var result = await svc.CloseAuctionAsync(1);

        result.Success.Should().BeTrue();

        // AuctionClosed fires, but NOT AuctionWinnerDeclared
        _publish.Verify(p =>
            p.Publish(It.IsAny<AuctionClosed>(), It.IsAny<CancellationToken>()), Times.Once);
        _publish.Verify(p =>
            p.Publish(It.IsAny<AuctionWinnerDeclared>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloseAuction_ReserveNotMet_NoWinnerDeclared()
    {
        var auction = SeedData.LiveAuction(1);
        auction.ReservePrice = 1000m;                              // reserve = $1000
        var lowBid  = SeedData.ActiveBid(1, SeedData.UserId2, 200m);  // only $200
        var svc     = Build();
        AllowPublish();
        AllowHub();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(lowBid);
        _bidRepo.Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<Bid>>())).Returns(Task.CompletedTask);
        _bidRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _redis.Setup(r => r.DeleteAuctionCacheAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        var result = await svc.CloseAuctionAsync(1);

        result.Success.Should().BeTrue();
        _publish.Verify(p =>
            p.Publish(It.IsAny<AuctionWinnerDeclared>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloseAuction_WhenNotLive_ReturnsFail()
    {
        var auction = SeedData.UpcomingAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

        var result = await svc.CloseAuctionAsync(1);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    // ── StartAuction ──────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAuction_UpcomingAuction_Succeeds_PublishesAuctionStarted()
    {
        var auction = SeedData.UpcomingAuction(1);
        var svc     = Build();
        AllowPublish();
        AllowHub();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await svc.StartAuctionAsync(1);

        result.Success.Should().BeTrue();
        _publish.Verify(p =>
            p.Publish(It.IsAny<AuctionStarted>(), It.IsAny<CancellationToken>()), Times.Once);
        _hub.Verify(h => h.BroadcastAuctionStarted(1), Times.Once);
    }

    [Fact]
    public async Task StartAuction_AlreadyLive_ReturnsFail()
    {
        var auction = SeedData.LiveAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

        var result = await svc.StartAuctionAsync(1);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }
}
