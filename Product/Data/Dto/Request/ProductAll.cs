namespace PRODUCT.Data.Dto.Request
{
    public class ProductAll
    {
        public bool mine { get; set; } = false;
        public bool verified { get; set; } = false;
        public int? id { get; set; } = null;
        public string? searchName { get; set; } = null;
        public int? productId { get; set; } = null;
        public DateTime? createdFrom { get; set; } = null;
        public DateTime? createdTo { get; set; } = null;
        public DateTime? buyFrom { get; set; } = null;
        public DateTime? buyTo { get; set; } = null;
        public int page{get;set;}=1;
        public int size{get;set;}=10;
    }
}