namespace PRODUCT.Data.Dto.Request
{
    public class ProductCreate
    {
        public DateTime date { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public int ?id{get;set;}
        public int ?user_id{get;set;}
    }
}