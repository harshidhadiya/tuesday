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
        public ProductService(Irepository repo, IMapper mapper, IPublishEndpoint publishEndpoint, ClodinaryService clodinary, ILogger<ProductService> logger)
        {
            _publishEndpoint = publishEndpoint;
            this.repository = repo;
            this.mapper = mapper;
            this.clodinary = clodinary;
            this.logger = logger;
        }
        public async Task<ServiceResult<ProductDto>> createProduct(ProductCreate product)
        {
            var exist = await repository.exist(product.name!);
            if (exist)
            {
                return ServiceResult<ProductDto>.Fail("product already exist", 400);
            }

            List<ImageTable> images = null;
            if (product.images != null)
            {
                images = await addImages(product.images);
            }

            var data = mapper.Map<ProductTable>(product);
            data.images = images ?? new List<ImageTable>();
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
            if (deleted_product == null)
                return ServiceResult<ProductDto>.Fail("Product didn't return", 500);
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

            if (product.ids != null && product.images != null && product.ids.Count() > 0 && product.images.Count() > 0)
            {
                data.images = await updateImages(product.images, product.ids, data.images);

            }

            var response = await repository.Update(data);

            return ServiceResult<ProductDto>.Ok(mapper.Map<ProductDto>(response), "product updated successfully");
        }

        public async Task<ServiceResult<ProductDto>> addImage(AddImage query, int userid)
        {
            var data = await repository.getByIdProduct(query.id);

            if (data == null)
                return ServiceResult<ProductDto>.NotFound("Product Not Found");

            if (data.user_id != userid)
                return ServiceResult<ProductDto>.Forbidden("You are not owner of this Product");

            if (query.images == null || query.images.Count() == 0)
                return ServiceResult<ProductDto>.Fail("No Images Found to Add", 400);

            var newImages = await addImages(query.images, data.images?.Count ?? 0);

            if (data.images == null)
                data.images = new List<ImageTable>();

            foreach (var img in newImages)
                data.images.Add(img);

            var response = await repository.Update(data);

            return ServiceResult<ProductDto>.Ok(
                mapper.Map<ProductDto>(response),
                "Images added successfully"
            );
        }

        public async Task<ServiceResult<List<ProductDto>>> getAllProducts(ProductAll query)
        {
            var products = await repository.AllProducts(query);
            if (products == null)
                return ServiceResult<List<ProductDto>>.NotFound("Product Not Found");



            var response = mapper.Map<List<ProductDto>>(products);
            return ServiceResult<List<ProductDto>>.Ok(response, $"{response.Count()} product's fetched successfully");

        }

        public async Task<ServiceResult<ProductDto>> deleteProductImage(int productId, int imageId, int userId)
        {
            var product = await repository.getByIdProduct(productId);

            if (product == null)
                throw new KeyNotFoundException("Product not found");

            if (product.user_id != userId)
                throw new UnauthorizedAccessException("You are not owner of this product");

            if (product.images == null || product.images.Count == 0)
                throw new InvalidOperationException("Product has no images");

            var image = product.images.FirstOrDefault(x => x.Id == imageId);

            if (image == null)
                throw new KeyNotFoundException("Image not found");

            try
            {

                product.images.Remove(image);

                var response = await repository.Update(product);

                await _publishEndpoint.Publish(new productDeleteImage(
                    PublicId: image.public_Id
                ));
                return ServiceResult<ProductDto>.Ok(
                    mapper.Map<ProductDto>(response),
                    "Image deleted successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting product image");
                throw;
            }
        }

        // this is the helper used in the another method to update the images of the product and also add new images if needed
        public async Task<ICollection<ImageTable>> updateImages(
           List<IFormFile> files,
           List<int> ids,
           ICollection<ImageTable> images)
        {
            if (files == null || files.Count == 0)
                throw new ArgumentException("No images provided");

            if (ids == null || ids.Count == 0)
                throw new ArgumentException("Image ids required");

            if (files.Count != ids.Count)
                throw new ArgumentException("Images count and ids count must match");

            if (images == null || images.Count == 0)
                throw new InvalidOperationException("Product has no images");

            for (int i = 0; i < files.Count; i++)
            {
                var image = images.FirstOrDefault(x => x.Id == ids[i]);

                if (image == null)
                    throw new KeyNotFoundException($"Image with id {ids[i]} not found");

                try
                {
                    var upload = await clodinary.singleUpload(files[i]);

                    if (upload.url == null || upload.publicId == null)
                        throw new Exception("Image upload failed");

                    await _publishEndpoint.Publish(new productDeleteImage(
                        PublicId : image.public_Id
                    ));

                    image.Image_URL = upload.url;
                    image.public_Id = upload.publicId;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error updating product image");
                    throw;
                }
            }

            return images;
        }
        public async Task<List<ImageTable>> addImages(List<IFormFile>? files, int alreadyHas = 0)
        {
            if (files == null || files.Count == 0)
                throw new ArgumentException("No images provided");

            if (alreadyHas >= 5)
                throw new InvalidOperationException("Product already has maximum allowed images");

            if (files.Count + alreadyHas > 5)
                throw new InvalidOperationException("Maximum 5 images allowed per product");

            List<ImageTable> images = new();

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("Invalid image file");

                try
                {
                    var result = await clodinary.singleUpload(file);

                    if (result.url == null || result.publicId == null)
                        throw new Exception("Image upload failed");

                    images.Add(new ImageTable
                    {
                        Image_URL = result.url,
                        public_Id = result.publicId
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while uploading image to Cloudinary");
                    throw;
                }
            }

            return images;
        }
    }
}