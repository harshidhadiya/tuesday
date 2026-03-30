using System.Security.Claims;
using ADMIN.Data.Dto;
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
using Xunit;

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

        private void SetUserIdInContext(int id)
        {
            _httpContext.Items["id"] = id;
        }

        // =========================
        // VERIFY PRODUCT
        // =========================

        [Fact]
        public async Task VerifyProduct_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.VerifyProduct(new VerifyProductRequest());
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var req = new VerifyProductRequest { ProductId = 1, SellerId = 2 };
            var serviceResult = ServiceResult<object>.Ok(new { ProductId = 1 }, "Verified");
            _service.Setup(s => s.VerifyProductAsync(5, req)).ReturnsAsync(serviceResult);

            var result = await _sut.VerifyProduct(req);
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_403_When_Forbidden()
        {
            SetUserIdInContext(5);
            var req = new VerifyProductRequest { ProductId = 1, SellerId = 2 };
            var serviceResult = ServiceResult<object>.Forbidden("No rights");
            _service.Setup(s => s.VerifyProductAsync(5, req)).ReturnsAsync(serviceResult);

            var result = await _sut.VerifyProduct(req);
            result.Should().BeOfType<ForbidResult>();
        }

        // =========================
        // UNVERIFY PRODUCT
        // =========================

        [Fact]
        public async Task UnverifyProduct_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.UnverifyProduct(new ProductUnverify());
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var req = new ProductUnverify { productId = 1 };
            var serviceResult = ServiceResult<object>.Ok(new { ProductId = 1 }, "Unverified");
            _service.Setup(s => s.UnverifyProductAsync(5, req)).ReturnsAsync(serviceResult);

            var result = await _sut.UnverifyProduct(req);
            result.Should().BeOfType<OkObjectResult>();
        }

        // =========================
        // GET VERIFY STATUS
        // =========================

        [Fact]
        public async Task GetVerifyStatus_Should_Return_200_When_Successful()
        {
            var dto = new VerifyStatusResponse { ProductId = 1, IsVerified = true };
            var serviceResult = ServiceResult<VerifyStatusResponse>.Ok(dto);
            _service.Setup(s => s.GetVerifyStatusAsync(1)).ReturnsAsync(serviceResult);

            var result = await _sut.GetVerifyStatus(1);
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetVerifyStatus_Should_Return_404_When_Not_Found()
        {
            var serviceResult = ServiceResult<VerifyStatusResponse>.NotFound("Not found");
            _service.Setup(s => s.GetVerifyStatusAsync(1)).ReturnsAsync(serviceResult);

            var result = await _sut.GetVerifyStatus(1);
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        // =========================
        // GET PRODUCTS VERIFIED BY ME
        // =========================

        [Fact]
        public async Task GetProductsVerifiedByMe_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.GetProductsVerifiedByMe();
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetProductsVerifiedByMe_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var list = new List<VerifiedProductDetail>();
            var serviceResult = ServiceResult<List<VerifiedProductDetail>>.Ok(list);
            _service.Setup(s => s.GetProductsVerifiedByMeAsync(5, null, null, 1, 10)).ReturnsAsync(serviceResult);

            var result = await _sut.GetProductsVerifiedByMe();
            result.Should().BeOfType<OkObjectResult>();
        }

        // =========================
        // GET UNVERIFIED PRODUCTS
        // =========================

        [Fact]
        public async Task GetUnverifiedProducts_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.GetUnverifiedProducts();
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetUnverifiedProducts_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var list = new List<object>();
            var serviceResult = ServiceResult<List<object>>.Ok(list);
            _service.Setup(s => s.GetUnverifiedProductsAsync(5, null, null, 1, 10)).ReturnsAsync(serviceResult);

            var result = await _sut.GetUnverifiedProducts();
            result.Should().BeOfType<OkObjectResult>();
        }

        // =========================
        // GET PRODUCTS UNIVERSAL
        // =========================

        [Fact]
        public async Task GetProductsUniversal_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.GetProductsUniversal(new FilterVerify());
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetProductsUniversal_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var list = new List<FilterResponse>();
            var serviceResult = ServiceResult<List<FilterResponse>>.Ok(list);
            var filter = new FilterVerify();
            _service.Setup(s => s.getUniverSalVerified(It.IsAny<FilterVerify>())).ReturnsAsync(serviceResult);

            var result = await _sut.GetProductsUniversal(filter);
            result.Should().BeOfType<OkObjectResult>();
        }

        // =========================
        // CREATE AUCTIONS
        // =========================

        [Fact]
        public async Task CreateAuctions_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.createAuctions(new CreateAuctionRequest());
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateAuctions_Should_Return_200_When_Successful()
        {
            SetUserIdInContext(5);
            var req = new CreateAuctionRequest();
            var serviceResult = ServiceResult<object>.Ok(new object());
            _service.Setup(s => s.CreatAuctionEvent(req, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.createAuctions(req);
            result.Should().BeOfType<OkObjectResult>();
        }
    }
}
