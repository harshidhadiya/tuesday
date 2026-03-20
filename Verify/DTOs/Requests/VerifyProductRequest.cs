namespace VERIFY.DTOs.Requests
{
    public class VerifyProductRequest
    {
        public int ProductId { get; set; }
        public int SellerId { get; set; }
        public string description { get; set; } = string.Empty;
    }
}
