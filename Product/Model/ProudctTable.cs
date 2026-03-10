
namespace PRODUCT.Model
{
    public class ProductTable

    {
        public int Id { get; set; }
        public DateTime Buy_Date { get; set; }
        public int? user_id { get; set; }
        public string product_name { get; set; }
        public string? product_description { get; set; }
        public DateTime creation_date { get; set; }
        public bool isVerified{get;set;}=false;
        public ICollection<ImageTable>? images { get; set; } = new List<ImageTable>();
        public DateTime? AuctionStartTime { get; set; }
        public DateTime? AuctionEndTime { get; set; }
    }
}