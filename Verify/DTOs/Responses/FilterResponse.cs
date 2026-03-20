using RabbitMQ.Client;

namespace VERIFY.DTOs.Responses
{
    public class FilterResponse
    {
        public string? ProductName { get; set; }
        public int productId{get;set;}
        public int sellerId{get;set;}
        // this description is the product description
        public string? Description { get; set; }
        public int? VerifierId { get; set; }
        public DateTime? VerifiedTime { get; set; }
        public bool IsVerified { get; set; }
        public string? VerifyDescription { get; set; }
    }
}