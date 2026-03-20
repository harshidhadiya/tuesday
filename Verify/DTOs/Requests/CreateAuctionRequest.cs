namespace VERIFY.DTOs.Requests
{
    public class CreateAuctionRequest
{
    public int      ProductId       { get; set; }
    public decimal  StartingPrice   { get; set; }
    public decimal? ReservePrice    { get; set; }
    public decimal  MinBidIncrement { get; set; } = 1.00m;
    public DateTime StartDate       { get; set; }
    public DateTime EndDate         { get; set; }
}
}