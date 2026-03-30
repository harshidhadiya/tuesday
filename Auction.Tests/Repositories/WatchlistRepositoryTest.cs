using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using AUCTION.Data.Entities;
using AUCTION.Data;
using AUCTION.Data.Repositories;
using AUCTION.Data.Dto.Request;

public class WatchlistRepositoryTests
{
    private AuctionDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AuctionDbContext(options);
    }

    private async Task<Auction> SeedAuction(AuctionDbContext ctx, int id = 1,
        AuctionStatus status = AuctionStatus.Live, string name = "Product")
    {
        var auction = new Auction
        {
            Id = id,
            ProductId = 100 + id,
            ProductName = $"{name}{id}",
            Description = "Test",
            CreatedByUserId = 1,
            StartingPrice = 1000,
            Status = status,
            StartDate = TimeHelper.Now().AddMinutes(-10),
            EndDate = TimeHelper.Now().AddMinutes(10),
        };
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();
        return auction;
    }

    private async Task<Watchlist> SeedWatchlist(AuctionDbContext ctx, int userId, int auctionId, int id = 0)
    {
        var watchlist = new Watchlist
        {
            UserId = userId,
            AuctionId = auctionId,
        };
        if (id > 0) watchlist.Id = id;
        ctx.Watchlists.Add(watchlist);
        await ctx.SaveChangesAsync();
        return watchlist;
    }

    // ────────────── GetAsync ──────────────

    [Fact]
    public async Task GetAsync_ExistingEntry_ShouldReturnWatchlist()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        await SeedWatchlist(ctx, userId: 1, auctionId: auction.Id);
        var repo = new WatchlistRepository(ctx);

        var result = await repo.GetAsync(1, auction.Id);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(1);
        result.AuctionId.Should().Be(auction.Id);
    }

    [Fact]
    public async Task GetAsync_NonExistingEntry_ShouldReturnNull()
    {
        var ctx = GetDbContext();
        var repo = new WatchlistRepository(ctx);

        var result = await repo.GetAsync(999, 999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WrongUserId_ShouldReturnNull()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        await SeedWatchlist(ctx, userId: 1, auctionId: auction.Id);
        var repo = new WatchlistRepository(ctx);

        var result = await repo.GetAsync(2, auction.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WrongAuctionId_ShouldReturnNull()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        await SeedWatchlist(ctx, userId: 1, auctionId: auction.Id);
        var repo = new WatchlistRepository(ctx);

        var result = await repo.GetAsync(1, 999);

        result.Should().BeNull();
    }

    // ────────────── GetByUserIdAsync — all filter branches ──────────────

    [Fact]
    public async Task GetByUserIdAsync_NoFilters_ShouldReturnUserWatchlist()
    {
        var ctx = GetDbContext();
        var a1 = await SeedAuction(ctx, 1);
        var a2 = await SeedAuction(ctx, 2);
        await SeedWatchlist(ctx, userId: 1, auctionId: a1.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a2.Id);
        await SeedWatchlist(ctx, userId: 2, auctionId: a1.Id);
        var repo = new WatchlistRepository(ctx);

        var filter = new WatchListFilterRequest { page = 1, size = 10 };
        var result = await repo.GetByUserIdAsync(1, filter);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_NameFilter_ShouldFilterByProductName()
    {
        var ctx = GetDbContext();
        var a1 = await SeedAuction(ctx, 1, name: "Car");
        var a2 = await SeedAuction(ctx, 2, name: "Bike");
        await SeedWatchlist(ctx, userId: 1, auctionId: a1.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a2.Id);
        var repo = new WatchlistRepository(ctx);

        var filter = new WatchListFilterRequest { name = "Car", page = 1, size = 10 };
        var result = await repo.GetByUserIdAsync(1, filter);

        result.Should().HaveCount(1);
        result.First().Auction.ProductName.Should().Contain("Car");
    }

    [Fact]
    public async Task GetByUserIdAsync_EmptyName_ShouldNotFilterByName()
    {
        var ctx = GetDbContext();
        var a1 = await SeedAuction(ctx, 1);
        var a2 = await SeedAuction(ctx, 2);
        await SeedWatchlist(ctx, userId: 1, auctionId: a1.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a2.Id);
        var repo = new WatchlistRepository(ctx);

        var filter = new WatchListFilterRequest { name = "", page = 1, size = 10 };
        var result = await repo.GetByUserIdAsync(1, filter);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_EndDateFilter_ShouldFilterByAuctionStartDate()
    {
        var ctx = GetDbContext();
        var now = TimeHelper.Now();
        var a1 = await SeedAuction(ctx, 1);
        a1.StartDate = now.AddDays(-3);
        ctx.Auctions.Update(a1);
        var a2 = await SeedAuction(ctx, 2);
        a2.StartDate = now.AddDays(3);
        ctx.Auctions.Update(a2);
        await ctx.SaveChangesAsync();

        await SeedWatchlist(ctx, userId: 1, auctionId: a1.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a2.Id);
        var repo = new WatchlistRepository(ctx);

        var filter = new WatchListFilterRequest { endDate = now, page = 1, size = 10 };
        var result = await repo.GetByUserIdAsync(1, filter);

        result.Should().HaveCount(1);
        result.First().AuctionId.Should().Be(a1.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_StatusFilter_ShouldFilterByAuctionStatus()
    {
        var ctx = GetDbContext();
        var a1 = await SeedAuction(ctx, 1, status: AuctionStatus.Live);
        var a2 = await SeedAuction(ctx, 2, status: AuctionStatus.Ended);
        await SeedWatchlist(ctx, userId: 1, auctionId: a1.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a2.Id);
        var repo = new WatchlistRepository(ctx);

        var filter = new WatchListFilterRequest { status = AuctionStatus.Live, page = 1, size = 10 };
        var result = await repo.GetByUserIdAsync(1, filter);

        result.Should().HaveCount(1);
        result.First().Auction.Status.Should().Be(AuctionStatus.Live);
    }

    [Fact]
    public async Task GetByUserIdAsync_IsDashBoardPage_ShouldReturnLiveAndUpcomingOnly()
    {
        var ctx = GetDbContext();
        var a1 = await SeedAuction(ctx, 1, status: AuctionStatus.Live);
        var a2 = await SeedAuction(ctx, 2, status: AuctionStatus.Upcoming);
        var a3 = await SeedAuction(ctx, 3, status: AuctionStatus.Ended);
        var a4 = await SeedAuction(ctx, 4, status: AuctionStatus.Cancelled);
        await SeedWatchlist(ctx, userId: 1, auctionId: a1.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a2.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a3.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a4.Id);
        var repo = new WatchlistRepository(ctx);

        var filter = new WatchListFilterRequest { isdashBoardPage = true, page = 1, size = 10 };
        var result = await repo.GetByUserIdAsync(1, filter);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(x =>
            x.Auction.Status == AuctionStatus.Live || x.Auction.Status == AuctionStatus.Upcoming);
    }

    [Fact]
    public async Task GetByUserIdAsync_IsDashBoardPageFalse_ShouldReturnAllStatuses()
    {
        var ctx = GetDbContext();
        var a1 = await SeedAuction(ctx, 1, status: AuctionStatus.Live);
        var a2 = await SeedAuction(ctx, 2, status: AuctionStatus.Ended);
        await SeedWatchlist(ctx, userId: 1, auctionId: a1.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a2.Id);
        var repo = new WatchlistRepository(ctx);

        var filter = new WatchListFilterRequest { isdashBoardPage = false, page = 1, size = 10 };
        var result = await repo.GetByUserIdAsync(1, filter);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_Pagination_ShouldRespectPageAndSize()
    {
        var ctx = GetDbContext();
        for (int i = 1; i <= 5; i++)
        {
            var a = await SeedAuction(ctx, i);
            await SeedWatchlist(ctx, userId: 1, auctionId: a.Id);
        }
        var repo = new WatchlistRepository(ctx);

        var page1 = await repo.GetByUserIdAsync(1, new WatchListFilterRequest { page = 1, size = 2 });
        var page2 = await repo.GetByUserIdAsync(1, new WatchListFilterRequest { page = 2, size = 2 });

        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_NoWatchlists_ShouldReturnEmptyList()
    {
        var ctx = GetDbContext();
        var repo = new WatchlistRepository(ctx);

        var filter = new WatchListFilterRequest { page = 1, size = 10 };
        var result = await repo.GetByUserIdAsync(999, filter);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserIdAsync_CombinedFilters_ShouldApplyAll()
    {
        var ctx = GetDbContext();
        var a1 = await SeedAuction(ctx, 1, status: AuctionStatus.Live, name: "Car");
        var a2 = await SeedAuction(ctx, 2, status: AuctionStatus.Ended, name: "Bike");
        var a3 = await SeedAuction(ctx, 3, status: AuctionStatus.Live, name: "Truck");
        await SeedWatchlist(ctx, userId: 1, auctionId: a1.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a2.Id);
        await SeedWatchlist(ctx, userId: 1, auctionId: a3.Id);
        var repo = new WatchlistRepository(ctx);

        var filter = new WatchListFilterRequest
        {
            status = AuctionStatus.Live,
            name = "Car",
            page = 1,
            size = 10
        };
        var result = await repo.GetByUserIdAsync(1, filter);

        result.Should().HaveCount(1);
        result.First().Auction.ProductName.Should().Contain("Car");
    }

    // ────────────── GetWatcherCountAsync ──────────────

    [Fact]
    public async Task GetWatcherCountAsync_ShouldReturnCorrectCount()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        await SeedWatchlist(ctx, userId: 1, auctionId: auction.Id);
        await SeedWatchlist(ctx, userId: 2, auctionId: auction.Id);
        await SeedWatchlist(ctx, userId: 3, auctionId: auction.Id);
        var repo = new WatchlistRepository(ctx);

        var result = await repo.GetWatcherCountAsync(auction.Id);

        result.Should().Be(3);
    }

    [Fact]
    public async Task GetWatcherCountAsync_NoWatchers_ShouldReturnZero()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var repo = new WatchlistRepository(ctx);

        var result = await repo.GetWatcherCountAsync(auction.Id);

        result.Should().Be(0);
    }

    // ────────────── AddAsync ──────────────

    [Fact]
    public async Task AddAsync_ShouldPersistWatchlist()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var repo = new WatchlistRepository(ctx);

        var watchlist = new Watchlist { UserId = 1, AuctionId = auction.Id };
        await repo.AddAsync(watchlist);
        await repo.SaveChangesAsync();

        var result = await ctx.Watchlists.FirstOrDefaultAsync(x => x.UserId == 1 && x.AuctionId == auction.Id);
        result.Should().NotBeNull();
    }

    // ────────────── RemoveAsync ──────────────

    [Fact]
    public async Task RemoveAsync_ShouldDeleteWatchlist()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var watchlist = await SeedWatchlist(ctx, userId: 1, auctionId: auction.Id);
        var repo = new WatchlistRepository(ctx);

        await repo.RemoveAsync(watchlist);
        await repo.SaveChangesAsync();

        var result = await ctx.Watchlists.FindAsync(watchlist.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldNotAffectOtherWatchlists()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var w1 = await SeedWatchlist(ctx, userId: 1, auctionId: auction.Id);
        var w2 = await SeedWatchlist(ctx, userId: 2, auctionId: auction.Id);
        var repo = new WatchlistRepository(ctx);

        await repo.RemoveAsync(w1);
        await repo.SaveChangesAsync();

        var remaining = await ctx.Watchlists.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining.First().UserId.Should().Be(2);
    }

    // ────────────── SaveChangesAsync ──────────────

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistChanges()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var repo = new WatchlistRepository(ctx);

        ctx.Watchlists.Add(new Watchlist { UserId = 1, AuctionId = auction.Id });
        await repo.SaveChangesAsync();

        ctx.Watchlists.Count().Should().Be(1);
    }
}
