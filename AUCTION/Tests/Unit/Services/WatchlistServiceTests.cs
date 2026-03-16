using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Services;
using FluentAssertions;
using Moq;

namespace AUCTION.Tests.Unit.Services;

public class WatchlistServiceTests
{
    private readonly Mock<IAuctionRepository>   _auctionRepo   = new();
    private readonly Mock<IWatchlistRepository> _watchlistRepo = new();
    private readonly Mock<IBidRepository>       _bidRepo       = new();
    private readonly Mock<IRedisService>        _redis         = new();

    private WatchlistService Build() => new(
        _auctionRepo.Object,
        _watchlistRepo.Object,
        _bidRepo.Object,
        _redis.Object);

    [Fact]
    public async Task Watch_NewEntry_Succeeds()
    {
        var auction = SeedData.LiveAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _watchlistRepo.Setup(r => r.GetAsync(SeedData.UserId2, 1)).ReturnsAsync((Watchlist?)null);
        _watchlistRepo.Setup(r => r.AddAsync(It.IsAny<Watchlist>())).Returns(Task.CompletedTask);
        _watchlistRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await svc.WatchAuctionAsync(1, SeedData.UserId2);

        result.Success.Should().BeTrue();
        _watchlistRepo.Verify(r => r.AddAsync(It.IsAny<Watchlist>()), Times.Once);
    }

    [Fact]
    public async Task Watch_AlreadyWatching_ReturnsConflict()
    {
        var auction = SeedData.LiveAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _watchlistRepo.Setup(r => r.GetAsync(SeedData.UserId2, 1))
                      .ReturnsAsync(new Watchlist { UserId = SeedData.UserId2, AuctionId = 1 });

        var result = await svc.WatchAuctionAsync(1, SeedData.UserId2);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Watch_EndedAuction_ReturnsBadRequest()
    {
        var auction = SeedData.EndedAuction(1);
        var svc     = Build();

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

        var result = await svc.WatchAuctionAsync(1, SeedData.UserId2);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Unwatch_ExistingEntry_Succeeds()
    {
        var entry = new Watchlist { Id = 1, UserId = SeedData.UserId2, AuctionId = 5 };
        var svc   = Build();

        _watchlistRepo.Setup(r => r.GetAsync(SeedData.UserId2, 5)).ReturnsAsync(entry);
        _watchlistRepo.Setup(r => r.RemoveAsync(entry)).Returns(Task.CompletedTask);
        _watchlistRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await svc.UnwatchAuctionAsync(5, SeedData.UserId2);

        result.Success.Should().BeTrue();
        _watchlistRepo.Verify(r => r.RemoveAsync(entry), Times.Once);
    }

    [Fact]
    public async Task Unwatch_EntryNotFound_Returns404()
    {
        var svc = Build();
        _watchlistRepo.Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>()))
                      .ReturnsAsync((Watchlist?)null);

        var result = await svc.UnwatchAuctionAsync(99, SeedData.UserId2);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }
}
