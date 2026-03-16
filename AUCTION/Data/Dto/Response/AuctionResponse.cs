namespace AUCTION.Data.Dto.Response;

public class AuctionResponse
{
    public int      Id                { get; set; }
    public int      ProductId         { get; set; }
    public int      CreatedByUserId   { get; set; }
    public decimal  StartingPrice     { get; set; }
    public decimal? ReservePrice      { get; set; }
    public decimal  MinBidIncrement   { get; set; }
    public DateTime StartDate         { get; set; }
    public DateTime EndDate           { get; set; }
    public string   Status            { get; set; } = string.Empty;
    public decimal  CurrentHighestBid { get; set; }
    public int      TotalBids         { get; set; }
    public double?  TimeRemainingSeconds { get; set; }
    public DateTime CreatedAt         { get; set; }
}

public class AuctionDetailResponse : AuctionResponse
{
    public BidResponse?       HighestBid     { get; set; }
    public List<BidResponse>  RecentBids     { get; set; } = new();
    public int                WatcherCount   { get; set; }
    public long               LiveViewerCount { get; set; }
    public WinnerResponse?    Winner         { get; set; }
}

public class BidResponse
{
    public int      Id           { get; set; }
    public int      AuctionId    { get; set; }
    public string   MaskedBidder { get; set; } = string.Empty;
    public decimal  Amount       { get; set; }
    public string   Status       { get; set; } = string.Empty;
    public DateTime PlacedAt     { get; set; }
}

public class MyBidResponse : BidResponse
{
    public int  UserId            { get; set; }
    public bool IsCurrentlyWinning { get; set; }
}

public class WinnerResponse
{
    public int      AuctionId     { get; set; }
    public int      WinnerUserId  { get; set; }
    public decimal  FinalPrice    { get; set; }
    public DateTime ClosedAt      { get; set; }
}

public class PagedResponse<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class HighestBidCacheDto
{
    public int      BidId    { get; set; }
    public int      UserId   { get; set; }
    public decimal  Amount   { get; set; }
    public DateTime PlacedAt { get; set; }
}


   public class VerifyStatusResponse
    {
        public int ProductId { get; set; }
        public bool IsVerified { get; set; }
        public int? VerifierId { get; set; }
        public DateTime? VerifiedTime { get; set; }
        public string? Description { get; set; }
    }