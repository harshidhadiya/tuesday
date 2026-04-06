using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using VERIFY.Controllers;
using VERIFY.Data.Dto;
using VERIFY.DTOs.Requests;
using VERIFY.DTOs.Responses;
using VERIFY.Services;
namespace Verify.Tests.Controllers
{
    public class VerifyControllerTests
    {
        private readonly Mock<IVerifyService> _service;
        private readonly Mock<ILogger<VerifyController>> _logger;
        private readonly VerifyController _sut;
        private readonly DefaultHttpContext _httpContext;

        public VerifyControllerTests()
        {
            _service = new Mock<IVerifyService>();
            _logger = new Mock<ILogger<VerifyController>>();

            _httpContext = new DefaultHttpContext();

            _sut = new VerifyController(_service.Object, _logger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = _httpContext
                }
            };
        }

        private void SetUserId(int id)
        {
            _httpContext.Items["id"] = id;
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.VerifyProduct(new VerifyProductRequest());

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_200_When_Success()
        {
            SetUserId(5);

            var req = new VerifyProductRequest { ProductId = 1, SellerId = 2 };

            var serviceResult = ServiceResult<object>.Ok(new { ProductId = 1 });

            _service.Setup(s => s.VerifyProductAsync(5, req))
                .ReturnsAsync(serviceResult);

            var result = await _sut.VerifyProduct(req);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_403_When_Forbidden()
        {
            SetUserId(5);

            var req = new VerifyProductRequest { ProductId = 1, SellerId = 2 };

            var serviceResult = ServiceResult<object>.Forbidden("No rights");

            _service.Setup(s => s.VerifyProductAsync(5, req))
                .ReturnsAsync(serviceResult);

            var result = await _sut.VerifyProduct(req);

            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_400_When_Service_Fails()
        {
            SetUserId(5);

            var req = new VerifyProductRequest { ProductId = 1, SellerId = 2 };

            var serviceResult = ServiceResult<object>.Fail("bad");

            _service.Setup(s => s.VerifyProductAsync(5, req))
                .ReturnsAsync(serviceResult);

            var result = await _sut.VerifyProduct(req);

            result.Should().BeOfType<BadRequestObjectResult>();
        }


        [Fact]
        public async Task UnverifyProduct_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.UnverifyProduct(new ProductUnverify());

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_200_When_Success()
        {
            SetUserId(5);

            var req = new ProductUnverify { productId = 1 };

            var serviceResult = ServiceResult<object>.Ok(new { ProductId = 1 });

            _service.Setup(s => s.UnverifyProductAsync(5, req, It.IsAny<HttpContext>()))
                .ReturnsAsync(serviceResult);

            var result = await _sut.UnverifyProduct(req);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_403_When_Forbidden()
        {
            SetUserId(5);

            var req = new ProductUnverify { productId = 1 };

            var serviceResult = ServiceResult<object>.Forbidden("forbidden");

            _service.Setup(s => s.UnverifyProductAsync(5, req, It.IsAny<HttpContext>()))
                .ReturnsAsync(serviceResult);

            var result = await _sut.UnverifyProduct(req);

            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_400_When_Service_Fail()
        {
            SetUserId(5);

            var req = new ProductUnverify { productId = 1 };

            var serviceResult = ServiceResult<object>.Fail("fail");

            _service.Setup(s => s.UnverifyProductAsync(5, req, It.IsAny<HttpContext>()))
                .ReturnsAsync(serviceResult);

            var result = await _sut.UnverifyProduct(req);

            result.Should().BeOfType<BadRequestObjectResult>();
        }


        [Fact]
        public async Task GetVerifyStatus_Should_Return_200()
        {
            var dto = new VerifyStatusResponse { ProductId = 1 };

            var serviceResult = ServiceResult<VerifyStatusResponse>.Ok(dto);

            _service.Setup(s => s.GetVerifyStatusAsync(1))
                .ReturnsAsync(serviceResult);

            var result = await _sut.GetVerifyStatus(1);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetVerifyStatus_Should_Return_404()
        {
            var serviceResult = ServiceResult<VerifyStatusResponse>.NotFound("Not found");

            _service.Setup(s => s.GetVerifyStatusAsync(1))
                .ReturnsAsync(serviceResult);

            var result = await _sut.GetVerifyStatus(1);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetVerifyStatus_Should_Return_400()
        {
            var serviceResult = ServiceResult<VerifyStatusResponse>.Fail("bad");

            _service.Setup(s => s.GetVerifyStatusAsync(1))
                .ReturnsAsync(serviceResult);

            var result = await _sut.GetVerifyStatus(1);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetProductsUniversal_Should_Return_400_When_No_User()
        {
            var result = await _sut.GetProductsUniversal(new FilterVerify());

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetProductsUniversal_Should_Return_200()
        {
            SetUserId(5);

            var serviceResult = ServiceResult<List<FilterResponse>>.Ok(new List<FilterResponse>());

            _service.Setup(s => s.getUniverSalVerified(It.IsAny<FilterVerify>()))
                .ReturnsAsync(serviceResult);

            var result = await _sut.GetProductsUniversal(new FilterVerify());

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CreateAuctions_Should_Return_400_When_No_User()
        {
            var result = await _sut.createAuctions(new CreateAuctionRequest());

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateAuctions_Should_Return_200()
        {
            SetUserId(5);

            var req = new CreateAuctionRequest();

            var serviceResult = ServiceResult<object>.Ok(new object());

            _service.Setup(s => s.CreatAuctionEvent(req, 5))
                .ReturnsAsync(serviceResult);

            var result = await _sut.createAuctions(req);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CreateAuctions_Should_Return_403()
        {
            SetUserId(5);

            var req = new CreateAuctionRequest();

            var serviceResult = ServiceResult<object>.Forbidden("forbidden");

            _service.Setup(s => s.CreatAuctionEvent(req, 5))
                .ReturnsAsync(serviceResult);

            var result = await _sut.createAuctions(req);

            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task CreateAuctions_Should_Return_400_When_Fail()
        {
            SetUserId(5);

            var req = new CreateAuctionRequest();

            var serviceResult = ServiceResult<object>.Fail("fail");

            _service.Setup(s => s.CreatAuctionEvent(req, 5))
                .ReturnsAsync(serviceResult);

            var result = await _sut.createAuctions(req);

            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}