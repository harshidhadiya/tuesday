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
using Moq;
using Xunit;

namespace AuctionTests.Controllers
{
    public class BidControllerTests
    {
        private readonly Mock<IBidService> _service;
        private readonly BidController _sut;
        
        public BidControllerTests()
        {
            _service = new Mock<IBidService>();
            _sut = new BidController(_service.Object);
        }

        private void SetupUserClaimsAndIp(int userId, string ipAddress)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            var context = new DefaultHttpContext { User = claimsPrincipal };
            
            context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
            
            _sut.ControllerContext = new ControllerContext { HttpContext = context };
        }

        [Fact]
        public async Task PlaceBid_Should_Return_201_When_Successful()
        {
            SetupUserClaimsAndIp(5, "127.0.0.1");
            var req = new PlaceBidRequest { Amount = 100 };
            var dto = new BidResponse { Id = 1 };
            var serviceResult = ServiceResult<BidResponse>.Created(dto);
            
            _service.Setup(s => s.PlaceBidAsync(1, req, 5, "127.0.0.1")).ReturnsAsync(serviceResult);

            var result = await _sut.PlaceBid(1, req) as ObjectResult;
            
            result!.StatusCode.Should().Be(201);
            var apiResp = result.Value as ApiResponse<BidResponse>;
            apiResp!.Data!.Id.Should().Be(1);
        }

        [Fact]
        public async Task PlaceBid_Should_Return_400_When_Unsuccessful()
        {
            SetupUserClaimsAndIp(5, "127.0.0.1");
            var req = new PlaceBidRequest { Amount = 100 };
            var serviceResult = ServiceResult<BidResponse>.Fail("Error", 400);
            
            _service.Setup(s => s.PlaceBidAsync(1, req, 5, "127.0.0.1")).ReturnsAsync(serviceResult);

            var result = await _sut.PlaceBid(1, req) as ObjectResult;
            result!.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task GetHistory_Should_Return_200_When_Successful()
        {
            SetupUserClaimsAndIp(5, "127.0.0.1");
            var pagedResponse = new PagedResponse<BidResponse>();
            var serviceResult = ServiceResult<PagedResponse<BidResponse>>.Ok(pagedResponse);
            
            _service.Setup(s => s.GetBidHistoryAsync(1, 1, 20, true, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.GetHistory(1, 1, 20, true) as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }
        
        [Fact]
        public async Task GetHistory_Should_Return_404_When_Not_Found()
        {
            SetupUserClaimsAndIp(5, "127.0.0.1");
            var serviceResult = ServiceResult<PagedResponse<BidResponse>>.NotFound("Not found");
            
            _service.Setup(s => s.GetBidHistoryAsync(1, 1, 20, true, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.GetHistory(1, 1, 20, true) as ObjectResult;
            result!.StatusCode.Should().Be(404);
        }
    }
}
