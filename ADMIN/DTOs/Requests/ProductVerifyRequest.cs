namespace ADMIN.DTOs.Requests
{
    public class ProductVerifyRequest
    {
        public int ProductId { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
