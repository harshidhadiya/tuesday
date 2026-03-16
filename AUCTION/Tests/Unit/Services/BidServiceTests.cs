using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using AUCTION.Services;
using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Moq;

namespace AUCTION.Tests.Unit.Services;

public class BidServiceTests
{
    private readonly Mock<IAuctionRepository>      _auctionRepo = new();
    private readonly Mock<IBidRepository>          _bidRepo     = new();
    private readonly Mock<IRedisService>           _redis       = new();
    private readonly Mock<IPublishEndpoint>        _publish     = new();
    private readonly Mock<IAuctionHubService>      _hub         = new();
    private readonly Mock<ILogger<BidService>>     _logger      = new();

    private BidService Build() => new(
        _auctionRepo.Object,
        _bidRepo.Object,
        _redis.Object,
        _publish.Object,
        _hub.Object,
        _logger.Object);

    // ── Shared setup helpers ──────────────────────────────────────────────────

    private void LockGranted(int auctionId, int userId)
        => _redis.Setup(r => r.SetBidLockAsync(auctionId, userId, It.IsAny<TimeSpan>()))
                 .ReturnsAsync(true);

    private void LockDenied(int auctionId, int userId)
        => _redis.Setup(r => r.SetBidLockAsync(auctionId, userId, It.IsAny<TimeSpan>()))
                 .ReturnsAsync(false);

    private void ReleaseLock()
        => _redis.Setup(r => r.ReleaseBidLockAsync(It.IsAny<int>(), It.IsAny<int>()))
                 .Returns(Task.CompletedTask);

    private void AllowSaveAndCache()
    {
        _bidRepo.Setup(r => r.AddAsync(It.IsAny<Bid>())).Returns(Task.CompletedTask);
        _bidRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _redis.Setup(r => r.SetHighestBidAsync(It.IsAny<int>(), It.IsAny<HighestBidCacheDto>()))
              .Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _publish.Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        _hub.Setup(h => h.BroadcastBidPlaced(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceBid_FirstBid_AtStartingPrice_Succeeds()
    {
        var auction = SeedData.LiveAuction(1);
        auction.StartingPrice = 100m;
        var svc = Build();

        LockGranted(1, SeedData.UserId2);
        ReleaseLock();
        AllowSaveAndCache();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((HighestBidCacheDto?)null);

        var result = await svc.PlaceBidAsync(1,
            new PlaceBidRequest { Amount = 100m }, SeedData.UserId2, "127.0.0.1");

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.Amount.Should().Be(100m);

        _bidRepo.Verify(r => r.AddAsync(It.IsAny<Bid>()), Times.Once);
        _redis.Verify(r =>
            r.SetHighestBidAsync(1, It.IsAny<HighestBidCacheDto>()), Times.Once);
        _publish.Verify(p =>
            p.Publish(It.IsAny<AuctionBidPlaced>(), It.IsAny<CancellationToken>()), Times.Once);
        _hub.Verify(h => h.BroadcastBidPlaced(1, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task PlaceBid_OutbidsPrevious_MarksOldBidAsOutbid()
    {
        var auction  = SeedData.LiveAuction(1);
        var prevBid  = SeedData.ActiveBid(1, SeedData.UserId1, 100m);
        prevBid.Id   = 10;
        var svc      = Build();

        LockGranted(1, SeedData.UserId2);
        ReleaseLock();
        AllowSaveAndCache();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(new HighestBidCacheDto
        {
            BidId = 10, UserId = SeedData.UserId1, Amount = 100m, PlacedAt = DateTime.UtcNow
        });
        _bidRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(prevBid);
        _bidRepo.Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<Bid>>())).Returns(Task.CompletedTask);

        var result = await svc.PlaceBidAsync(1,
            new PlaceBidRequest { Amount = 120m }, SeedData.UserId2, null);

        result.Success.Should().BeTrue();
        _bidRepo.Verify(r => r.UpdateRangeAsync(
            It.Is<IEnumerable<Bid>>(bids =>
                bids.Any(b => b.Status == BidStatus.Outbid))), Times.Once);
    }

    // ── Validation failures ───────────────────────────────────────────────────

    [Fact]
    public async Task PlaceBid_BelowMinimumIncrement_Fails()
    {
        var auction = SeedData.LiveAuction(1);
        auction.MinBidIncrement = 10m;
        var svc = Build();

        LockGranted(1, SeedData.UserId2);
        ReleaseLock();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(new HighestBidCacheDto
        {
            BidId = 5, UserId = SeedData.UserId1, Amount = 100m, PlacedAt = DateTime.UtcNow
        });

        // 105 is less than 100 + 10 = 110
        var result = await svc.PlaceBidAsync(1,
            new PlaceBidRequest { Amount = 105m }, SeedData.UserId2, null);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("at least");
        _bidRepo.Verify(r => r.AddAsync(It.IsAny<Bid>()), Times.Never);
    }

    [Fact]
    public async Task PlaceBid_ByAuctionCreator_ReturnsForbidden()
    {
        var auction = SeedData.LiveAuction(1);  // CreatedByUserId = UserId1
        var svc = Build();

        LockGranted(1, SeedData.UserId1);
        ReleaseLock();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((HighestBidCacheDto?)null);

        var result = await svc.PlaceBidAsync(1,
            new PlaceBidRequest { Amount = 150m }, SeedData.UserId1, null);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task PlaceBid_AuctionNotLive_ReturnsBadRequest()
    {
        var auction = SeedData.UpcomingAuction(1);
        var svc = Build();

        LockGranted(1, SeedData.UserId2);
        ReleaseLock();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

        var result = await svc.PlaceBidAsync(1,
            new PlaceBidRequest { Amount = 150m }, SeedData.UserId2, null);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("live");
    }

    [Fact]
    public async Task PlaceBid_AuctionNotFound_Returns404()
    {
        var svc = Build();

        LockGranted(1, SeedData.UserId2);
        ReleaseLock();
        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Auction?)null);

        var result = await svc.PlaceBidAsync(1,
            new PlaceBidRequest { Amount = 150m }, SeedData.UserId2, null);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task PlaceBid_LockAlreadyHeld_ReturnsBadRequest_NeverHitsDb()
    {
        var svc = Build();

        LockDenied(1, SeedData.UserId2);  // concurrent request
        ReleaseLock();

        var result = await svc.PlaceBidAsync(1,
            new PlaceBidRequest { Amount = 150m }, SeedData.UserId2, null);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("wait");
        _auctionRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    // ── Auto-extend ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceBid_InLastTwoMinutes_AutoExtendsEndDate()
    {
        var auction     = SeedData.LiveAuction(1);
        auction.EndDate = DateTime.UtcNow.AddMinutes(1);   // only 1 min left
        var originalEnd = auction.EndDate;
        var svc         = Build();

        LockGranted(1, SeedData.UserId2);
        ReleaseLock();
        AllowSaveAndCache();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((HighestBidCacheDto?)null);

        await svc.PlaceBidAsync(1,
            new PlaceBidRequest { Amount = 100m }, SeedData.UserId2, null);

        // EndDate should have been extended
        _auctionRepo.Verify(r => r.UpdateAsync(
            It.Is<Auction>(a => a.EndDate > originalEnd)), Times.Once);
    }

    // ── Cache miss fallback ───────────────────────────────────────────────────

    [Fact]
    public async Task GetHighestBid_CacheMiss_FallsBackToDb_AndRepopulatesCache()
    {
        var auction = SeedData.LiveAuction(1);
        var dbBid   = SeedData.ActiveBid(1, SeedData.UserId2, 300m);
        dbBid.Id    = 20;
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((HighestBidCacheDto?)null);
        _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(dbBid);
        _redis.Setup(r => r.SetHighestBidAsync(1, It.IsAny<HighestBidCacheDto>()))
              .Returns(Task.CompletedTask);

        var result = await svc.GetHighestBidAsync(1);

        result.Success.Should().BeTrue();
        result.Data!.Amount.Should().Be(300m);
        _redis.Verify(r =>
            r.SetHighestBidAsync(1, It.IsAny<HighestBidCacheDto>()), Times.Once);
    }

    // ── GetMyBids ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyBids_CurrentlyWinning_FlagSetCorrectly()
    {
        var auction = SeedData.LiveAuction(1);
        var myBid   = SeedData.ActiveBid(1, SeedData.UserId2, 200m);
        myBid.Id    = 30;
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _bidRepo.Setup(r => r.GetByUserAndAuctionAsync(SeedData.UserId2, 1))
                .ReturnsAsync(new List<Bid> { myBid });
        _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(new HighestBidCacheDto
        {
            BidId = 30, UserId = SeedData.UserId2, Amount = 200m, PlacedAt = DateTime.UtcNow
        });

        var result = await svc.GetMyBidsAsync(1, SeedData.UserId2);

        result.Success.Should().BeTrue();
        result.Data![0].IsCurrentlyWinning.Should().BeTrue();
    }
}
