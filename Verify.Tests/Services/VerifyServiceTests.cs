using System.Net;
using System.Text.Json;
using AutoMapper;
using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using VERIFY.DTOs.Requests;
using VERIFY.Model;
using VERIFY.Repositories;
using VERIFY.Services;
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

        private HttpContext GetHttpContext()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["Cookie"] = "test-cookie";
            return context;
        }

        private void SetupHttpClient(bool hasRights, bool isSuccess = true, bool haveData = true, bool auctionSuccess = true)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
                {
                    // Admin API
                    if (request.RequestUri!.AbsolutePath.Contains("admin-request"))
                    {
                        var responseJson = JsonSerializer.Serialize(new
                        {
                            Data = haveData ? new { VerifiedByAdmin = hasRights } : null
                        });

                        return new HttpResponseMessage
                        {
                            StatusCode = isSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
                            Content = new StringContent(responseJson)
                        };
                    }

                    // Auction API
                    if (request.RequestUri.AbsolutePath.Contains("auctions"))
                    {
                        if (!auctionSuccess)
                            throw new HttpRequestException("Auction service failed");

                        var responseJson = JsonSerializer.Serialize(new
                        {
                            Data = new
                            {
                                Items = new[]
                                {
                                    new { Status = "Upcoming" }
                                }
                            }
                        });

                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.OK,
                            Content = new StringContent(responseJson)
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost/")
            };

            _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_400_When_Invalid_Request()
        {
            var req = new VerifyProductRequest { ProductId = 0 };
            var result = await _sut.VerifyProductAsync(1, req);

            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_403_When_Admin_No_Rights()
        {
            SetupHttpClient(false);

            var req = new VerifyProductRequest { ProductId = 1, SellerId = 1 };
            var result = await _sut.VerifyProductAsync(1, req);

            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_400_When_SellerId_Mismatch()
        {
            SetupHttpClient(true);

            _repo.Setup(x => x.GetByProductIdAsync(1))
                .ReturnsAsync(new VerifyProductTable { SellerId = 2 });

            var result = await _sut.VerifyProductAsync(1, new VerifyProductRequest { ProductId = 1, SellerId = 1 });

            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task VerifyProduct_Should_Return_200_When_Valid()
        {
            SetupHttpClient(true);

            var entity = new VerifyProductTable { SellerId = 1 };

            _repo.Setup(x => x.GetByProductIdAsync(1)).ReturnsAsync(entity);

            var result = await _sut.VerifyProductAsync(1,
                new VerifyProductRequest { ProductId = 1, SellerId = 1 });

            result.StatusCode.Should().Be(200);

            _repo.Verify(x => x.Update(entity), Times.Once);
            _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
            _publish.Verify(x => x.Publish(It.IsAny<ProductVerified>(), default), Times.Once);
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_400_When_Invalid_Id()
        {
            var result = await _sut.UnverifyProductAsync(1,
                new ProductUnverify { productId = 0 },
                GetHttpContext());

            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_403_When_No_Rights()
        {
            SetupHttpClient(false);

            var result = await _sut.UnverifyProductAsync(1,
                new ProductUnverify { productId = 1 },
                GetHttpContext());

            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_404_When_Not_Found()
        {
            SetupHttpClient(true);

            _repo.Setup(x => x.GetByProductIdAsync(1)).ReturnsAsync((VerifyProductTable?)null);

            var result = await _sut.UnverifyProductAsync(1,
                new ProductUnverify { productId = 1 },
                GetHttpContext());

            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_403_When_Verified_By_Another()
        {
            SetupHttpClient(true);

            _repo.Setup(x => x.GetByProductIdAsync(1))
                .ReturnsAsync(new VerifyProductTable { VerifierId = 2 });

            var result = await _sut.UnverifyProductAsync(1,
                new ProductUnverify { productId = 1 },
                GetHttpContext());

            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task UnverifyProduct_Should_Return_200_When_Valid()
        {
            SetupHttpClient(true);

            var entity = new VerifyProductTable { VerifierId = 1 };

            _repo.Setup(x => x.GetByProductIdAsync(1)).ReturnsAsync(entity);

            var result = await _sut.UnverifyProductAsync(1,
                new ProductUnverify { productId = 1 },
                GetHttpContext());

            result.StatusCode.Should().Be(200);

            _repo.Verify(x => x.Update(entity), Times.Once);
            _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
            _publish.Verify(x => x.Publish(It.IsAny<ProductUnverified>(), default), Times.Once);
        }


        [Fact]
        public async Task CreatAuctionEvent_Should_Return_404_When_NotFound()
        {
            _repo.Setup(x => x.GetByProductIdAsync(1))
                .ReturnsAsync((VerifyProductTable?)null);

            var result = await _sut.CreatAuctionEvent(new CreateAuctionRequest { ProductId = 1 }, 1);

            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task CreatAuctionEvent_Should_Return_403_When_NotOwner()
        {
            _repo.Setup(x => x.GetByProductIdAsync(1))
                .ReturnsAsync(new VerifyProductTable { SellerId = 2 });

            var result = await _sut.CreatAuctionEvent(new CreateAuctionRequest { ProductId = 1 }, 1);

            result.StatusCode.Should().Be(403);
            result.Message.Should().Contain("Your Not Owner");
        }

        [Fact]
        public async Task CreatAuctionEvent_Should_Return_403_When_NotVerified()
        {
            _repo.Setup(x => x.GetByProductIdAsync(1))
                .ReturnsAsync(new VerifyProductTable { SellerId = 1, isProductVerified = false });

            var result = await _sut.CreatAuctionEvent(new CreateAuctionRequest { ProductId = 1 }, 1);

            result.StatusCode.Should().Be(403);
            result.Message.Should().Contain("pending verification remaining");
        }

        [Fact]
        public async Task CreatAuctionEvent_Should_Return_200_When_Valid()
        {
            _repo.Setup(x => x.GetByProductIdAsync(1))
                .ReturnsAsync(new VerifyProductTable
                {
                    SellerId = 1,
                    isProductVerified = true,
                    VerifierId = 2
                });

            var result = await _sut.CreatAuctionEvent(new CreateAuctionRequest { ProductId = 1 }, 1);

            result.StatusCode.Should().Be(200);

            _publish.Verify(x => x.Publish(It.IsAny<AuctionCreatedFromVerifyService>(), default), Times.Once);
        }

        [Fact]
        public async Task GetVerifyStatusAsync_Should_Return_Invalid()
        {
            var result = await _sut.GetVerifyStatusAsync(0);

            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task GetVerifyStatusAsync_Should_Return_NotFound()
        {
            _repo.Setup(x => x.GetByProductIdAsync(1)).ReturnsAsync((VerifyProductTable?)null);

            var result = await _sut.GetVerifyStatusAsync(1);

            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task GetVerifyStatusAsync_Should_Return_Data()
        {
            _repo.Setup(x => x.GetByProductIdAsync(1))
                .ReturnsAsync(new VerifyProductTable
                {
                    ProductId = 1,
                    isProductVerified = true,
                    VerifierId = 10,
                    SellerId = 5
                });

            var result = await _sut.GetVerifyStatusAsync(1);

            result.Success.Should().BeTrue();
            result.Data.ProductId.Should().Be(1);
        }
    }
}