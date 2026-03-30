using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PRODUCT.Controllers;
using PRODUCT.Data.Dto;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Data.Dto.Response;
using PRODUCT.Services;
namespace Product.Tests.Controllers
{
    public class ProductControllerTests
    {
        private readonly Mock<IproductService> _service;
        private readonly Mock<ILogger<ProductController>> _logger;
        private readonly ProductController _sut;
        private readonly DefaultHttpContext _httpContext;

        public ProductControllerTests()
        {
            _service = new Mock<IproductService>();
            _logger = new Mock<ILogger<ProductController>>();

            _httpContext = new DefaultHttpContext();
            _sut = new ProductController(_service.Object, _logger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = _httpContext }
            };
        }

        private void SetUserIdInContext(int id)
        {
            _httpContext.Items["id"] = id;
        }

     

        [Fact]
        public async Task CreateProduct_Should_Return_400_When_No_UserId_In_Token()
        {
            // Act
            var result = await _sut.createProduct(new ProductCreate()) as BadRequestObjectResult;

            // Assert
            result.Should().NotBeNull();
            result!.StatusCode.Should().Be(400);
            var apiResponse = result.Value as ApiResponse<object>;
            apiResponse.Should().NotBeNull();
            apiResponse!.Message.Should().Contain("Your Id is not valid in the token");
        }

        [Fact]
        public async Task CreateProduct_Should_Return_400_When_Service_Returns_Error()
        {
            // Arrange
            SetUserIdInContext(5);
            var req = new ProductCreate();
            var serviceResult = ServiceResult<ProductDto>.Fail("Name exists", 400);
            _service.Setup(s => s.createProduct(req)).ReturnsAsync(serviceResult);

            // Act
            var result = await _sut.createProduct(req) as BadRequestObjectResult;

            // Assert
            result.Should().NotBeNull();
            result!.StatusCode.Should().Be(400);
            var apiResponse = result.Value as ApiResponse<object>;
            apiResponse!.Message.Should().Be("Name exists");
        }

        [Fact]
        public async Task CreateProduct_Should_Return_200_When_Successful()
        {
            // Arrange
            SetUserIdInContext(5);
            var req = new ProductCreate();
            var dto = new ProductDto { id = 1, Name = "Prod" };
            var serviceResult = ServiceResult<ProductDto>.Ok(dto, "Created");
            _service.Setup(s => s.createProduct(req)).ReturnsAsync(serviceResult);

            // Act
            var result = await _sut.createProduct(req) as OkObjectResult;

            // Assert
            result.Should().NotBeNull();
            result!.StatusCode.Should().Be(200);
            var apiResponse = result.Value as ApiResponse<ProductDto>;
            apiResponse!.Data!.id.Should().Be(1);
        }

     

        [Fact]
        public async Task DeleteProduct_Should_Return_400_When_No_UserId_In_Token()
        {
            var result = await _sut.deleteproduct(1) as BadRequestObjectResult;
            result!.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task DeleteProduct_Should_Return_404_When_Not_Found()
        {
            SetUserIdInContext(5);
            var serviceResult = ServiceResult<ProductDto>.NotFound("Not found");
            _service.Setup(s => s.deleteProduct(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.deleteproduct(1) as NotFoundObjectResult;
            result!.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task DeleteProduct_Should_Return_403_When_Forbidden()
        {
            SetUserIdInContext(5);
            var serviceResult = ServiceResult<ProductDto>.Forbidden("Forbidden");
            _service.Setup(s => s.deleteProduct(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.deleteproduct(1) as ForbidResult;
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteProduct_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var dto = new ProductDto { id = 1 };
            var serviceResult = ServiceResult<ProductDto>.Ok(dto);
            _service.Setup(s => s.deleteProduct(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.deleteproduct(1) as OkObjectResult;
            result!.StatusCode.Should().Be(200);
        }



        [Fact]
        public async Task UpdateProduct_Should_Return_400_When_No_UserId_In_Token()
        {
            var result = await _sut.updateproduct(1, new ProductUpdate()) as BadRequestObjectResult;
            result!.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task UpdateProduct_Should_Return_404_When_Not_Found()
        {
            SetUserIdInContext(5);
            var req = new ProductUpdate();
            var serviceResult = ServiceResult<ProductDto>.NotFound();
            _service.Setup(s => s.updateProduct(req, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.updateproduct(1, req) as NotFoundObjectResult;
            result!.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task UpdateProduct_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var req = new ProductUpdate();
            var dto = new ProductDto { id = 1 };
            var serviceResult = ServiceResult<ProductDto>.Ok(dto);
            _service.Setup(s => s.updateProduct(req, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.updateproduct(1, req) as OkObjectResult;
            result!.StatusCode.Should().Be(200);
        }



        [Fact]
        public async Task GetAllProducts_Should_Return_400_When_No_UserId_In_Token()
        {
            var result = await _sut.getallProducts(new ProductAll()) as BadRequestObjectResult;
            result!.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task GetAllProducts_Should_Return_404_When_Service_Fails()
        {
            SetUserIdInContext(5);
            var req = new ProductAll();
            var serviceResult = ServiceResult<List<ProductDto>>.NotFound("Not found");
            _service.Setup(s => s.getAllProducts(req)).ReturnsAsync(serviceResult);

            var result = await _sut.getallProducts(req) as NotFoundObjectResult;
            result!.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GetAllProducts_Should_Return_404_When_Empty_List()
        {
            SetUserIdInContext(5);
            var req = new ProductAll();
            var serviceResult = ServiceResult<List<ProductDto>>.Ok(new List<ProductDto>());
            _service.Setup(s => s.getAllProducts(req)).ReturnsAsync(serviceResult);

            var result = await _sut.getallProducts(req) as NotFoundObjectResult;
            result!.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GetAllProducts_Should_Return_200_When_Data_Exists()
        {
            SetUserIdInContext(5);
            var req = new ProductAll();
            var dtos = new List<ProductDto> { new ProductDto { id = 1 } };
            var serviceResult = ServiceResult<List<ProductDto>>.Ok(dtos);
            _service.Setup(s => s.getAllProducts(req)).ReturnsAsync(serviceResult);

            var result = await _sut.getallProducts(req) as OkObjectResult;
            result!.StatusCode.Should().Be(200);
            var apiResp = result.Value as ApiResponse<List<ProductDto>>;
            apiResp!.Data.Should().HaveCount(1);
        }



        [Fact]
        public async Task AddImages_Should_Return_400_When_No_UserId_In_Token()
        {
            var result = await _sut.addImages(new AddImage()) as BadRequestObjectResult;
            result!.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task AddImages_Should_Return_404_When_Not_Found()
        {
            SetUserIdInContext(5);
            var req = new AddImage();
            var serviceResult = ServiceResult<ProductDto>.NotFound();
            _service.Setup(s => s.addImage(req, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.addImages(req) as NotFoundObjectResult;
            result!.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task AddImages_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var req = new AddImage();
            var dto = new ProductDto { id = 1 };
            var serviceResult = ServiceResult<ProductDto>.Ok(dto);
            _service.Setup(s => s.addImage(req, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.addImages(req) as OkObjectResult;
            result!.StatusCode.Should().Be(200);
        }



        [Fact]
        public async Task DeleteProductImage_Should_Return_400_When_No_UserId_In_Token()
        {
            var result = await _sut.deleteProductImage(1, 1) as BadRequestObjectResult;
            result!.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task DeleteProductImage_Should_Return_404_When_Not_Found()
        {
            SetUserIdInContext(5);
            var serviceResult = ServiceResult<ProductDto>.NotFound();
            _service.Setup(s => s.deleteProductImage(1, 2, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.deleteProductImage(1, 2) as NotFoundObjectResult;
            result!.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task DeleteProductImage_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var dto = new ProductDto { id = 1 };
            var serviceResult = ServiceResult<ProductDto>.Ok(dto);
            _service.Setup(s => s.deleteProductImage(1, 2, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.deleteProductImage(1, 2) as OkObjectResult;
            result!.StatusCode.Should().Be(200);
        }
    }
}
