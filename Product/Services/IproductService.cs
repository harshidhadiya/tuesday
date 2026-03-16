using PRODUCT.Data.Dto.Request;
using PRODUCT.Data.Dto.Response;

namespace  PRODUCT.Services
{
    public interface IproductService
    {
        public Task<ServiceResult<ProductDto>> createProduct(ProductCreate product);
        public Task<ServiceResult<ProductDto>> deleteProduct(int productId,int userid);
        public Task<ServiceResult<ProductDto>> updateProduct(ProductUpdate product,int useId);
        public  Task<ServiceResult<List<ProductDto>>> getAllProducts(ProductAll query);
        public Task<ServiceResult<ProductDto>> addImage(AddImage query,int productId);
        public  Task<ServiceResult<ProductDto>> deleteProductImage(int productId, int imageId, int userId);

    }
}