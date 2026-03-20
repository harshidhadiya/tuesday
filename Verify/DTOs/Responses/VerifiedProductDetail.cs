namespace VERIFY.DTOs.Responses
{
    /// <summary>
    /// Enriched response combining verify record + product + owner data.
    /// Used by GetProductsVerifiedByMe endpoint.
    /// </summary>
    public class VerifiedProductDetail
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Description { get; set; }
        public DateTime BuyDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? VerifierId { get; set; }
        public DateTime? VerifiedTime { get; set; }
        public bool IsVerified { get; set; }
        public string? VerifyDescription { get; set; }
    }

    public class OwnerInfo
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
    }
    
}
    //    public int Id { get; set; }

    //    // Product that is being verified for auction
    //    public int ProductId { get; set; }

    //    // Owner/seller of the product
    //    public int SellerId { get; set; }

    //    // Admin who verified the product
    //    public int ?VerifierId { get; set; }

    //    // When the product was verified
    //    public DateTime ?VerifiedTime { get; set; }

    //    // Snapshot of product name at the time of verification (for searching)
    //    public string ProductName { get; set; } = string.Empty;
    //    public string ?Description {get; set; } = string.Empty;
    //    public bool isProductVerified { get; set;}