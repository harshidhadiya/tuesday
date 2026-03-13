using AutoMapper;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Model;
using PRODUCT.Repository;
using PRODUCT.Data.Dto.Response;
using MassTransit;
using Messaging.Contracts;

namespace PRODUCT.Services
{
    public class ProductService : IproductService
    {
        private readonly IMapper mapper;
        private readonly Irepository repository;
        private readonly IPublishEndpoint _publishEndpoint;
        public ProductService(Irepository repo, IMapper mapper, IPublishEndpoint publishEndpoint)
        {
            _publishEndpoint = publishEndpoint;
            this.repository = repo;
            this.mapper = mapper;
        }
        public async Task<ServiceResult<ProductDto>> createProduct(ProductCreate product)
        {
            var exist = await repository.exist(product.name!);
            if (exist)
            {
                return ServiceResult<ProductDto>.Fail("product already exist", 400);
            }
            var data = mapper.Map<ProductTable>(product);
            var response = await repository.Add(data);
            await _publishEndpoint.Publish(new ProductCreatedForVerification(
                ProductId: response.Id,
                SellerId: (int)response.user_id!,
                ProductName: response.product_name));

            var result = mapper.Map<ProductDto>(response);
            return ServiceResult<ProductDto>.Ok(result, "User create Successfully");
        }
        public async Task<ServiceResult<ProductDto>> deleteProduct(int productId, int userid)
        {
            var data = await repository.getByIdProduct(productId);
            if (data == null)
                return ServiceResult<ProductDto>.Fail("Product Not Found", 404);

            if (data.user_id != userid)
                return ServiceResult<ProductDto>.Forbidden("You are not owner of this Product");

            var deleted_product = await repository.deleteProduct(data);
            if(deleted_product==null)
            return ServiceResult<ProductDto>.Fail("Product didn't return",500);
            var result = mapper.Map<ProductDto>(deleted_product);
            await _publishEndpoint.Publish(new ProductDeleted(
                ProductId: productId,
                DeletedByUserId: userid));

            return ServiceResult<ProductDto>.Ok(result, "Product Delete Success Fully");
        }
        public async Task<ServiceResult<ProductDto>> updateProduct(ProductUpdate product, int userid)
        {

            var data = await repository.getByIdProduct(product.id);
            if (data == null)
                return ServiceResult<ProductDto>.NotFound("Product Not Found");

            if (data.user_id != userid)
                return ServiceResult<ProductDto>.Forbidden("You are not owner of this Product");


            if (product.AuctionStartTime != null && product.AuctionEndTime != null && product.AuctionEndTime > product.AuctionStartTime && product.AuctionStartTime > DateTime.Now)
            {
                if (!data.isVerified)
                {
                    return ServiceResult<ProductDto>.Fail("Sorry But Your Product is not verified");
                }
                Console.WriteLine("Auction Start Time: " + product.AuctionStartTime);
                Console.WriteLine("Auction End Time: " + product.AuctionEndTime);
                data.AuctionStartTime = product.AuctionStartTime;
                data.AuctionEndTime = product.AuctionEndTime;
            }
            if (product.description != null && product.description != "")
                data.product_description = product.description;
            if (product.date != null && product.date >= DateTime.Now)
                data.Buy_Date = (DateTime)product.date;


           var response=await repository.Update(data);

           return ServiceResult<ProductDto>.Ok(mapper.Map<ProductDto>(response),"product updated successfully");
        }
        public async Task<ServiceResult<List<ProductDto>>> getAllProducts(ProductAll query)
        {
               var products = await repository.AllProducts(query);
               if(products == null)
                return ServiceResult<List<ProductDto>>.NotFound("Product Not Found");

            

            var response=mapper.Map<List<ProductDto>>(products);
            return ServiceResult<List<ProductDto>>.Ok(response,$"{response.Count()} product's fetched successfully");

        }
    }
}