using System.Net;
using System.Text.Json;
using AutoMapper;
using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using VERIFY.Data.Dto;
using VERIFY.DTOs.Requests;
using VERIFY.Model;
using VERIFY.Repositories;
using VERIFY.Services;
using Xunit;

namespace Verify.Tests.Services
{
    public class VerifyServiceTests
    {
        private readonly Mock<IVerifyRepository> _repo;
        private readonly Mock<IHttpClientFactory> _httpClientFactory;
        private readonly Mock<IPublishEndpoint> _publish;
        private readonly Mock<ILogger<VerifyService>> _logger;
        private readonly Mock<IMapper> _mapper;
        private readonly VerifyService _sut;

        public VerifyServiceTests()
        {
            _repo = new Mock<IVerifyRepository>();
            _httpClientFactory = new Mock<IHttpClientFactory>();
            _publish = new Mock<IPublishEndpoint>();
            _logger = new Mock<ILogger<VerifyService>>();
            _mapper = new Mock<IMapper>();

            _sut = new VerifyService(
                _repo.Object,
                _httpClientFactory.Object,
                _publish.Object,
                _logger.Object,
                _mapper.Object
            );
        }

        private void SetupHttpClient(bool hasRights, bool isSuccess = true)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            var responseJson = JsonSerializer.Serialize(new
            {
                Data = new { VerifiedByAdmin = hasRights }
            });

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = isSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
                    Content = new StringContent(responseJson)
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost/")
            };

            _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        }


        [Fact]
        public async Task VerifyProduct_Should_Return_400_When_Invalid_Request()
        {
            var req = new VerifyProductRequest { ProductId = 0 };
            var result = await _sut.VerifyProductAsync(1, req);

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("Invalid request");
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_403_When_Admin_No_Rights()
        {
            SetupHttpClient(hasRights: false);

            var req = new VerifyProductRequest { ProductId = 1, SellerId = 1 };
            var result = await _sut.VerifyProductAsync(1, req);

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(403);
            result.Message.Should().Contain("Admin does not have verify permission");
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_400_When_SellerId_Mismatches()
        {
            SetupHttpClient(hasRights: true);
            var existing = new VerifyProductTable { SellerId = 2 };
            _repo.Setup(r => r.GetByProductIdAsync(1)).ReturnsAsync(existing);

            var req = new VerifyProductRequest { ProductId = 1, SellerId = 1 };
            var result = await _sut.VerifyProductAsync(1, req);

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("already verified by another seller");
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_200_When_Valid()
        {
            SetupHttpClient(hasRights: true);
            var existing = new VerifyProductTable { SellerId = 1 };
            _repo.Setup(r => r.GetByProductIdAsync(1)).ReturnsAsync(existing);

            var req = new VerifyProductRequest { ProductId = 1, SellerId = 1, description = "desc" };
            var result = await _sut.VerifyProductAsync(1, req);

            result.Success.Should().BeTrue();
            result.StatusCode.Should().Be(200);
            _repo.Verify(r => r.Update(existing), Times.Once);
            _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
            _publish.Verify(p => p.Publish(It.IsAny<ProductVerified>(), default), Times.Once);
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_400_When_Invalid_Id()
        {
            var req = new ProductUnverify { productId = 0 };
            var result = await _sut.UnverifyProductAsync(1, req);

            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_403_When_No_Rights()
        {
            SetupHttpClient(hasRights: false);
            var req = new ProductUnverify { productId = 1 };
            var result = await _sut.UnverifyProductAsync(1, req);

            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_404_When_Not_Found()
        {
            SetupHttpClient(hasRights: true);
            _repo.Setup(r => r.GetByProductIdAsync(1)).ReturnsAsync((VerifyProductTable?)null);

            var req = new ProductUnverify { productId = 1 };
            var result = await _sut.UnverifyProductAsync(1, req);

            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_403_When_Verified_By_Another()
        {
            SetupHttpClient(hasRights: true);
            var existing = new VerifyProductTable { VerifierId = 2 };
            _repo.Setup(r => r.GetByProductIdAsync(1)).ReturnsAsync(existing);

            var req = new ProductUnverify { productId = 1 };
            var result = await _sut.UnverifyProductAsync(1, req);

            result.StatusCode.Should().Be(403);
            result.Message.Should().Contain("You can only unverify products that you verified");
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_200_When_Valid()
        {
            SetupHttpClient(hasRights: true);
            var existing = new VerifyProductTable { VerifierId = 1 };
            _repo.Setup(r => r.GetByProductIdAsync(1)).ReturnsAsync(existing);

            var req = new ProductUnverify { productId = 1, description = "desc" };
            var result = await _sut.UnverifyProductAsync(1, req);

            result.StatusCode.Should().Be(200);
            _repo.Verify(r => r.Update(existing), Times.Once);
            _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
            _publish.Verify(p => p.Publish(It.IsAny<ProductUnverified>(), default), Times.Once);
        }


        [Fact]
        public async Task CreatAuctionEvent_Should_Return_404_When_Product_Not_Found()
        {
            _repo.Setup(r => r.GetByProductIdAsync(1)).ReturnsAsync((VerifyProductTable?)null);
            var req = new CreateAuctionRequest { ProductId = 1 };
            var result = await _sut.CreatAuctionEvent(req, 1);

            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task CreatAuctionEvent_Should_Return_403_When_User_Not_Owner()
        {
            var existing = new VerifyProductTable { SellerId = 2 };
            _repo.Setup(r => r.GetByProductIdAsync(1)).ReturnsAsync(existing);

            var req = new CreateAuctionRequest { ProductId = 1 };
            var result = await _sut.CreatAuctionEvent(req, 1);

            result.StatusCode.Should().Be(403);
            result.Message.Should().Contain("Not Owner");
        }

        [Fact]
        public async Task CreatAuctionEvent_Should_Return_403_When_Not_Verified()
        {
            var existing = new VerifyProductTable { SellerId = 1, isProductVerified = false };
            _repo.Setup(r => r.GetByProductIdAsync(1)).ReturnsAsync(existing);

            var req = new CreateAuctionRequest { ProductId = 1 };
            var result = await _sut.CreatAuctionEvent(req, 1);

            result.StatusCode.Should().Be(403);
            result.Message.Should().Contain("pending verification");
        }

        [Fact]
        public async Task CreatAuctionEvent_Should_Return_200_When_Valid()
        {
            var existing = new VerifyProductTable { SellerId = 1, isProductVerified = true, VerifierId = 2 };
            _repo.Setup(r => r.GetByProductIdAsync(1)).ReturnsAsync(existing);

            var req = new CreateAuctionRequest { ProductId = 1 };
            var result = await _sut.CreatAuctionEvent(req, 1);

            result.StatusCode.Should().Be(200);
            _publish.Verify(p => p.Publish(It.IsAny<AuctionCreatedFromVerifyService>(), default), Times.Once);
        }
    }
}
