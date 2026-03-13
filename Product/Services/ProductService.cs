using AutoMapper;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Model;
using PRODUCT.Repository;
using PRODUCT.Data.Dto.Response;
using MassTransit;
using Messaging.Contracts;
using CloudinaryService;

namespace PRODUCT.Services
{
    public class ProductService : IproductService
    {
        private readonly IMapper mapper;
        private readonly Irepository repository;
        private readonly ClodinaryService clodinary;
        private readonly IPublishEndpoint _publishEndpoint;
        public readonly ILogger<ProductService> logger;
        public ProductService(Irepository repo, IMapper mapper, IPublishEndpoint publishEndpoint,ClodinaryService clodinary,ILogger<ProductService> logger)
        {
            _publishEndpoint = publishEndpoint;
            this.repository = repo;
            this.mapper = mapper;
            this.clodinary=clodinary;
            this.logger=logger;
        }
        public async Task<ServiceResult<ProductDto>> createProduct(ProductCreate product)
        {
            var exist = await repository.exist(product.name!);
            if (exist)
            {
                return ServiceResult<ProductDto>.Fail("product already exist", 400);
            }

            List<ImageTable> images=null;
            if (product.images !=null)
            {
                images=await addImages(product.images);
            }

            var data = mapper.Map<ProductTable>(product);
            data.images=images ?? new List<ImageTable>();
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
        public async Task<List<ImageTable>> addImages(List<IFormFile> files)
        {
            List<ImageTable> images=new List<ImageTable>();
            if (files==null || files.Count()==0)
            {
                return images;
            }
            try
            {
                int count=0;
                foreach (var item in files)
                {
                    if(count>=5)break;
                    logger.LogInformation(item+"");
                    var data=await clodinary.singleUpload(item);

                    if(data.url != null && data.publicId!= null)
                    images.Add(new ImageTable{Image_URL=data.url,public_Id=data.publicId});
                    count++;
                }
                return images;
            }
            catch (System.Exception ex)
            {
                
                logger.LogError(ex.Message+ " while Storing the Images");
                logger.LogError("stackTrace : "+ex.StackTrace);
            }
            return new List<ImageTable>();
        }
    }
}