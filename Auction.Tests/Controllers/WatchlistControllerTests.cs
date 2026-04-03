using System.Security.Claims;
using AUCTION.Controllers;
using AUCTION.Data.Dto;
using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Services;
using AUCTION.Services.Interfaces;
using Azure.Core;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AuctionTests.Controllers
{
    public class WatchlistControllerTests
    {
        private readonly Mock<IWatchlistService> _service;
        private readonly WatchlistController _sut;
        
        public WatchlistControllerTests()
        {
            _service = new Mock<IWatchlistService>();
            _sut = new WatchlistController(_service.Object);
        }

        private void SetupUserClaims(int userId)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            var items=new Dictionary<object,object?>();
            // this i write for the practice purpose only
            items["data"]="nothing";
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal ,Items=items}
            };
            
        }

        [Fact]
        public async Task Watch_Should_Return_200_When_Successful()
        {
            SetupUserClaims(5);
            var serviceResult = ServiceResult<bool>.Ok(true);
            _service.Setup(s => s.WatchAuctionAsync(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.Watch(1) as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task Watch_Should_Return_400_When_Unsuccessful()
        {
            SetupUserClaims(5);
            var serviceResult = ServiceResult<bool>.Fail("Error", 400);
            _service.Setup(s => s.WatchAuctionAsync(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.Watch(1) as ObjectResult;
            result!.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task Unwatch_Should_Return_200_When_Successful()
        {
            SetupUserClaims(5);
            var serviceResult = ServiceResult<bool>.Ok(true);
            _service.Setup(s => s.UnwatchAuctionAsync(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.Unwatch(1) as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task Unwatch_Should_Return_404_When_Not_Found()
        {
            SetupUserClaims(5);
            var serviceResult = ServiceResult<bool>.NotFound("Not found");
            _service.Setup(s => s.UnwatchAuctionAsync(1, 5)).ReturnsAsync(serviceResult);

            var result = await _sut.Unwatch(1) as ObjectResult;
            result!.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GetWatched_Should_Return_200_When_Successful()
        {
            SetupUserClaims(5);
            var filter = new WatchListFilterRequest();
            var list = new List<AuctionResponse>();
            var serviceResult = ServiceResult<List<AuctionResponse>>.Ok(list);
            _service.Setup(s => s.GetWatchedAuctionsAsync(5, filter)).ReturnsAsync(serviceResult);

            var result = await _sut.GetWatched(filter) as ObjectResult;
            result!.StatusCode.Should().Be(200);
        }
        [Fact]
        public async Task GetWatched_Should_Return_Error_Response_When_service_Return_Error()
        {
            // Given

            SetupUserClaims(5);
            var filter=new WatchListFilterRequest();
            var serviceResult= ServiceResult<List<AuctionResponse>>.Fail("not okay");
            _service.Setup(x=> x.GetWatchedAuctionsAsync(5,filter)).ReturnsAsync(serviceResult);
            // When
        
            var result = await _sut.GetWatched(filter) as ObjectResult;
            // Then
            result.Should().NotBeNull();
            result.StatusCode.Should().Be(serviceResult.StatusCode);
            result.Value.Should().NotBeNull();
            var data=result.Value as ApiResponse<object>;
            data.Should().NotBeNull();
            data.StatusCode.Should().Be(serviceResult.StatusCode);
            data.Message.Should().Be(serviceResult.Message);

              
        }
    }
}
