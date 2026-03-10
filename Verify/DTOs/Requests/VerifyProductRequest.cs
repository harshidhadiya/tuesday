namespace VERIFY.DTOs.Requests
{
    public class VerifyProductRequest
    {
        public int ProductId { get; set; }
        public int SellerId { get; set; }
        public string ProductName { get; set; } = string.Empty;
    }
}
