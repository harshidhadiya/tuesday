namespace VERIFY.DTOs.Responses
{
    /// <summary>
    /// DTO for reading product data from the Product microservice.
    /// Property names use camelCase to match the JSON returned by the Product service.
    /// </summary>
    public class ProductSummary
    {
        public int id { get; set; }
        public int? userId { get; set; }
        public string productName { get; set; } = string.Empty;
        public string? description { get; set; }
        public DateTime buyDate { get; set; }
        public DateTime createdDate { get; set; }
    }

    /// <summary>
    /// Envelope used to deserialize the Product service list response.
    /// </summary>
    public class ProductListEnvelope
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public List<ProductSummary>? Data { get; set; }
    }
}
