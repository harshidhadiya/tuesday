using AUCTION.Data.Entities;

namespace AUCTION.Data.Dto.Request
{
    public class AuctionUpdateConsumerDto
    {
        public int StartingPrice { get; set; }
        public DateTime StartDate { get; set; }
        public int MinBidIncrement { get; set; }
        public DateTime EndDate { get; set; }
        public int Id { get; set; }
        public List<int> users { get; set; } = new List<int>();
        public string Status { get; set; } =string.Empty;
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int totalBids{get;set;}=0;
        public string productDescription{get;set;}=string.Empty;
    }
}