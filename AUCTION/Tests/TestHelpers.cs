using AUCTION.Data;
using AUCTION.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AUCTION.Tests;

// ── In-memory DB factory ──────────────────────────────────────────────────────
public static class TestDbFactory
{
    public static AuctionDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new AuctionDbContext(options);
    }
}

// ── Seed data builders ────────────────────────────────────────────────────────
public static class SeedData
{
    public const int UserId1    = 101;
    public const int UserId2    = 202;
    public const int AdminId    = 999;
    public const int VerifyId1  = 501;
    public const int ProductId1 = 10;

    public static Auction UpcomingAuction(int? id = null) => new()
    {
        Id                = id ?? 0,    // 0 = let EF assign
        ProductId         = ProductId1,
        CreatedByUserId   = UserId1,
        CreatedByVerifyId = VerifyId1,
        StartingPrice     = 100m,
        MinBidIncrement   = 10m,
        StartDate         = DateTime.UtcNow.AddHours(1),
        EndDate           = DateTime.UtcNow.AddDays(1),
        Status            = AuctionStatus.Upcoming
    };

    public static Auction LiveAuction(int? id = null) => new()
    {
        Id                = id ?? 0,
        ProductId         = ProductId1,
        CreatedByUserId   = UserId1,
        CreatedByVerifyId = VerifyId1,
        StartingPrice     = 100m,
        MinBidIncrement   = 10m,
        StartDate         = DateTime.UtcNow.AddHours(-1),
        EndDate           = DateTime.UtcNow.AddHours(2),
        Status            = AuctionStatus.Live
    };

    public static Auction EndedAuction(int? id = null) => new()
    {
        Id                = id ?? 0,
        ProductId         = ProductId1,
        CreatedByUserId   = UserId1,
        CreatedByVerifyId = VerifyId1,
        StartingPrice     = 100m,
        MinBidIncrement   = 10m,
        StartDate         = DateTime.UtcNow.AddDays(-2),
        EndDate           = DateTime.UtcNow.AddDays(-1),
        Status            = AuctionStatus.Ended
    };

    public static Bid ActiveBid(int auctionId, int userId, decimal amount) => new()
    {
        AuctionId = auctionId,
        UserId    = userId,
        Amount    = amount,
        Status    = BidStatus.Active,
        PlacedAt  = DateTime.UtcNow
    };
}
