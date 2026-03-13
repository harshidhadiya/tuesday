using System;
using System.Collections.Generic;

namespace PRODUCT.Data.Dto.Response
{
    public class imageData
    {
        public int id{get;set;}
        public String imageUrl{get;set;}
    }
    public class ProductDto
    {
        public int id { get; set; }
        public DateTime product_buy_date { get; set; }
        public int? user_id { get; set; }
        public string Name { get; set; }
        public string? description { get; set; }
        public bool verified { get; set; } = false;
        public List<imageData> ?images{get;set;}
        public DateTime? AuctionStartTime { get; set; }
        public DateTime? AuctionEndTime { get; set; }
    }
}