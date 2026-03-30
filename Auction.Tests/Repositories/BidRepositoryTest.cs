using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using AUCTION.Data.Entities;
using AUCTION.Data;
using AUCTION.Data.Repositories;
using AUCTION.Data.Dto.Request;
using FluentAssertions.Execution;

public class BidRepositoryTests
{
    private AuctionDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AuctionDbContext(options);
    }

    private Auction CreateAuction(int id = 1, AuctionStatus status = AuctionStatus.Live,
        decimal startingPrice = 1000m, string name = "Product")
    {
        return new Auction
        {
            Id = id,
            ProductId = 100 + id,
            ProductName = $"{name}{id}",
            Description = "Test",
            CreatedByUserId = 1,
            StartingPrice = startingPrice,
            Status = status,
            StartDate = TimeHelper.Now().AddMinutes(-10),
            EndDate = TimeHelper.Now().AddMinutes(10),
        };
    }

    private async Task<(Auction auction, List<Bid> bids)> SeedAuctionWithBids(
        AuctionDbContext ctx, int auctionId = 1, int userId = 1, int bidCount = 3)
    {
        var auction = CreateAuction(auctionId);
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        var bids = new List<Bid>();
        for (int i = 1; i <= bidCount; i++)
        {
            var bid = new Bid
            {
                AuctionId = auction.Id,
                UserId = userId,
                Amount = 100 * i,
                Status = i == bidCount ? BidStatus.Active : BidStatus.Outbid,
                PlacedAt = TimeHelper.Now().AddMinutes(-bidCount + i)
            };
            ctx.Bids.Add(bid);
            bids.Add(bid);
        }
        await ctx.SaveChangesAsync();
        return (auction, bids);
    }

    // ────────────── GetByIdAsync ──────────────

    [Fact]
    public async Task GetByIdAsync_ExistingBid_ShouldReturnBid()
    {
        var ctx = GetDbContext();
        var (auction, bids) = await SeedAuctionWithBids(ctx);
        var repo = new BidRepository(ctx);

        var result = await repo.GetByIdAsync(bids.First().Id);

        result.Should().NotBeNull();
        result!.AuctionId.Should().Be(auction.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingBid_ShouldReturnNull()
    {
        var ctx = GetDbContext();
        var repo = new BidRepository(ctx);

        var result = await repo.GetByIdAsync(999);

        result.Should().BeNull();
    }

    // ────────────── GetByAuctionIdAsync ──────────────

    [Fact]
    public async Task GetByAuctionIdAsync_ShouldReturnBidsForAuction()
    {
        var ctx = GetDbContext();
        var (auction, _) = await SeedAuctionWithBids(ctx, bidCount: 5);
        var repo = new BidRepository(ctx);

        var result = await repo.GetByAuctionIdAsync(auction.Id, 1, 10, false, 0);

        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetByAuctionIdAsync_ShouldReturnOrderedByPlacedAtDescending()
    {
        var ctx = GetDbContext();
        var (auction, _) = await SeedAuctionWithBids(ctx, bidCount: 3);
        var repo = new BidRepository(ctx);

        var result = await repo.GetByAuctionIdAsync(auction.Id, 1, 10, false, 0);

        result.Should().BeInDescendingOrder(x => x.PlacedAt);
    }

    [Fact]
    public async Task GetByAuctionIdAsync_MineTrue_ShouldReturnOnlyUserBids()
    {
        var ctx = GetDbContext();
        var auction = CreateAuction(1);
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = auction.Id, UserId = 1, Amount = 100, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = auction.Id, UserId = 2, Amount = 200, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = auction.Id, UserId = 1, Amount = 300, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var result = await repo.GetByAuctionIdAsync(auction.Id, 1, 10, true, 1);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(x => x.UserId == 1);
    }

    [Fact]
    public async Task GetByAuctionIdAsync_MineFalse_ShouldReturnAllBids()
    {
        var ctx = GetDbContext();
        var auction = CreateAuction(1);
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = auction.Id, UserId = 1, Amount = 100, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = auction.Id, UserId = 2, Amount = 200, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var result = await repo.GetByAuctionIdAsync(auction.Id, 1, 10, false, 1);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByAuctionIdAsync_Pagination_ShouldRespectPageAndSize()
    {
        var ctx = GetDbContext();
        var (auction, _) = await SeedAuctionWithBids(ctx, bidCount: 5);
        var repo = new BidRepository(ctx);

        var page1 = await repo.GetByAuctionIdAsync(auction.Id, 1, 2, false, 0);
        var page2 = await repo.GetByAuctionIdAsync(auction.Id, 2, 2, false, 0);

        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByAuctionIdAsync_EmptyAuction_ShouldReturnEmptyList()
    {
        var ctx = GetDbContext();
        var auction = CreateAuction(1);
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var result = await repo.GetByAuctionIdAsync(auction.Id, 1, 10, false, 0);

        result.Should().BeEmpty();
    }

    // ────────────── GetParticipatedAuctionsAsync ──────────────

    [Fact]
    public async Task GetParticipatedAuctionsAsync_ShouldReturnAuctionsUserBidOn()
    {
        var ctx = GetDbContext();
        var a1 = CreateAuction(1);
        var a2 = CreateAuction(2);
        ctx.Auctions.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = a1.Id, UserId = 1, Amount = 100, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = a2.Id, UserId = 2, Amount = 200, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_WinTrue_ShouldReturnOnlyWonAuctions()
    {
        var ctx = GetDbContext();
        var a1 = CreateAuction(1);
        var a2 = CreateAuction(2);
        ctx.Auctions.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = a1.Id, UserId = 1, Amount = 100, Status = BidStatus.Won });
        ctx.Bids.Add(new Bid { AuctionId = a2.Id, UserId = 1, Amount = 200, Status = BidStatus.Lost });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { win = true, Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(1);
        result.Items.First().Id.Should().Be(a1.Id);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_WinFalse_ShouldReturnAllParticipatedAuctions()
    {
        var ctx = GetDbContext();
        var a1 = CreateAuction(1);
        var a2 = CreateAuction(2);
        ctx.Auctions.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = a1.Id, UserId = 1, Amount = 100, Status = BidStatus.Won });
        ctx.Bids.Add(new Bid { AuctionId = a2.Id, UserId = 1, Amount = 200, Status = BidStatus.Lost });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { win = false, Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_StatusFilter_ShouldFilterByStatus()
    {
        var ctx = GetDbContext();
        var a1 = CreateAuction(1, status: AuctionStatus.Live);
        var a2 = CreateAuction(2, status: AuctionStatus.Ended);
        ctx.Auctions.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = a1.Id, UserId = 1, Amount = 100, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = a2.Id, UserId = 1, Amount = 200, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { Status = AuctionStatus.Live, Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(1);
        result.Items.First().Status.Should().Be(AuctionStatus.Live);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_MinPriceFilter_ShouldFilterByMinPrice()
    {
        var ctx = GetDbContext();
        var a1 = CreateAuction(1, startingPrice: 500);
        var a2 = CreateAuction(2, startingPrice: 1500);
        ctx.Auctions.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = a1.Id, UserId = 1, Amount = 100, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = a2.Id, UserId = 1, Amount = 200, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { MinPrice = 1000, Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(1);
        result.Items.Should().OnlyContain(x => x.StartingPrice >= 1000);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_MaxPriceFilter_ShouldFilterByMaxPrice()
    {
        var ctx = GetDbContext();
        var a1 = CreateAuction(1, startingPrice: 500);
        var a2 = CreateAuction(2, startingPrice: 1500);
        ctx.Auctions.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = a1.Id, UserId = 1, Amount = 100, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = a2.Id, UserId = 1, Amount = 200, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { MaxPrice = 1000, Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(1);
        result.Items.Should().OnlyContain(x => x.StartingPrice <= 1000);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_NameFilter_ShouldFilterByName()
    {
        var ctx = GetDbContext();
        var a1 = CreateAuction(1, name: "Car");
        var a2 = CreateAuction(2, name: "Bike");
        ctx.Auctions.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = a1.Id, UserId = 1, Amount = 100, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = a2.Id, UserId = 1, Amount = 200, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { name = "Car", Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(1);
        result.Items.First().ProductName.Should().Contain("Car");
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_FilterStartDate_ShouldFilterByStartDate()
    {
        var ctx = GetDbContext();
        var now = TimeHelper.Now();
        var a1 = CreateAuction(1);
        a1.StartDate = now.AddDays(-2);
        var a2 = CreateAuction(2);
        a2.StartDate = now.AddDays(2);
        ctx.Auctions.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = a1.Id, UserId = 1, Amount = 100, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = a2.Id, UserId = 1, Amount = 200, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { FilterStartDate = now.AddDays(-1), Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(1);
        result.Items.First().Id.Should().Be(a2.Id);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_FilterEndDate_ShouldFilterByEndDate()
    {
        var ctx = GetDbContext();
        var now = TimeHelper.Now();
        var a1 = CreateAuction(1);
        a1.EndDate = now.AddDays(-2);
        var a2 = CreateAuction(2);
        a2.EndDate = now.AddDays(2);
        ctx.Auctions.AddRange(a1, a2);
        await ctx.SaveChangesAsync();

        ctx.Bids.Add(new Bid { AuctionId = a1.Id, UserId = 1, Amount = 100, Status = BidStatus.Active });
        ctx.Bids.Add(new Bid { AuctionId = a2.Id, UserId = 1, Amount = 200, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { FilterEndDate = now, Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(1);
        result.Items.First().Id.Should().Be(a1.Id);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_Pagination_ShouldRespectPageAndSize()
    {
        var ctx = GetDbContext();
        for (int i = 1; i <= 5; i++)
        {
            var a = CreateAuction(i);
            ctx.Auctions.Add(a);
            await ctx.SaveChangesAsync();
            ctx.Bids.Add(new Bid { AuctionId = a.Id, UserId = 1, Amount = i * 100, Status = BidStatus.Active });
        }
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { Page = 1, PageSize = 2 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Total.Should().Be(5);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_NoParticipation_ShouldReturnEmpty()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(CreateAuction(1));
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(999, filter);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetParticipatedAuctionsAsync_DistinctAuctions_ShouldNotDuplicate()
    {
        var ctx = GetDbContext();
        var auction = CreateAuction(1);
        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();

        // Multiple bids on same auction by same user
        ctx.Bids.Add(new Bid { AuctionId = auction.Id, UserId = 1, Amount = 100, Status = BidStatus.Outbid });
        ctx.Bids.Add(new Bid { AuctionId = auction.Id, UserId = 1, Amount = 200, Status = BidStatus.Active });
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var filter = new ParticipatedFilter { Page = 1, PageSize = 10 };
        var result = await repo.GetParticipatedAuctionsAsync(1, filter);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
    }

    // ────────────── GetHighestBidAsync ──────────────

    [Fact]
    public async Task GetHighestBidAsync_ShouldReturnHighestAmount()
    {
        var ctx = GetDbContext();
        var (auction, _) = await SeedAuctionWithBids(ctx, bidCount: 3);
        var repo = new BidRepository(ctx);

        var result = await repo.GetHighestBidAsync(auction.Id);

        result.Should().NotBeNull();
        result!.Amount.Should().Be(300); // 100 * 3 = highest
    }

    [Fact]
    public async Task GetHighestBidAsync_NoBids_ShouldReturnNull()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(CreateAuction(1));
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var result = await repo.GetHighestBidAsync(1);

        result.Should().BeNull();
    }

    // ────────────── GetBidCountAsync ──────────────

    [Fact]
    public async Task GetBidCountAsync_ShouldReturnCorrectCount()
    {
        var ctx = GetDbContext();
        var (auction, _) = await SeedAuctionWithBids(ctx, bidCount: 4);
        var repo = new BidRepository(ctx);

        var result = await repo.GetBidCountAsync(auction.Id);

        result.Should().Be(4);
    }

    [Fact]
    public async Task GetBidCountAsync_NoBids_ShouldReturnZero()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(CreateAuction(1));
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var result = await repo.GetBidCountAsync(1);

        result.Should().Be(0);
    }

    // ────────────── AddAsync ──────────────

    [Fact]
    public async Task AddAsync_ShouldPersistBid()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(CreateAuction(1));
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        var bid = new Bid { AuctionId = 1, UserId = 1, Amount = 500, Status = BidStatus.Active };
        await repo.AddAsync(bid);
        await repo.SaveChangesAsync();

        var result = await ctx.Bids.FirstOrDefaultAsync(x => x.AuctionId == 1);
        result.Should().NotBeNull();
        result!.Amount.Should().Be(500);
    }

    // ────────────── UpdateRangeAsync ──────────────

    [Fact]
    public async Task UpdateRangeAsync_ShouldModifyMultipleBids()
    {
        var ctx = GetDbContext();
        var (auction, bids) = await SeedAuctionWithBids(ctx, bidCount: 3);
        var repo = new BidRepository(ctx);

        foreach (var bid in bids)
            bid.Status = BidStatus.Lost;

        await repo.UpdateRangeAsync(bids);
        await repo.SaveChangesAsync();

        var updatedBids = await ctx.Bids.Where(x => x.AuctionId == auction.Id).ToListAsync();
        updatedBids.Should().OnlyContain(x => x.Status == BidStatus.Lost);
    }

    // ────────────── SaveChangesAsync ──────────────

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistChanges()
    {
        var ctx = GetDbContext();
        ctx.Auctions.Add(CreateAuction(1));
        await ctx.SaveChangesAsync();
        var repo = new BidRepository(ctx);

        ctx.Bids.Add(new Bid { AuctionId = 1, UserId = 1, Amount = 100, Status = BidStatus.Active });
        await repo.SaveChangesAsync();

        ctx.Bids.Count().Should().Be(1);
    }
}
