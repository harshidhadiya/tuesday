namespace Verify.DTOs.Responses{
    
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
    public string ?productName{get;set;}
    public string ?productDescription{get;set;}
}
public class PagedResponse<T>
{
    public List<T> Items      { get; set; } = new();
    public int     TotalCount { get; set; }
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
}