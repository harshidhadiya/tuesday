namespace USER.Data.Dto
{
    public class RequestDetailDto
    {
        public int Id { get; set; }
        // I changed this: Added Name and Email to match ADMIN microservice response
        public string Name { get; set; }
        public string Email { get; set; }
        public int RequestUserId { get; set; }
        public int VerifierId { get; set; }
        public bool VerifiedByAdmin { get; set; }
        public bool HasRightToAdd { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime? RightsGrantedAt { get; set; }
    }
    
}