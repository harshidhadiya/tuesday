using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories;
using FluentAssertions;

namespace AUCTION.Tests.Unit.Repositories;

public class AuctionRepositoryTests
{
    [Fact]
    public async Task Add_ThenGetById_ReturnsCorrectAuction()
    {
        using var ctx  = TestDbFactory.Create();
        var repo       = new AuctionRepository(ctx);
        var auction    = SeedData.UpcomingAuction();

        await repo.AddAsync(auction);
        await repo.SaveChangesAsync();

        var found = await repo.GetByIdAsync(auction.Id);

        found.Should().NotBeNull();
        found!.StartingPrice.Should().Be(100m);
    }

    [Fact]
    public async Task GetAll_FilterByStatus_ReturnsOnlyMatchingRows()
    {
        using var ctx = TestDbFactory.Create();
        var repo      = new AuctionRepository(ctx);

        await repo.AddAsync(SeedData.UpcomingAuction());
        await repo.AddAsync(SeedData.LiveAuction());
        await repo.AddAsync(SeedData.LiveAuction());
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetAllAsync(new AuctionFilterRequest
        {
            Status = AuctionStatus.Live, Page = 1, PageSize = 10
        });

        total.Should().Be(2);
        items.Should().AllSatisfy(a => a.Status.Should().Be(AuctionStatus.Live));
    }

    [Fact]
    public async Task GetAll_FilterByPriceRange_ReturnsCorrectRows()
    {
        using var ctx = TestDbFactory.Create();
        var repo      = new AuctionRepository(ctx);

        var cheap     = SeedData.UpcomingAuction();
        cheap.StartingPrice = 50m;
        var expensive = SeedData.UpcomingAuction();
        expensive.StartingPrice = 500m;

        await repo.AddAsync(cheap);
        await repo.AddAsync(expensive);
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetAllAsync(new AuctionFilterRequest
        {
            MinPrice = 100m, MaxPrice = 999m, Page = 1, PageSize = 10
        });

        total.Should().Be(1);
        items[0].StartingPrice.Should().Be(500m);
    }

    [Fact]
    public async Task GetAll_Pagination_ReturnsCorrectPage()
    {
        using var ctx = TestDbFactory.Create();
        var repo      = new AuctionRepository(ctx);

        for (int i = 0; i < 15; i++)
            await repo.AddAsync(SeedData.UpcomingAuction());
        await repo.SaveChangesAsync();

        var (page1, total) = await repo.GetAllAsync(new AuctionFilterRequest { Page = 1, PageSize = 5 });
        var (page2, _)     = await repo.GetAllAsync(new AuctionFilterRequest { Page = 2, PageSize = 5 });

        total.Should().Be(15);
        page1.Should().HaveCount(5);
        page2.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetLiveAuctionsDueToClose_ReturnsOnlyExpiredLiveAuctions()
    {
        using var ctx = TestDbFactory.Create();
        var repo      = new AuctionRepository(ctx);

        var expired = SeedData.LiveAuction();
        expired.EndDate = DateTime.UtcNow.AddMinutes(-5);   // already past

        var stillLive = SeedData.LiveAuction();
        stillLive.EndDate = DateTime.UtcNow.AddHours(1);    // still running

        await repo.AddAsync(expired);
        await repo.AddAsync(stillLive);
        await repo.SaveChangesAsync();

        var due = await repo.GetLiveAuctionsDueToCloseAsync();

        due.Should().HaveCount(1);
        due[0].Id.Should().Be(expired.Id);
    }

    [Fact]
    public async Task GetUpcomingAuctionsDueToStart_ReturnsOnlyReadyAuctions()
    {
        using var ctx = TestDbFactory.Create();
        var repo      = new AuctionRepository(ctx);

        var ready = SeedData.UpcomingAuction();
        ready.StartDate = DateTime.UtcNow.AddMinutes(-1);   // start time passed

        var notYet = SeedData.UpcomingAuction();
        notYet.StartDate = DateTime.UtcNow.AddHours(2);     // future

        await repo.AddAsync(ready);
        await repo.AddAsync(notYet);
        await repo.SaveChangesAsync();

        var due = await repo.GetUpcomingAuctionsDueToStartAsync();

        due.Should().HaveCount(1);
        due[0].Id.Should().Be(ready.Id);
    }

    [Fact]
    public async Task GetByUserId_ReturnsOnlyThatUsersAuctions()
    {
        using var ctx = TestDbFactory.Create();
        var repo      = new AuctionRepository(ctx);

        var mine   = SeedData.UpcomingAuction();
        var theirs = SeedData.UpcomingAuction();
        theirs.CreatedByUserId = SeedData.UserId2;

        await repo.AddAsync(mine);
        await repo.AddAsync(theirs);
        await repo.SaveChangesAsync();

        var result = await repo.GetByUserIdAsync(SeedData.UserId1);

        result.Should().HaveCount(1);
        result[0].CreatedByUserId.Should().Be(SeedData.UserId1);
    }
}

public class BidRepositoryTests
{
    [Fact]
    public async Task GetHighestBid_ReturnsHighestActiveAmount()
    {
        using var ctx = TestDbFactory.Create();
        var auction = SeedData.LiveAuction();
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        var repo = new BidRepository(ctx);
        await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId1, 100m));
        await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId2, 250m));
        await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId1, 200m));
        await repo.SaveChangesAsync();

        var highest = await repo.GetHighestBidAsync(auction.Id);

        highest.Should().NotBeNull();
        highest!.Amount.Should().Be(250m);
    }

    [Fact]
    public async Task GetHighestBid_ExcludesOutbidBids()
    {
        using var ctx = TestDbFactory.Create();
        var auction = SeedData.LiveAuction();
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        var repo   = new BidRepository(ctx);
        var oldBid = SeedData.ActiveBid(auction.Id, SeedData.UserId1, 300m);
        oldBid.Status = BidStatus.Outbid;   // already outbid

        await repo.AddAsync(oldBid);
        await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId2, 200m));
        await repo.SaveChangesAsync();

        var highest = await repo.GetHighestBidAsync(auction.Id);

        highest!.Amount.Should().Be(200m);   // 300 is outbid, so 200 wins
    }

    [Fact]
    public async Task GetBidCount_ReturnsCorrectCount()
    {
        using var ctx = TestDbFactory.Create();
        var auction = SeedData.LiveAuction();
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        var repo = new BidRepository(ctx);
        await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId1, 100m));
        await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId2, 120m));
        await repo.SaveChangesAsync();

        var count = await repo.GetBidCountAsync(auction.Id);
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetByAuctionId_PaginatesCorrectly()
    {
        using var ctx = TestDbFactory.Create();
        var auction = SeedData.LiveAuction();
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        var repo = new BidRepository(ctx);
        for (int i = 0; i < 10; i++)
            await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId2, 100m + i));
        await repo.SaveChangesAsync();

        var page1 = await repo.GetByAuctionIdAsync(auction.Id, 1, 5);
        var page2 = await repo.GetByAuctionIdAsync(auction.Id, 2, 5);

        page1.Should().HaveCount(5);
        page2.Should().HaveCount(5);
        page1.Select(b => b.Id).Should().NotIntersectWith(page2.Select(b => b.Id));
    }

    [Fact]
    public async Task GetByUserAndAuction_ReturnsOnlyThatUsersBids()
    {
        using var ctx = TestDbFactory.Create();
        var auction = SeedData.LiveAuction();
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        var repo = new BidRepository(ctx);
        await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId1, 100m));
        await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId1, 120m));
        await repo.AddAsync(SeedData.ActiveBid(auction.Id, SeedData.UserId2, 200m));
        await repo.SaveChangesAsync();

        var bids = await repo.GetByUserAndAuctionAsync(SeedData.UserId1, auction.Id);

        bids.Should().HaveCount(2);
        bids.Should().AllSatisfy(b => b.UserId.Should().Be(SeedData.UserId1));
    }
}

public class WatchlistRepositoryTests
{
    [Fact]
    public async Task Add_ThenGet_ReturnsEntry()
    {
        using var ctx = TestDbFactory.Create();
        var auction = SeedData.LiveAuction();
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        var repo = new WatchlistRepository(ctx);
        await repo.AddAsync(new Watchlist { UserId = SeedData.UserId2, AuctionId = auction.Id });
        await repo.SaveChangesAsync();

        var entry = await repo.GetAsync(SeedData.UserId2, auction.Id);
        entry.Should().NotBeNull();
    }

    [Fact]
    public async Task GetWatcherCount_ReturnsCorrectNumber()
    {
        using var ctx = TestDbFactory.Create();
        var auction = SeedData.LiveAuction();
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        var repo = new WatchlistRepository(ctx);
        await repo.AddAsync(new Watchlist { UserId = SeedData.UserId1, AuctionId = auction.Id });
        await repo.AddAsync(new Watchlist { UserId = SeedData.UserId2, AuctionId = auction.Id });
        await repo.SaveChangesAsync();

        var count = await repo.GetWatcherCountAsync(auction.Id);
        count.Should().Be(2);
    }

    [Fact]
    public async Task Remove_ThenGet_ReturnsNull()
    {
        using var ctx = TestDbFactory.Create();
        var auction = SeedData.LiveAuction();
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        var repo  = new WatchlistRepository(ctx);
        var entry = new Watchlist { UserId = SeedData.UserId2, AuctionId = auction.Id };
        await repo.AddAsync(entry);
        await repo.SaveChangesAsync();

        await repo.RemoveAsync(entry);
        await repo.SaveChangesAsync();

        var found = await repo.GetAsync(SeedData.UserId2, auction.Id);
        found.Should().BeNull();
    }
}
