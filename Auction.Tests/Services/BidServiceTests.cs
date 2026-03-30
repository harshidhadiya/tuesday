using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using AUCTION.Services;
using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Moq;

namespace AuctionTests.Services
{
    
    public class BidServiceTests
    {
        private readonly Mock<IAuctionRepository>   _auctionRepo;
        private readonly Mock<IBidRepository>       _bidRepo;
        private readonly Mock<IRedisService>        _redis;
        private readonly Mock<IPublishEndpoint>     _publish;
        private readonly Mock<IAuctionHubService>   _hub;
        private readonly Mock<ILogger<BidService>>  _logger;
        private readonly BidService                 _sut;

        public BidServiceTests()
        {
            _auctionRepo = new Mock<IAuctionRepository>();
            _bidRepo     = new Mock<IBidRepository>();
            _redis       = new Mock<IRedisService>();
            _publish     = new Mock<IPublishEndpoint>();
            _hub         = new Mock<IAuctionHubService>();
            _logger      = new Mock<ILogger<BidService>>();

            _sut = new BidService(
                _auctionRepo.Object,
                _bidRepo.Object,
                _redis.Object,
                _publish.Object,
                _hub.Object,
                _logger.Object);

            // Default no-ops for hub
            _hub.Setup(h => h.BroadcastBidPlaced(It.IsAny<int>(), It.IsAny<object>())).Returns(Task.CompletedTask);
            _hub.Setup(h => h.BroadcastTimerTick(It.IsAny<int>(), It.IsAny<double>())).Returns(Task.CompletedTask);
            _hub.Setup(h => h.AuctionMessage(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            // Default: lock always acquired
            _redis.Setup(r => r.SetBidLockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);
            _redis.Setup(r => r.ReleaseBidLockAsync(It.IsAny<int>(), It.IsAny<int>())).Returns(Task.CompletedTask);
        }
        

        [Fact]
        public async Task PlaceBid_Should_Return_400_When_Redis_Lock_Not_Acquired()
        {
            _redis.Setup(r => r.SetBidLockAsync(1, 1, It.IsAny<TimeSpan>())).ReturnsAsync(false);

            var result = await _sut.PlaceBidAsync(1, new PlaceBidRequest { Amount = 200m }, 1, null);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("Please wait");
        }

        [Fact]
        public async Task PlaceBid_Should_Return_404_When_Auction_Not_Found()
        {
            _auctionRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Auction?)null);

            var result = await _sut.PlaceBidAsync(99, new PlaceBidRequest { Amount = 200m }, 1, null);

            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task PlaceBid_Should_Return_400_When_Auction_Is_Not_Live()
        {
            var auction = BuildLiveAuction(status: AuctionStatus.Upcoming);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

            var result = await _sut.PlaceBidAsync(1, new PlaceBidRequest { Amount = 200m }, 1, null);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("not currently live");
        }

        [Fact]
        public async Task PlaceBid_Should_Return_403_When_Creator_Bids_On_Own_Auction()
        {
            var auction = BuildLiveAuction(createdByUserId: 5);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

            // Caller (user 5) is also the creator
            var result = await _sut.PlaceBidAsync(1, new PlaceBidRequest { Amount = 200m }, 5, null);

            result.StatusCode.Should().Be(403);
            result.Message.Should().Contain("your own auction");
        }

        [Fact]
        public async Task PlaceBid_Should_Return_400_When_Bid_Amount_Below_Minimum()
        {
            var auction = BuildLiveAuction(startingPrice: 100m, minBidIncrement: 10m);

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            // Redis returns no cached bid, DB also returns null → minimum is starting price
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((HighestBidCacheDto?)null);
            _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((Bid?)null);

            // Bid of 50 is below starting price of 100
            var result = await _sut.PlaceBidAsync(1, new PlaceBidRequest { Amount = 50m }, 99, null);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("at least");
        }

        [Fact]
        public async Task PlaceBid_Should_Return_400_When_Bid_Below_Current_Highest_Plus_Increment()
        {
            var auction = BuildLiveAuction(startingPrice: 100m, minBidIncrement: 10m);
            var cachedBid = new HighestBidCacheDto { BidId = 5, UserId = 20, Amount = 200m };

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(cachedBid);

            // Minimum bid must be 200 + 10 = 210; sending 205 should fail
            var result = await _sut.PlaceBidAsync(1, new PlaceBidRequest { Amount = 205m }, 99, null);

            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task PlaceBid_Should_Return_201_And_Outbid_Previous_Bidder_When_Valid()
        {
            var auction = BuildLiveAuction(startingPrice: 100m, minBidIncrement: 10m);
            var currentHighest = new HighestBidCacheDto { BidId = 5, UserId = 20, Amount = 200m };
            var previousBid    = new Bid { Id = 5, UserId = 20, Amount = 200m };

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(currentHighest);
            _bidRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(previousBid);
            _bidRepo.Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<Bid>>())).Returns(Task.CompletedTask);
            _bidRepo.Setup(r => r.AddAsync(It.IsAny<Bid>())).Returns(Task.CompletedTask);
            _bidRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _redis.Setup(r => r.SetHighestBidAsync(1, It.IsAny<HighestBidCacheDto>())).Returns(Task.CompletedTask);

            // Bid of 215 (200 + 10 = 210 minimum → 215 passes)
            var result = await _sut.PlaceBidAsync(1, new PlaceBidRequest { Amount = 215m }, 99, "127.0.0.1");

            result.StatusCode.Should().Be(201);
            result.Success.Should().BeTrue();
            result.Data!.Amount.Should().Be(215m);
            previousBid.Status.Should().Be(BidStatus.Outbid);

            _publish.Verify(p => p.Publish(It.IsAny<AuctionBidPlaced>(), default), Times.Once);
            _hub.Verify(h => h.BroadcastBidPlaced(1, It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task PlaceBid_Should_Return_201_As_First_Bid_When_No_Previous_Bids()
        {
            var auction = BuildLiveAuction(startingPrice: 100m, minBidIncrement: 10m);

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((HighestBidCacheDto?)null);
            _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((Bid?)null);
            _bidRepo.Setup(r => r.AddAsync(It.IsAny<Bid>())).Returns(Task.CompletedTask);
            _bidRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _redis.Setup(r => r.SetHighestBidAsync(1, It.IsAny<HighestBidCacheDto>())).Returns(Task.CompletedTask);

            var result = await _sut.PlaceBidAsync(1, new PlaceBidRequest { Amount = 100m }, 99, null);

            result.StatusCode.Should().Be(201);
            result.Data!.Amount.Should().Be(100m);
        }


        [Fact]
        public async Task GetBidHistory_Should_Return_404_When_No_Bids_Found()
        {
            _bidRepo.Setup(r => r.GetByAuctionIdAsync(1, 1, 10, false, 0)).ReturnsAsync((List<Bid>?)null);

            var result = await _sut.GetBidHistoryAsync(1, 1, 10, false, 0);

            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GetBidHistory_Should_Return_200_With_Masked_Bidders()
        {
            var bids = new List<Bid>
            {
                new Bid { Id = 1, AuctionId = 1, UserId = 1234, Amount = 200m, Status = BidStatus.Active },
                new Bid { Id = 2, AuctionId = 1, UserId = 9,    Amount = 150m, Status = BidStatus.Outbid }
            };

            _bidRepo.Setup(r => r.GetByAuctionIdAsync(1, 1, 10, false, 0)).ReturnsAsync(bids);
            _bidRepo.Setup(r => r.GetBidCountAsync(1)).ReturnsAsync(2);

            var result = await _sut.GetBidHistoryAsync(1, 1, 10, false, 0);

            result.StatusCode.Should().Be(200);
            result.Data!.Items.Should().HaveCount(2);
            result.Data.TotalCount.Should().Be(2);
            // MaskedBidder should not expose raw user ID
            result.Data.Items.ForEach(b => b.MaskedBidder.Should().Contain("***"));
        }


        [Fact]
        public async Task GetHighestBid_Should_Return_404_When_Auction_Not_Found()
        {
            _auctionRepo.Setup(r => r.GetByIdAsync(9)).ReturnsAsync((Auction?)null);
            var result = await _sut.GetHighestBidAsync(9);
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GetHighestBid_Should_Return_200_With_Cached_Value_From_Redis()
        {
            var auction   = BuildLiveAuction();
            var cached    = new HighestBidCacheDto { BidId = 3, UserId = 55, Amount = 300m };

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(cached);

            var result = await _sut.GetHighestBidAsync(1);

            result.StatusCode.Should().Be(200);
            result.Data!.Amount.Should().Be(300m);
            _bidRepo.Verify(r => r.GetHighestBidAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetHighestBid_Should_Fallback_To_DB_When_Redis_Returns_Null()
        {
            var auction  = BuildLiveAuction();
            var dbBid    = new Bid { Id = 7, UserId = 88, Amount = 400m };
            var cached   = default(HighestBidCacheDto?);

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(cached);
            _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(dbBid);
            _redis.Setup(r => r.SetHighestBidAsync(1, It.IsAny<HighestBidCacheDto>())).Returns(Task.CompletedTask);

            var result = await _sut.GetHighestBidAsync(1);

            result.StatusCode.Should().Be(200);
            result.Data!.Amount.Should().Be(400m);
            _redis.Verify(r => r.SetHighestBidAsync(1, It.IsAny<HighestBidCacheDto>()), Times.Once);
        }

        [Fact]
        public async Task GetHighestBid_Should_Return_200_With_Null_When_No_Bids_At_All()
        {
            var auction = BuildLiveAuction();

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((HighestBidCacheDto?)null);
            _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((Bid?)null);

            var result = await _sut.GetHighestBidAsync(1);

            result.StatusCode.Should().Be(200);
            result.Data.Should().BeNull();
        }


        private static Auction BuildLiveAuction(
            AuctionStatus status = AuctionStatus.Live,
            int   createdByUserId = 1,
            decimal startingPrice    = 100m,
            decimal minBidIncrement  = 10m) => new Auction
        {
            Id              = 1,
            CreatedByUserId = createdByUserId,
            StartingPrice   = startingPrice,
            MinBidIncrement = minBidIncrement,
            StartDate       = DateTime.UtcNow.AddHours(-1),
            EndDate         = DateTime.UtcNow.AddHours(1),
            Status          = status,
            Extension       = 0,
            maxExtension    = 3
        };
    }
}
