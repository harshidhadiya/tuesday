using ADMIN.Data.Dto;
using VERIFY.Data.Dto;
using VERIFY.DTOs.Requests;
using VERIFY.DTOs.Responses;
using VERIFY.Messaging;
using VERIFY.Messaging.Events;
using VERIFY.Model;
using VERIFY.Repositories;

namespace VERIFY.Services
{
   
    public class VerifyService : IVerifyService
    {
        private readonly IVerifyRepository _repository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IRabbitMqPublisher _publisher;
        private readonly ILogger<VerifyService> _logger;

        public VerifyService(
            IVerifyRepository repository,
            IHttpClientFactory httpClientFactory,
            IRabbitMqPublisher publisher,
            ILogger<VerifyService> logger)
        {
            _repository = repository;
            _httpClientFactory = httpClientFactory;
            _publisher = publisher;
            _logger = logger;
        }

        public async Task<ServiceResult<object>> VerifyProductAsync(int adminId, VerifyProductRequest request)
        {
            if (request.ProductId <= 0 || request.SellerId <= 0)
            {
                return ServiceResult<object>.Fail(
                    "Invalid request. ProductId and SellerId are required and must be greater than 0.");
            }
            var hasRights = await AdminHasVerifyPermissionAsync(adminId);
            if (!hasRights)
            {
                return ServiceResult<object>.Forbidden("Admin does not have verify permission.");
            }

            var existing = await _repository.GetByProductIdAsync(request.ProductId);

            if (existing != null && existing.SellerId != request.SellerId)
            {
                return ServiceResult<object>.Fail("Product is already verified by another seller.");
            }

            if (existing != null)
            {
                existing.VerifierId = adminId;
                existing.VerifiedTime = DateTime.UtcNow;
                existing.ProductName = request.ProductName;
                existing.isProductVerified = true;

                if (string.IsNullOrWhiteSpace(existing.Description))
                {
                    existing.Description = "Product verified by admin.";
                }

                _repository.Update(existing);
            }
            else
            {
                var entity = new VerifyProductTable
                {
                    ProductId = request.ProductId,
                    SellerId = request.SellerId,
                    VerifierId = adminId,
                    VerifiedTime = DateTime.UtcNow,
                    ProductName = request.ProductName,
                    isProductVerified = true,
                    Description = "Product verified by admin."
                };

                await _repository.AddAsync(entity);
            }

            await _repository.SaveChangesAsync();

            _publisher.Publish("product.verified", new
            {
                request.ProductId
            });

            _logger.LogInformation("Product {ProductId} verified by admin {AdminId}", request.ProductId, adminId);

            return ServiceResult<object>.Ok(
                new { request.ProductId, request.SellerId, VerifierId = adminId },
                "Product verified successfully");
        }


        public async Task<ServiceResult<object>> UnverifyProductAsync(int adminId, int productId, string? description)
        {
            if (productId <= 0)
            {
                return ServiceResult<object>.Fail("Invalid product id.");
            }

            var hasRights = await AdminHasVerifyPermissionAsync(adminId);
            if (!hasRights)
            {
                return ServiceResult<object>.Forbidden("Admin does not have verify permission.");
            }

            var record = await _repository.GetByProductIdAsync(productId);
            if (record == null)
            {
                return ServiceResult<object>.NotFound("Verification record not found for this product");
            }

            if (record.VerifierId != adminId)
            {
                _logger.LogWarning(
                    "Admin {AdminId} attempted to unverify product {ProductId} verified by another admin {VerifierId}",
                    adminId, productId, record.VerifierId);
                return ServiceResult<object>.Forbidden("You can only unverify products that you verified.");
            }

            record.isProductVerified = false;
            record.VerifiedTime = DateTime.UtcNow;
            record.Description = !string.IsNullOrWhiteSpace(description)
                ? description
                : "Product unverification requested by admin.";

            _repository.Update(record);
            await _repository.SaveChangesAsync();

            _publisher.Publish("product.unverified", new ProductUnverifiedEvent
            {
                ProductId = productId,
            });

            return ServiceResult<object>.Ok(
                new { ProductId = productId },
                "Product unverification completed and auction cleared if scheduled");
        }


        public async Task<ServiceResult<VerifyStatusResponse>> GetVerifyStatusAsync(int productId)
        {
            if (productId <= 0)
            {
                return ServiceResult<VerifyStatusResponse>.Fail("Invalid product id.");
            }

            var record = await _repository.GetByProductIdAsync(productId);

            if (record == null)
            {
                return ServiceResult<VerifyStatusResponse>.Ok(
                    new VerifyStatusResponse
                    {
                        ProductId = productId,
                        IsVerified = false,
                        Description = null
                    },
                    "Product is not verified");
            }

            return ServiceResult<VerifyStatusResponse>.Ok(
                new VerifyStatusResponse
                {
                    ProductId = productId,
                    IsVerified = record.isProductVerified,
                    VerifierId = record.VerifierId,
                    VerifiedTime = record.VerifiedTime,
                    Description = record.Description
                },
                "Product verification status retrieved successfully");
        }


        public async Task<ServiceResult<List<VerifiedProductDetail>>> GetProductsVerifiedByMeAsync(
            int adminId, string? searchName, string? authorizationHeader,int page=1,int size=10)
        {
            Console.WriteLine("Admin ID: " + adminId);
            var hasRights = await AdminHasVerifyPermissionAsync(adminId);
            if (!hasRights)
            {
                return ServiceResult<List<VerifiedProductDetail>>.Forbidden("Admin does not have verify permission.");
            }

            var verifyRecords = await _repository.GetVerifiedByAdminAsync(adminId, searchName);

            if (verifyRecords.Count == 0)
            {
                return ServiceResult<List<VerifiedProductDetail>>.Ok(
                    new List<VerifiedProductDetail>(),
                    "No verified products found for this admin");
            }

            var allProducts = await GetAllProductsFromProductServiceAsync(authorizationHeader);
            if (allProducts.Count == 0)
            {
                return ServiceResult<List<VerifiedProductDetail>>.Ok(
                    new List<VerifiedProductDetail>(),
                    "No products found in product service");
            }

            var productsById = allProducts.ToDictionary(p => p.id, p => p);
            var results = new List<VerifiedProductDetail>();

            foreach (var v in verifyRecords)
            {
                if (productsById.TryGetValue(v.ProductId, out var p))
                {
               

                    results.Add(new VerifiedProductDetail
                    {
                        ProductId = p.id,
                        ProductName = p.productName,
                        Description = p.description,
                        BuyDate = p.buyDate,
                        CreatedDate = p.createdDate,
                        VerifierId = v.VerifierId,
                        VerifiedTime = v.VerifiedTime,
                        IsVerified = v.isProductVerified,
                        VerifyDescription = v.Description
                    });
                }
            }
            results = results.Skip((page-1)*size).Take(size).ToList();

            return ServiceResult<List<VerifiedProductDetail>>.Ok(
                results,
                "Verified products with details retrieved successfully");
        }


        public async Task<ServiceResult<List<object>>> GetUnverifiedProductsAsync(
            int adminId, string? searchName, string? authorizationHeader,int page,int size)
        {
            var hasRights = await AdminHasVerifyPermissionAsync(adminId);
            if (!hasRights)
            {
                return ServiceResult<List<object>>.Forbidden("Admin does not have verify permission.");
            }

            var allProducts = await GetAllProductsFromProductServiceAsync(authorizationHeader);
            if (allProducts.Count == 0)
            {
                return ServiceResult<List<object>>.Ok(
                    new List<object>(),
                    "No products found in product service");
            }

            var verifiedSet = await _repository.GetVerifiedProductIdsAsync();

            IEnumerable<ProductSummary> unverified = allProducts
                .Where(p => !verifiedSet.Contains(p.id));

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                unverified = unverified.Where(p =>
                    !string.IsNullOrEmpty(p.productName) &&
                    p.productName.Contains(searchName, StringComparison.OrdinalIgnoreCase));
            }

            var result = unverified
                .Select(p => (object)new
                {
                    productId = p.id,
                    productName = p.productName,
                    description = p.description,
                    buyDate = p.buyDate,
                    createdDate = p.createdDate,
                    ownerId = p.userId,
                    isVerified = false
                })
                .ToList();
              result = result.Skip((page-1)*size).Take(size).ToList();


            return ServiceResult<List<object>>.Ok(
                result,
                "Unverified products with details retrieved successfully");
        }


        private async Task<bool> AdminHasVerifyPermissionAsync(int adminId)
        {
            var client = _httpClientFactory.CreateClient("DefaultClient");

            try
            {
                var response = await client.GetAsync($"/api/admin-request/details/{adminId}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Admin rights check failed with status {StatusCode} for admin {AdminId}",
                        response.StatusCode, adminId);
                    return false;
                }

                var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RequestDetailResponse>>();
                if (envelope?.Data == null)
                {
                    _logger.LogWarning("Admin rights check returned no data for admin {AdminId}", adminId);
                    return false;
                }

                return envelope.Data.VerifiedByAdmin;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error calling ADMIN service to check rights for admin {AdminId}", adminId);
                return false;
            }
        }


        private async Task<List<ProductSummary>> GetAllProductsFromProductServiceAsync(string? authorizationHeader)
        {
            var client = _httpClientFactory.CreateClient("ProductService");
            var result = new List<ProductSummary>();

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/product/all");

                if (!string.IsNullOrEmpty(authorizationHeader))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
                }

                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch products. Status: {StatusCode}", response.StatusCode);
                    return result;
                }

                var envelope = await response.Content.ReadFromJsonAsync<ProductListEnvelope>();
                if (envelope?.Data != null)
                {
                    result = envelope.Data;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error calling Product service");
            }

            return result;
        }

        private async Task<UserSummary?> GetUserFromUserServiceAsync(int? userId)
        {
            if (userId == null || userId <= 0)
            {
                return null;
            }

            var client = _httpClientFactory.CreateClient("UserService");

            try
            {
                var response = await client.GetAsync($"/api/user/{userId.Value}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch user {UserId} from user service. Status: {StatusCode}",
                        userId, response.StatusCode);
                    return null;
                }

                var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<UserSummary>>();
                return envelope?.Data;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error calling User service for user {UserId}", userId);
                return null;
            }
        }
    }
}
