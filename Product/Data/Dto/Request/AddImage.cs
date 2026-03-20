namespace PRODUCT.Data.Dto.Request
{
   public class AddImage
    {
        public int id{get;set;}
        public List<IFormFile> ?images{get;set;}
    }
    public class ProductUpdate : AddImage
    {
         public List<int> ?ids{get;set;}
        
        public string ?name { get; set; }=null;
        public string? description { get; set; }=null;
        public DateTime ?date { get; set; }=null;
        public DateTime? AuctionStartTime { get; set; }=null;
        public DateTime? AuctionEndTime { get; set; }=null;
    }

}