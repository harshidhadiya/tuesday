using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using AUCTION.Services;
using AutoMapper;
using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Moq;

namespace AuctionTests.Services
{
    public class AuctionServiceTests
    {
        // ---- mocks ----
        private readonly Mock<IAuctionRepository>    _auctionRepo;
        private readonly Mock<IBidRepository>        _bidRepo;
        private readonly Mock<IWatchlistRepository>  _watchlistRepo;
        private readonly Mock<IRedisService>         _redis;
        private readonly Mock<IPublishEndpoint>      _publish;
        private readonly Mock<IAuctionHubService>    _hub;
        private readonly Mock<ILogger<AuctionService>> _logger;
        private readonly Mock<IMapper>               _mapper;
        private readonly AuctionService              _sut;

        public AuctionServiceTests()
        {
            _auctionRepo   = new Mock<IAuctionRepository>();
            _bidRepo       = new Mock<IBidRepository>();
            _watchlistRepo = new Mock<IWatchlistRepository>();
            _redis         = new Mock<IRedisService>();
            _publish       = new Mock<IPublishEndpoint>();
            _hub           = new Mock<IAuctionHubService>();
            _logger        = new Mock<ILogger<AuctionService>>();
            _mapper        = new Mock<IMapper>();

            _sut = new AuctionService(
                _auctionRepo.Object,
                _bidRepo.Object,
                _watchlistRepo.Object,
                _redis.Object,
                _publish.Object,
                _hub.Object,
                _logger.Object,
                _mapper.Object);

            // Default: Hub calls are no-ops
            _hub.Setup(h => h.BroadcastAuctionUpdated(It.IsAny<int>(), It.IsAny<object>())).Returns(Task.CompletedTask);
            _hub.Setup(h => h.BroadcastAuctionClosed(It.IsAny<int>(), It.IsAny<object>())).Returns(Task.CompletedTask);
            _hub.Setup(h => h.BroadcastAuctionStarted(It.IsAny<int>())).Returns(Task.CompletedTask);
        }

       
        [Fact]
        public async Task GetAuction_Should_Return_404_When_Auction_Not_Found()
        {
            _auctionRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Auction?)null);

            var result = await _sut.GetAuctionAsync(99, 1);

            result.StatusCode.Should().Be(404);
            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task GetAuction_Should_Return_200_With_Detail_When_Auction_Exists()
        {
            var auction = BuildAuction(1, 100, AuctionStatus.Upcoming);

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((HighestBidCacheDto?)null);
            _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((Bid?)null);
            _bidRepo.Setup(r => r.GetBidCountAsync(1)).ReturnsAsync(0);
            _watchlistRepo.Setup(r => r.GetWatcherCountAsync(1)).ReturnsAsync(5);
            _redis.Setup(r => r.GetViewerCountAsync(1)).ReturnsAsync(12L);

            var result = await _sut.GetAuctionAsync(1, 100);

            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task GetAllAuctions_Should_Return_200_With_Paged_Response()
        {
            var filter   = new AuctionFilterRequest { Page = 1, PageSize = 10 };
            var auctions = new List<Auction> { BuildAuction(1, 50), BuildAuction(2, 50) };

            _auctionRepo.Setup(r => r.GetAllAsync(filter)).ReturnsAsync((auctions, 2));
            _redis.Setup(r => r.GetHighestBidAsync(It.IsAny<int>())).ReturnsAsync((HighestBidCacheDto?)null);
            _bidRepo.Setup(r => r.GetHighestBidAsync(It.IsAny<int>())).ReturnsAsync((Bid?)null);
            _bidRepo.Setup(r => r.GetBidCountAsync(It.IsAny<int>())).ReturnsAsync(0);

            var result = await _sut.GetAllAuctionsAsync(filter);

            result.StatusCode.Should().Be(200);
            result.Data!.Items.Should().HaveCount(2);
            result.Data.TotalCount.Should().Be(2);
        }


        [Fact]
        public async Task UpdateAuction_Should_Return_404_When_Auction_Not_Found()
        {
            _auctionRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync((Auction?)null);

            var result = await _sut.UpdateAuctionAsync(5, new UpdateAuctionRequest(), 1);

            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task UpdateAuction_Should_Return_403_When_Caller_Is_Not_Owner()
        {
            var auction = BuildAuction(1, createdByUserId: 10);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

            var result = await _sut.UpdateAuctionAsync(1, new UpdateAuctionRequest(), 99);

            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task UpdateAuction_Should_Return_400_When_Auction_Is_Live()
        {
            var auction = BuildAuction(1, createdByUserId: 1, status: AuctionStatus.Live);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _bidRepo.Setup(r => r.GetBidCountAsync(1)).ReturnsAsync(0);

            var result = await _sut.UpdateAuctionAsync(1, new UpdateAuctionRequest(), 1);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("already started");
        }

        [Fact]
        public async Task UpdateAuction_Should_Return_400_When_Changing_Price_After_Bids()
        {
            var auction  = BuildAuction(1, createdByUserId: 1, status: AuctionStatus.Upcoming);
            var request  = new UpdateAuctionRequest { StartingPrice = 999m };

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _bidRepo.Setup(r => r.GetBidCountAsync(1)).ReturnsAsync(3); // bids exist

            var result = await _sut.UpdateAuctionAsync(1, request, 1);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("starting price");
        }

        [Fact]
        public async Task UpdateAuction_Should_Return_200_And_Publish_When_Dates_Changed()
        {
            var auction = BuildAuction(1, createdByUserId: 1, status: AuctionStatus.Upcoming);
            var request = new UpdateAuctionRequest
            {
                StartDate = TimeHelper.Now().AddHours(1),
                EndDate   = TimeHelper.Now().AddHours(3)
            };

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _bidRepo.Setup(r => r.GetBidCountAsync(1)).ReturnsAsync(0);
            _auctionRepo.Setup(r => r.UpdateAsync(auction)).Returns(Task.CompletedTask);
            _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _auctionRepo.Setup(r => r.GetByIdAsyncWithWatchList(1)).ReturnsAsync(auction);
            _mapper.Setup(m => m.Map<AuctionUpdateConsumerDto>(auction)).Returns(new AuctionUpdateConsumerDto());

            var result = await _sut.UpdateAuctionAsync(1, request, 1);

            result.StatusCode.Should().Be(200);
            _publish.Verify(p => p.Publish(It.IsAny<ProductAddAuctionDate>(), default), Times.Once);
            _hub.Verify(h => h.BroadcastAuctionUpdated(1, It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task CancelAuction_Should_Return_404_When_Auction_Not_Found()
        {
            _auctionRepo.Setup(r => r.GetByIdAsync(9)).ReturnsAsync((Auction?)null);
            var result = await _sut.CancelAuctionAsync(9, 1);
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task CancelAuction_Should_Return_403_When_Caller_Is_Not_Owner()
        {
            var auction = BuildAuction(1, createdByUserId: 10);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

            var result = await _sut.CancelAuctionAsync(1, 99);
            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task CancelAuction_Should_Return_400_When_Auction_Is_Live()
        {
            var auction = BuildAuction(1, createdByUserId: 1, status: AuctionStatus.Live);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

            var result = await _sut.CancelAuctionAsync(1, 1);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("live or already ended");
        }

        [Fact]
        public async Task CancelAuction_Should_Return_400_When_Auction_Is_Already_Ended()
        {
            var auction = BuildAuction(1, createdByUserId: 1, status: AuctionStatus.Ended);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

            var result = await _sut.CancelAuctionAsync(1, 1);
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task CancelAuction_Should_Return_200_And_Publish_When_Upcoming()
        {
            var auction = BuildAuction(1, createdByUserId: 1, status: AuctionStatus.Upcoming);

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _auctionRepo.Setup(r => r.UpdateAsync(auction)).Returns(Task.CompletedTask);
            _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _hub.Setup(h => h.BroadcastAuctionClosed(1, It.IsAny<object>())).Returns(Task.CompletedTask);

            var result = await _sut.CancelAuctionAsync(1, 1);

            result.StatusCode.Should().Be(200);
            result.Data.Should().BeTrue();
            auction.Status.Should().Be(AuctionStatus.Cancelled);
            _publish.Verify(p => p.Publish(It.IsAny<AuctionCancelled>(), default), Times.Once);
            _publish.Verify(p => p.Publish(It.IsAny<ProductAddAuctionDate>(), default), Times.Once);
        }


        [Fact]
        public async Task GetMyCreatedAuctions_Should_Return_200_With_All_Auctions()
        {
            var auctions = new List<Auction> { BuildAuction(1, 7), BuildAuction(2, 7) };

            _auctionRepo.Setup(r => r.GetByUserIdAsync(7)).ReturnsAsync(auctions);
            _redis.Setup(r => r.GetHighestBidAsync(It.IsAny<int>())).ReturnsAsync((HighestBidCacheDto?)null);
            _bidRepo.Setup(r => r.GetHighestBidAsync(It.IsAny<int>())).ReturnsAsync((Bid?)null);
            _bidRepo.Setup(r => r.GetBidCountAsync(It.IsAny<int>())).ReturnsAsync(0);

            var result = await _sut.GetMyCreatedAuctionsAsync(7);

            result.StatusCode.Should().Be(200);
            result.Data.Should().HaveCount(2);
        }


        [Fact]
        public async Task StartAuction_Should_Return_404_When_Auction_Not_Found()
        {
            _auctionRepo.Setup(r => r.GetByIdWithBidsAsync(9)).ReturnsAsync((Auction?)null);

            var result = await _sut.StartAuctionAsync(9);
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task StartAuction_Should_Return_400_When_Auction_Is_Not_Upcoming()
        {
            var auction = BuildAuction(1, status: AuctionStatus.Live);
            auction.Bids = new List<Bid>();
            _auctionRepo.Setup(r => r.GetByIdWithBidsAsync(1)).ReturnsAsync(auction);

            var result = await _sut.StartAuctionAsync(1);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("not in upcoming state");
        }

        [Fact]
        public async Task StartAuction_Should_Return_200_And_Set_StatusLive()
        {
            var auction = BuildAuction(1, status: AuctionStatus.Upcoming);
            auction.Bids = new List<Bid>(); // no bids

            _auctionRepo.Setup(r => r.GetByIdWithBidsAsync(1)).ReturnsAsync(auction);
            _auctionRepo.Setup(r => r.UpdateAsync(auction)).Returns(Task.CompletedTask);
            _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _hub.Setup(h => h.BroadcastAuctionStarted(1)).Returns(Task.CompletedTask);

            var result = await _sut.StartAuctionAsync(1);

            result.StatusCode.Should().Be(200);
            auction.Status.Should().Be(AuctionStatus.Live);
            _publish.Verify(p => p.Publish(It.IsAny<AuctionStarted>(), default), Times.Once);
            _hub.Verify(h => h.BroadcastAuctionStarted(1), Times.Once);
        }

        [Fact]
        public async Task StartAuction_Should_Cache_Highest_Bid_In_Redis_When_Bids_Exist()
        {
            var bid = new Bid { Id = 1, Amount = 500m, UserId = 99 };
            var auction = BuildAuction(1, status: AuctionStatus.Upcoming);
            auction.Bids = new List<Bid> { bid };

            _auctionRepo.Setup(r => r.GetByIdWithBidsAsync(1)).ReturnsAsync(auction);
            _auctionRepo.Setup(r => r.UpdateAsync(auction)).Returns(Task.CompletedTask);
            _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _hub.Setup(h => h.BroadcastAuctionStarted(1)).Returns(Task.CompletedTask);
            _redis.Setup(r => r.SetHighestBidAsync(1, It.IsAny<HighestBidCacheDto>())).Returns(Task.CompletedTask);

            var result = await _sut.StartAuctionAsync(1);

            result.StatusCode.Should().Be(200);
            _redis.Verify(r => r.SetHighestBidAsync(1, It.IsAny<HighestBidCacheDto>()), Times.Once);
        }

        [Fact]
        public async Task CloseAuction_Should_Return_404_When_Auction_Not_Found()
        {
            _auctionRepo.Setup(r => r.GetByIdAsync(9)).ReturnsAsync((Auction?)null);
            var result = await _sut.CloseAuctionAsync(9);
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task CloseAuction_Should_Return_400_When_Auction_Is_Not_Live()
        {
            var auction = BuildAuction(1, status: AuctionStatus.Upcoming);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

            var result = await _sut.CloseAuctionAsync(1);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("not live");
        }

        [Fact]
        public async Task CloseAuction_Should_Return_200_No_Winner_When_No_Bids()
        {
            var auction = BuildAuction(1, status: AuctionStatus.Live);
            auction.ReservePrice = null;

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((Bid?)null);
            _auctionRepo.Setup(r => r.UpdateAsync(auction)).Returns(Task.CompletedTask);
            _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _redis.Setup(r => r.DeleteAuctionCacheAsync(1)).Returns(Task.CompletedTask);

            var result = await _sut.CloseAuctionAsync(1);

            result.StatusCode.Should().Be(200);
            result.Message.Should().Contain("no winner");
            auction.Status.Should().Be(AuctionStatus.Ended);
            _publish.Verify(p => p.Publish(It.IsAny<AuctionClosed>(), default), Times.Once);
        }

        [Fact]
        public async Task CloseAuction_Should_Return_200_With_Winner_When_Reserve_Met()
        {
            var auction = BuildAuction(1, status: AuctionStatus.Live);
            auction.ReservePrice = 100m;

            var highestBid = new Bid { Id = 10, UserId = 55, Amount = 200m };

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(highestBid);
            _bidRepo.Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<Bid>>())).Returns(Task.CompletedTask);
            _auctionRepo.Setup(r => r.UpdateAsync(auction)).Returns(Task.CompletedTask);
            _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _redis.Setup(r => r.DeleteAuctionCacheAsync(1)).Returns(Task.CompletedTask);

            var result = await _sut.CloseAuctionAsync(1);

            result.StatusCode.Should().Be(200);
            result.Data!.WinnerUserId.Should().Be(55);
            result.Data.FinalPrice.Should().Be(200m);
            auction.WinnerUserId.Should().Be(55);

            _publish.Verify(p => p.Publish(It.IsAny<AuctionWinnerDeclared>(), default), Times.Once);
            _publish.Verify(p => p.Publish(It.IsAny<AuctionClosed>(), default), Times.Once);
        }

        [Fact]
        public async Task CloseAuction_Should_Have_No_Winner_When_Reserve_Not_Met()
        {
            var auction = BuildAuction(1, status: AuctionStatus.Live);
            auction.ReservePrice = 500m; // reserve is 500

            var highestBid = new Bid { Id = 10, UserId = 55, Amount = 100m }; // bid only 100

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(highestBid);
            _auctionRepo.Setup(r => r.UpdateAsync(auction)).Returns(Task.CompletedTask);
            _auctionRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _redis.Setup(r => r.DeleteAuctionCacheAsync(1)).Returns(Task.CompletedTask);

            var result = await _sut.CloseAuctionAsync(1);

            result.StatusCode.Should().Be(200);
            result.Message.Should().Contain("no winner");
            auction.WinnerUserId.Should().BeNull();

            _publish.Verify(p => p.Publish(It.IsAny<AuctionWinnerDeclared>(), default), Times.Never);
        }
        private static Auction BuildAuction(
            int id = 1,
            int createdByUserId = 1,
            AuctionStatus status = AuctionStatus.Upcoming) => new Auction
        {
            Id               = id,
            ProductId        = 10,
            ProductName      = "Test Product",
            Description      = "Test Description",
            CreatedByUserId  = createdByUserId,
            StartingPrice    = 100m,
            MinBidIncrement  = 10m,
            StartDate        = TimeHelper.Now().AddHours(1),
            EndDate          = TimeHelper.Now().AddHours(2),
            Status           = status
        };
    }
}
