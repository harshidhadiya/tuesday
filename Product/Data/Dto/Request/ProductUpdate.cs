namespace PRODUCT.Data.Dto.Request
{
    public class ProductUpdate
    {
        public int id{get;set;}
        public string ?name { get; set; }=null;
        public string? description { get; set; }=null;
        public DateTime ?date { get; set; }=null;
        public DateTime? AuctionStartTime { get; set; }=null;
        public DateTime? AuctionEndTime { get; set; }=null;
    }
}