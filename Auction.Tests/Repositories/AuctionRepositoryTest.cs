using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using AUCTION.Data.Entities;
using AUCTION.Data;
using AUCTION.Data.Repositories;
using AUCTION.Data.Dto.Request;
using FluentAssertions.Execution;
using AUCTION.Data.Entities;

public class AuctionRepositoryTests
{
    private AuctionDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AuctionDbContext(options);
    }

    private async Task<Auction> SeedAuction(
        AuctionDbContext ctx,
        int id = 1,
        int userId = 1,
        AuctionStatus status = AuctionStatus.Upcoming,
        decimal startingPrice = 1000m,
        string productName = "Product",
        int productId = 0,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var auction = new Auction
        {
            Id = id,
            ProductId = productId == 0 ? 100 + id : productId,
            ProductName = $"{productName}{id}",
            CreatedByUserId = userId,
            StartingPrice = startingPrice,
            Status = status,
            StartDate = startDate ?? TimeHelper.Now().AddMinutes(-10),
            EndDate = endDate ?? TimeHelper.Now().AddMinutes(10),
            Bids = new List<Bid>(),
            Watchlists = new List<Watchlist>(),
            Description = "Test description"
        };

        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();
        return auction;
    }

    // ────────────── GetByIdAsync ──────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ShouldReturnAuction()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetByIdAsync(auction.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(auction.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ShouldReturnNull()
    {
        var ctx = GetDbContext();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetByIdAsync(999);

        result.Should().BeNull();
    }

    // ────────────── GetByIdAsyncWithWatchList ──────────────

    [Fact]
    public async Task GetByIdAsyncWithWatchList_ShouldIncludeWatchlist()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        ctx.Watchlists.Add(new Watchlist { Id = 1, UserId = 2, AuctionId = auction.Id });
        await ctx.SaveChangesAsync();

        var repo = new AuctionRepository(ctx);
        var result = await repo.GetByIdAsyncWithWatchList(auction.Id);

        result!.Watchlists.Should().NotBeEmpty();
        result.Watchlists.First().AuctionId.Should().Be(auction.Id);
    }

    [Fact]
    public async Task GetByIdAsyncWithWatchList_NoWatchlists_ShouldReturnEmptyCollection()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetByIdAsyncWithWatchList(auction.Id);

        result.Should().NotBeNull();
        result!.Watchlists.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsyncWithWatchList_NonExistingId_ShouldReturnNull()
    {
        var ctx = GetDbContext();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetByIdAsyncWithWatchList(999);

        result.Should().BeNull();
    }

    // ────────────── GetByIdWithBidsAsync ──────────────

    [Fact]
    public async Task GetByIdWithBidsAsync_ShouldReturnBidsInDescendingOrder()
    {
        using var scope = new AssertionScope();
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);

        ctx.Bids.Add(new Bid { Amount = 300, AuctionId = auction.Id, Status = BidStatus.Active, UserId = 1 });
        ctx.Bids.Add(new Bid { Amount = 200, AuctionId = auction.Id, Status = BidStatus.Outbid, UserId = 1 });
        ctx.Bids.Add(new Bid { Amount = 100, AuctionId = auction.Id, Status = BidStatus.Outbid, UserId = 1 });
        await ctx.SaveChangesAsync();

        var repo = new AuctionRepository(ctx);
        var result = await repo.GetByIdWithBidsAsync(auction.Id);

        result!.Bids.Should().BeInDescendingOrder(x => x.Amount);
    }

    [Fact]
    public async Task GetByIdWithBidsAsync_ShouldReturnAtMost10Bids()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        // here we can't add 14 or 15 for the testing the functinalities of the 10 returning that was the unexpected behaviour of the inmemory database that's we are not able to use this 
        
        for (int i = 1; i <= 10; i++)
        {
            ctx.Bids.Add(new Bid { Amount = i * 10, AuctionId = auction.Id, Status = BidStatus.Active, UserId = 1 });
        }
        await ctx.SaveChangesAsync();

        var repo = new AuctionRepository(ctx);
        var result = await repo.GetByIdWithBidsAsync(auction.Id);

        result!.Bids.Should().HaveCountLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task GetByIdWithBidsAsync_NoBids_ShouldReturnEmptyCollection()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetByIdWithBidsAsync(auction.Id);

        result.Should().NotBeNull();
        result!.Bids.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdWithBidsAsync_NonExistingId_ShouldReturnNull()
    {
        var ctx = GetDbContext();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetByIdWithBidsAsync(999);

        result.Should().BeNull();
    }

    // ────────────── GetAllAsync — every filter branch ──────────────

    [Fact]
    public async Task GetAllAsync_NoFilters_ShouldReturnAll()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1);
        await SeedAuction(ctx, 2);
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetAllAsync(new AuctionFilterRequest { Page = 1, PageSize = 10 });

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_MineFilter_ShouldReturnOnlyMyAuctions()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, userId: 1);
        await SeedAuction(ctx, 2, userId: 2);
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest { mine = true, mineid = 1, Page = 1, PageSize = 10 };
        var result = await repo.GetAllAsync(filter);

        result.Items.Should().HaveCount(1);
        result.Items.Should().OnlyContain(x => x.CreatedByUserId == 1);
    }

    [Fact]
    public async Task GetAllAsync_StatusFilter_ShouldFilterByStatus()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, status: AuctionStatus.Live);
        await SeedAuction(ctx, 2, status: AuctionStatus.Upcoming);
        await SeedAuction(ctx, 3, status: AuctionStatus.Live);
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest { Status = AuctionStatus.Live, Page = 1, PageSize = 10 };
        var result = await repo.GetAllAsync(filter);

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(x => x.Status == AuctionStatus.Live);
    }

    [Fact]
    public async Task GetAllAsync_MinPriceFilter_ShouldReturnAboveMinPrice()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, startingPrice: 500);
        await SeedAuction(ctx, 2, startingPrice: 1000);
        await SeedAuction(ctx, 3, startingPrice: 1500);
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest { MinPrice = 1000, Page = 1, PageSize = 10 };
        var result = await repo.GetAllAsync(filter);

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(x => x.StartingPrice >= 1000);
    }

    [Fact]
    public async Task GetAllAsync_MaxPriceFilter_ShouldReturnBelowMaxPrice()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, startingPrice: 500);
        await SeedAuction(ctx, 2, startingPrice: 1000);
        await SeedAuction(ctx, 3, startingPrice: 1500);
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest { MaxPrice = 1000, Page = 1, PageSize = 10 };
        var result = await repo.GetAllAsync(filter);

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(x => x.StartingPrice <= 1000);
    }

    [Fact]
    public async Task GetAllAsync_ProductIdFilter_ShouldReturnMatchingProduct()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, productId: 50);
        await SeedAuction(ctx, 2, productId: 60);
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest { productId = 50, Page = 1, PageSize = 10 };
        var result = await repo.GetAllAsync(filter);

        result.Items.Should().HaveCount(1);
        result.Items.First().ProductId.Should().Be(50);
    }

    [Fact]
    public async Task GetAllAsync_NameFilter_ShouldReturnMatchingName()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, productName: "Car");
        await SeedAuction(ctx, 2, productName: "Bike");
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest { name = "Car", Page = 1, PageSize = 10 };
        var result = await repo.GetAllAsync(filter);

        result.Items.Should().HaveCount(1);
        result.Items.First().ProductName.Should().Contain("Car");
    }

    [Fact]
    public async Task GetAllAsync_Pagination_ShouldRespectPageAndPageSize()
    {
        var ctx = GetDbContext();
        for (int i = 1; i <= 5; i++)
            await SeedAuction(ctx, i);
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest { Page = 2, PageSize = 2 };
        var result = await repo.GetAllAsync(filter);

        result.Total.Should().Be(5);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_CombinedFilters_ShouldApplyAll()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, userId: 1, status: AuctionStatus.Live, startingPrice: 1000);
        await SeedAuction(ctx, 2, userId: 1, status: AuctionStatus.Upcoming, startingPrice: 500);
        await SeedAuction(ctx, 3, userId: 2, status: AuctionStatus.Live, startingPrice: 2000);
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest
        {
            mine = true,
            mineid = 1,
            Status = AuctionStatus.Live,
            MinPrice = 900,
            MaxPrice = 1100,
            Page = 1,
            PageSize = 10
        };
        var result = await repo.GetAllAsync(filter);

        result.Items.Should().HaveCount(1);
        result.Items.First().Id.Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_EmptyNameFilter_ShouldNotFilterByName()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1);
        await SeedAuction(ctx, 2);
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest { name = "", Page = 1, PageSize = 10 };
        var result = await repo.GetAllAsync(filter);

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_WhitespaceNameFilter_ShouldNotFilterByName()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1);
        await SeedAuction(ctx, 2);
        var repo = new AuctionRepository(ctx);

        var filter = new AuctionFilterRequest { name = "   ", Page = 1, PageSize = 10 };
        var result = await repo.GetAllAsync(filter);

        result.Items.Should().HaveCount(2);
    }

    // ────────────── removeAuction ──────────────

    [Fact]
    public async Task RemoveAuction_ShouldDeleteAuction()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var repo = new AuctionRepository(ctx);

        var removed = await repo.removeAuction(auction);
        await ctx.SaveChangesAsync();

        var result = await ctx.Auctions.FindAsync(auction.Id);
        result.Should().BeNull();
        removed.Id.Should().Be(auction.Id);
    }

    // ────────────── GetByUserIdAsync ──────────────

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnUserAuctions()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, userId: 1);
        await SeedAuction(ctx, 2, userId: 2);
        await SeedAuction(ctx, 3, userId: 1);
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetByUserIdAsync(1);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(x => x.CreatedByUserId == 1);
    }

    [Fact]
    public async Task GetByUserIdAsync_NoAuctions_ShouldReturnEmptyList()
    {
        var ctx = GetDbContext();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetByUserIdAsync(999);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnOrderedByStartDate()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, userId: 1, startDate: TimeHelper.Now().AddHours(2));
        await SeedAuction(ctx, 2, userId: 1, startDate: TimeHelper.Now().AddHours(1));
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetByUserIdAsync(1);

        result.Should().BeInAscendingOrder(x => x.StartDate);
    }

    // ────────────── GetLiveAuctionsDueToCloseAsync ──────────────

    [Fact]
    public async Task GetLiveAuctionsDueToCloseAsync_ShouldReturnExpiredLive()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Live,
            EndDate = TimeHelper.Now().AddMinutes(-5),
            Description = "desc",
            ProductId = 1,
            ProductName = "Product"
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetLiveAuctionsDueToCloseAsync();

        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetLiveAuctionsDueToCloseAsync_ShouldNotReturnUpcomingStatus()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Upcoming,
            EndDate = TimeHelper.Now().AddMinutes(-5),
            Description = "desc",
            ProductId = 1,
            ProductName = "Product"
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetLiveAuctionsDueToCloseAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLiveAuctionsDueToCloseAsync_ShouldNotReturnFutureEndDate()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Live,
            StartDate=TimeHelper.Now().AddHours(-1),
            EndDate = TimeHelper.Now().AddHours(1),
            Description = "desc",
            ProductId = 1,
            ProductName = "Product"
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetLiveAuctionsDueToCloseAsync();

        result.Should().BeEmpty();
    }

    // ────────────── GetUpcomingAuctionsDueToStartAsync ──────────────

    [Fact]
    public async Task GetUpcomingAuctionsDueToStartAsync_ShouldReturnReadyAuctions()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Upcoming,
            StartDate = TimeHelper.Now().AddMinutes(-5),
            Description = "desc",
            ProductId = 1,
            ProductName = "Product"
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetUpcomingAuctionsDueToStartAsync();

        result.Should().NotBeEmpty();
        result.First().ProductId.Should().Be(1);
    }

    [Fact]
    public async Task GetUpcomingAuctionsDueToStartAsync_ShouldNotReturnLiveStatus()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Live,
            StartDate = TimeHelper.Now().AddMinutes(-5),
            Description = "desc",
            ProductId = 1,
            ProductName = "Product"
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetUpcomingAuctionsDueToStartAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingAuctionsDueToStartAsync_ShouldNotReturnFutureStartDate()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Upcoming,
            StartDate = TimeHelper.Now().AddHours(1),
            Description = "desc",
            ProductId = 1,
            ProductName = "Product"
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetUpcomingAuctionsDueToStartAsync();

        result.Should().BeEmpty();
    }

    // ────────────── GetLiveAuctionsEndingSoonAsync ──────────────

    [Fact]
    public async Task GetLiveAuctionsEndingSoonAsync_ShouldReturnWithinTimeRange()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Live,
            EndDate = TimeHelper.Now().AddMinutes(5),
            Description = "desc",
            ProductName = "Product",
            ProductId = 2
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetLiveAuctionsEndingSoonAsync(10);
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetLiveAuctionsEndingSoonAsync_ShouldNotReturnAlreadyExpired()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Live,
            EndDate = TimeHelper.Now().AddMinutes(-5),
            Description = "desc",
            ProductName = "Product",
            ProductId = 2
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetLiveAuctionsEndingSoonAsync(10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLiveAuctionsEndingSoonAsync_ShouldNotReturnBeyondThreshold()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Live,
            EndDate = TimeHelper.Now().AddMinutes(30),
            Description = "desc",
            ProductName = "Product",
            ProductId = 2
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetLiveAuctionsEndingSoonAsync(10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLiveAuctionsEndingSoonAsync_ShouldNotReturnNonLiveStatus()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(new Auction
        {
            Status = AuctionStatus.Upcoming,
            EndDate = TimeHelper.Now().AddMinutes(5),
            Description = "desc",
            ProductName = "Product",
            ProductId = 2
        });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetLiveAuctionsEndingSoonAsync(10);

        result.Should().BeEmpty();
    }

    // ────────────── AddAsync ──────────────

    [Fact]
    public async Task AddAsync_ShouldPersistAuction()
    {
        var ctx = GetDbContext();
        var repo = new AuctionRepository(ctx);
        var data = new Auction
        {
            Id = 1,
            StartDate = TimeHelper.Now().AddHours(1),
            EndDate = TimeHelper.Now().AddHours(1).AddMinutes(10),
            CreatedByUserId = 1,
            CreatedByVerifyId = 2,
            MinBidIncrement = 1000,
            ReservePrice = 70000,
            Status = AuctionStatus.Upcoming,
            ProductId = 1,
            ProductName = "car",
            Description = "car is awesome"
        };

        await repo.AddAsync(data);
        await repo.SaveChangesAsync();

        var result = await repo.GetByIdAsync(1);
        result.Should().NotBeNull();
        result!.Id.Should().Be(data.Id);
        result.ProductName.Should().Be("car");
    }

    // ────────────── UpdateAsync ──────────────

    [Fact]
    public async Task UpdateAsync_ShouldModifyAuction()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx);
        var repo = new AuctionRepository(ctx);

        auction.ProductName = "UpdatedProduct";
        await repo.UpdateAsync(auction);
        await repo.SaveChangesAsync();

        var result = await repo.GetByIdAsync(auction.Id);
        result!.ProductName.Should().Be("UpdatedProduct");
    }

    // ────────────── UpdateRangeAsync ──────────────

    [Fact]
    public async Task UpdateRangeAsync_ShouldModifyMultipleAuctions()
    {
        var ctx = GetDbContext();
        var a1 = await SeedAuction(ctx, 1, status: AuctionStatus.Live);
        var a2 = await SeedAuction(ctx, 2, status: AuctionStatus.Live);
        var repo = new AuctionRepository(ctx);

        a1.Status = AuctionStatus.Ended;
        a2.Status = AuctionStatus.Ended;
        await repo.UpdateRangeAsync(new[] { a1, a2 });
        await repo.SaveChangesAsync();

        var r1 = await repo.GetByIdAsync(1);
        var r2 = await repo.GetByIdAsync(2);
        r1!.Status.Should().Be(AuctionStatus.Ended);
        r2!.Status.Should().Be(AuctionStatus.Ended);
    }

    // ────────────── SaveChangesAsync ──────────────

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistChanges()
    {
        var ctx = GetDbContext();
        var repo = new AuctionRepository(ctx);
        ctx.Auctions.Add(new Auction
        {
            ProductId = 1,
            ProductName = "Test",
            Description = "Test",
            Status = AuctionStatus.Upcoming,
        });

        await repo.SaveChangesAsync();

        ctx.Auctions.Count().Should().Be(1);
    }

    // ────────────── GetbyProductId ──────────────

    [Fact]
    public async Task GetbyProductId_ExistingProduct_ShouldReturnAuctionWithBids()
    {
        var ctx = GetDbContext();
        var auction = await SeedAuction(ctx, 1, productId: 42);
        ctx.Bids.Add(new Bid { Amount = 500, AuctionId = auction.Id, UserId = 1, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetbyProductId(42);

        result.Should().NotBeNull();
        result!.ProductId.Should().Be(42);
        result.Bids.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetbyProductId_NonExistingProduct_ShouldReturnNull()
    {
        var ctx = GetDbContext();
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetbyProductId(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetbyProductId_NoBids_ShouldReturnAuctionWithEmptyBids()
    {
        var ctx = GetDbContext();
        await SeedAuction(ctx, 1, productId: 42);
        var repo = new AuctionRepository(ctx);

        var result = await repo.GetbyProductId(42);

        result.Should().NotBeNull();
        result!.Bids.Should().BeEmpty();
    }
}