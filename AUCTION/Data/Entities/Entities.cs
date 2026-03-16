namespace AUCTION.Data.Entities;

public enum AuctionStatus { Upcoming, Live, Ended, Cancelled }
public enum BidStatus     { Active, Outbid, Won, Lost }

public class Auction
{
    public int Id { get; set; }

    // Links to your ProductService — the verified product's ID
    public int ProductId { get; set; }

    // The verify_id from your VerifyService
    public int CreatedByVerifyId { get; set; }

    // The user_id from your UserService (extracted from JWT)
    public int CreatedByUserId { get; set; }

    public decimal StartingPrice { get; set; }
    public decimal? ReservePrice { get; set; }
    public decimal MinBidIncrement { get; set; } = 1.00m;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public AuctionStatus Status { get; set; } = AuctionStatus.Upcoming;

    // Set when auction closes
    public int? WinnerBidId { get; set; }
    public int? WinnerUserId { get; set; }
    public decimal? FinalPrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Bid>       Bids       { get; set; } = new List<Bid>();
    public ICollection<Watchlist> Watchlists { get; set; } = new List<Watchlist>();
}

public class Bid
{
    public int    Id        { get; set; }
    public int    AuctionId { get; set; }
    public int    UserId    { get; set; }
    public decimal Amount   { get; set; }
    public BidStatus Status { get; set; } = BidStatus.Active;
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }

    public Auction Auction { get; set; } = null!;
}

public class Watchlist
{
    public int      Id        { get; set; }
    public int      UserId    { get; set; }
    public int      AuctionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Auction Auction { get; set; } = null!;
}
