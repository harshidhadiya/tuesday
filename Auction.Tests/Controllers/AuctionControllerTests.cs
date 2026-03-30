using System.Net;
using System.Security.Claims;
using AUCTION.Controllers;
using AUCTION.Data.Dto;
using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Services;
using AUCTION.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuctionTests.Controllers
{
    public class AuctionControllerTests
    {
        private readonly Mock<IAuctionService> _service;
        private readonly Mock<ILogger<AuctionController>> _logger;
        private readonly Mock<IHttpClientFactory> _factory;
        private readonly AuctionController _sut;
        
        public AuctionControllerTests()
        {
            _service = new Mock<IAuctionService>();
            _logger = new Mock<ILogger<AuctionController>>();
            _factory = new Mock<IHttpClientFactory>();

            _sut = new AuctionController(_logger.Object, _service.Object, _factory.Object);
        }

        private void SetupUserClaims(int userId)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }


        [Fact]
        public async Task GetAll_Should_Return_200_When_Successful()
        {
            SetupUserClaims(5);
            var filter = new AuctionFilterRequest();
            var pagedResponse = new PagedResponse<AuctionResponse> { TotalCount = 1, Items = new List<AuctionResponse>() };
            var serviceResult = ServiceResult<PagedResponse<AuctionResponse>>.Ok(pagedResponse);
            
            _service.Setup(s => s.GetAllAuctionsAsync(It.IsAny<AuctionFilterRequest>())).ReturnsAsync(serviceResult);

            var result = await _sut.GetAll(filter) as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task GetAll_Should_Return_ErrorData_When_Unsuccessful()
        {
            SetupUserClaims(5);
            var filter = new AuctionFilterRequest();
            var serviceResult = ServiceResult<PagedResponse<AuctionResponse>>.Fail("Error", 400);
            
            _service.Setup(s => s.GetAllAuctionsAsync(It.IsAny<AuctionFilterRequest>())).ReturnsAsync(serviceResult);

            var result = await _sut.GetAll(filter) as ObjectResult;
            result!.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task GetById_Should_Return_404_When_Not_Found()
        {
            SetupUserClaims(5);
            var serviceResult = ServiceResult<AuctionDetailResponse>.NotFound("Not found");
            _service.Setup(s => s.GetAuctionAsync(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.GetById(1) as ObjectResult;
            result!.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GetById_Should_Return_200_When_Successful()
        {
            SetupUserClaims(5);
            var dto = new AuctionDetailResponse { Id = 1 };
            var serviceResult = ServiceResult<AuctionDetailResponse>.Ok(dto);
            _service.Setup(s => s.GetAuctionAsync(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.GetById(1) as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task Update_Should_Return_200_When_Successful()
        {
            SetupUserClaims(5);
            var req = new UpdateAuctionRequest();
            var dto = new AuctionResponse { Id = 1 };
            var serviceResult = ServiceResult<AuctionResponse>.Ok(dto);
            _service.Setup(s => s.UpdateAuctionAsync(1, req, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.Update(1, req) as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task Cancel_Should_Return_200_When_Successful()
        {
            SetupUserClaims(5);
            var serviceResult = ServiceResult<bool>.Ok(true);
            _service.Setup(s => s.CancelAuctionAsync(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.Cancel(1) as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }


        [Fact]
        public async Task GetMyCreated_Should_Return_200_When_Successful()
        {
            SetupUserClaims(5);
            var list = new List<AuctionResponse>();
            var serviceResult = ServiceResult<List<AuctionResponse>>.Ok(list);
            _service.Setup(s => s.GetMyCreatedAuctionsAsync(5)).ReturnsAsync(serviceResult);

            var result = await _sut.GetMyCreated() as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task GetMyParticipated_Should_Return_200_When_Successful()
        {
            SetupUserClaims(5);
            var filter = new ParticipatedFilter();
            var pagedResponse = new PagedResponse<AuctionResponse>();
            var serviceResult = ServiceResult<PagedResponse<AuctionResponse>>.Ok(pagedResponse);
            _service.Setup(s => s.GetMyParticipatedAuctionsAsync(5, filter)).ReturnsAsync(serviceResult);

            var result = await _sut.GetMyParticipated(filter) as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }
    }
}

