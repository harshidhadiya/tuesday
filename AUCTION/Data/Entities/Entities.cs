namespace AUCTION.Data.Entities;

public enum AuctionStatus { Upcoming, Live, Ended, Cancelled ,UnVerified,Verified,Failed}
public enum BidStatus     { Active, Outbid, Won, Lost }
public static class TimeHelper
{
    private static readonly TimeZoneInfo IndiaZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public static DateTime Now()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaZone);
    }
}

public class Auction
{
    public int Id { get; set; }

    // Links to your ProductService — the verified product's ID
    public int ProductId { get; set; }
    public string ProductName{get;set;}
    public string Description{get;set;}

    // The verify_id from your VerifyService
    public int CreatedByVerifyId { get; set; }

    // The user_id from your UserService (extracted from JWT)
    public int CreatedByUserId { get; set; }
    public int Extension{get;set;}=0;
    public int maxExtension {get;set;}=3;
    public decimal StartingPrice { get; set; }
    public decimal? ReservePrice { get; set; }
    public decimal MinBidIncrement { get; set; } = 1.00m;
    
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public AuctionStatus Status { get; set; } = AuctionStatus.Upcoming;
    
    public int? WinnerBidId { get; set; }
    public int? WinnerUserId { get; set; }
    public decimal? FinalPrice { get; set; }
    public DateTime CreatedAt { get; set; } = TimeHelper.Now();
    public DateTime UpdatedAt { get; set; } = TimeHelper.Now();
    // Navigation
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public ICollection<Watchlist> Watchlists { get; set; } = new List<Watchlist>();
}

public class Bid
{
    public int Id { get; set; }
    public int AuctionId { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public BidStatus Status { get; set; } = BidStatus.Active;
    public DateTime PlacedAt { get; set; } = TimeHelper.Now();
    public string? IpAddress { get; set; }

    public Auction Auction { get; set; } = null!;
}

public class Watchlist
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int AuctionId { get; set; }
    public DateTime CreatedAt { get; set; } = TimeHelper.Now();
    public Auction Auction { get; set; } = null!;
}
