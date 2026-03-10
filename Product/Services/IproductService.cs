using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using PRODUCT.Data.Dto;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Data.Dto.Response;
using VERIFY.Services;

namespace  PRODUCT.Services
{
    public interface IproductService
    {
        public Task<ServiceResult<ProductDto>> createProduct(ProductCreate product);
        public Task<ServiceResult<ProductDto>> deleteProduct(int productId,int userid);
        public Task<ServiceResult<ProductDto>> updateProduct(ProductUpdate product,int useId);
        public  Task<ServiceResult<List<ProductDto>>> getAllProducts(ProductAll query);

    }
}