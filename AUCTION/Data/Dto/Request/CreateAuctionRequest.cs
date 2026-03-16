using AUCTION.Data.Entities;

namespace AUCTION.Data.Dto.Request;

public class CreateAuctionRequest
{
    public int      ProductId       { get; set; }
    public decimal  StartingPrice   { get; set; }
    public decimal? ReservePrice    { get; set; }
    public decimal  MinBidIncrement { get; set; } = 1.00m;
    public DateTime StartDate       { get; set; }
    public DateTime EndDate         { get; set; }
}

public class UpdateAuctionRequest
{
    public decimal?  StartingPrice   { get; set; }
    public decimal?  ReservePrice    { get; set; }
    public decimal?  MinBidIncrement { get; set; }
    public DateTime? StartDate       { get; set; }
    public DateTime? EndDate         { get; set; }
}

public class PlaceBidRequest
{
    public decimal Amount { get; set; }
}

public class AuctionFilterRequest
{
    public AuctionStatus? Status   { get; set; }
    public decimal?       MinPrice { get; set; }
    public decimal?       MaxPrice { get; set; }
    public int            Page     { get; set; } = 1;
    public int            PageSize { get; set; } = 20;
}

// ─────────────────────────────────────────────────────────────────────────────

