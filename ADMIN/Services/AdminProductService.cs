using System.Threading.Tasks;
using ADMIN.DTOs.Requests;
using MassTransit;
using Messaging.Contracts;

namespace ADMIN.Services
{
    public class AdminProductService : IAdminProductService
    {
        private readonly IPublishEndpoint _publishEndpoint;

        public class sendDataVerifyProduct
        {
            public int ProductId { get; set; }
            public int verifierId { get; set; }
            public string Description { get; set; } = string.Empty;
        }

        public AdminProductService(IPublishEndpoint publishEndpoint)
        {
            _publishEndpoint = publishEndpoint;
        }

        public ServiceResult<object> VerifyProduct(ProductVerifyRequest request, int userid)
        {
            _publishEndpoint.Publish(new ProductVerifyRequested(
                ProductId: request.ProductId,
                VerifierId: userid,
                Description: request.Description));
            
            return ServiceResult<object>.Ok(new object(), "Product verification request sent successfully");
        }

        public ServiceResult<object> UnverifyProduct(int productId, int userid, string description)
        {
            _publishEndpoint.Publish(new ProductUnverifyRequested(
                ProductId: productId,
                AdminId: userid,
                Description: description != "" ? description : "Unverified by admin"));
            
            return ServiceResult<object>.Ok(new object(), "Product unverification request sent successfully");
        }
    }
}
