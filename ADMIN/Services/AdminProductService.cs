using System.Threading.Tasks;
using ADMIN.DTOs.Requests;
using ADMIN.Messaging;

namespace ADMIN.Services
{
    public class AdminProductService : IAdminProductService
    {
        private readonly IRabbitMqPublisher _publisher;

        public class sendDataVerifyProduct
        {
            public int ProductId { get; set; }
            public int verifierId { get; set; }
            public string Description { get; set; } = string.Empty;
        }

        public AdminProductService(IRabbitMqPublisher publisher)
        {
            _publisher = publisher;
        }

        public ServiceResult<object> VerifyProduct(ProductVerifyRequest request, int userid)
        {
            var data = new sendDataVerifyProduct
            {
                ProductId = request.ProductId,
                verifierId = userid,
                Description = request.Description
            };

            _publisher.Publish("product.verify", data);
            
            return ServiceResult<object>.Ok(new object(), "Product verification request sent successfully");
        }

        public ServiceResult<object> UnverifyProduct(int productId, int userid, string description)
        {
            _publisher.Publish<object>("admin.unverify", new 
            { 
                productId = productId, 
                adminId = userid, 
                description = description != "" ? description : "Unverified by admin" 
            });
            
            return ServiceResult<object>.Ok(new object(), "Product unverification request sent successfully");
        }
    }
}
