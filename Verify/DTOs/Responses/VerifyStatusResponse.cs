namespace VERIFY.DTOs.Responses
{
    public class VerifyStatusResponse
    {
        public int ProductId { get; set; }
        public bool IsVerified { get; set; }
        public int? VerifierId { get; set; }
        public DateTime? VerifiedTime { get; set; }
        public string? Description { get; set; }
    }
}
