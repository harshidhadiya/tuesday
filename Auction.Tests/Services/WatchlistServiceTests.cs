using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AuctionTests.Services
{
   
    public class WatchlistServiceTests
    {
        private readonly Mock<IAuctionRepository> _auctionRepo;
        private readonly Mock<IWatchlistRepository> _watchlistRepo;
        private readonly Mock<IBidRepository> _bidRepo;
        private readonly Mock<IRedisService> _redis;
        private readonly Mock<ILogger<WatchlistService>> _logger;
        private readonly WatchlistService _sut;

        public WatchlistServiceTests()
        {
            _auctionRepo   = new Mock<IAuctionRepository>();
            _watchlistRepo = new Mock<IWatchlistRepository>();
            _bidRepo       = new Mock<IBidRepository>();
            _redis         = new Mock<IRedisService>();
            _logger        = new Mock<ILogger<WatchlistService>>();

            _sut = new WatchlistService(
                _auctionRepo.Object,
                _watchlistRepo.Object,
                _bidRepo.Object,
                _redis.Object,
                _logger.Object);
        }

        [Fact]
        public async Task WatchAuction_Should_Return_404_When_Auction_Not_Found()
        {
            _auctionRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Auction?)null);

            var result = await _sut.WatchAuctionAsync(99, 1);

            result.StatusCode.Should().Be(404);
            result.Message.Should().Contain("not found");
        }

        [Fact]
        public async Task WatchAuction_Should_Return_400_When_Auction_Is_Ended()
        {
            var auction = BuildAuction(status: AuctionStatus.Ended);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

            var result = await _sut.WatchAuctionAsync(1, 5);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("ended or cancelled");
        }

        [Fact]
        public async Task WatchAuction_Should_Return_400_When_Auction_Is_Cancelled()
        {
            var auction = BuildAuction(status: AuctionStatus.Cancelled);
            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

            var result = await _sut.WatchAuctionAsync(1, 5);

            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task WatchAuction_Should_Return_409_When_Already_Watching()
        {
            var auction  = BuildAuction();
            var existing = new Watchlist { UserId = 5, AuctionId = 1 };

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _watchlistRepo.Setup(r => r.GetAsync(5, 1)).ReturnsAsync(existing);

            var result = await _sut.WatchAuctionAsync(1, 5);

            result.StatusCode.Should().Be(409);
            result.Message.Should().Contain("Already watching");
        }

        [Fact]
        public async Task WatchAuction_Should_Return_200_When_Successfully_Watched()
        {
            var auction = BuildAuction(status: AuctionStatus.Upcoming);

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _watchlistRepo.Setup(r => r.GetAsync(5, 1)).ReturnsAsync((Watchlist?)null);
            _watchlistRepo.Setup(r => r.AddAsync(It.IsAny<Watchlist>())).Returns(Task.CompletedTask);
            _watchlistRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var result = await _sut.WatchAuctionAsync(1, 5);

            result.StatusCode.Should().Be(200);
            result.Data.Should().BeTrue();
            result.Message.Should().Contain("Added to watchlist");

            _watchlistRepo.Verify(r => r.AddAsync(It.Is<Watchlist>(w => w.UserId == 5 && w.AuctionId == 1)), Times.Once);
            _watchlistRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task WatchAuction_Should_Return_200_When_Auction_Is_Live()
        {
            var auction = BuildAuction(status: AuctionStatus.Live);

            _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
            _watchlistRepo.Setup(r => r.GetAsync(5, 1)).ReturnsAsync((Watchlist?)null);
            _watchlistRepo.Setup(r => r.AddAsync(It.IsAny<Watchlist>())).Returns(Task.CompletedTask);
            _watchlistRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var result = await _sut.WatchAuctionAsync(1, 5);

            result.StatusCode.Should().Be(200);
        }

        [Fact]
        public async Task UnwatchAuction_Should_Return_404_When_Entry_Not_Found()
        {
            _watchlistRepo.Setup(r => r.GetAsync(5, 1)).ReturnsAsync((Watchlist?)null);

            var result = await _sut.UnwatchAuctionAsync(1, 5);

            result.StatusCode.Should().Be(404);
            result.Message.Should().Contain("Watchlist entry not found");
        }

        [Fact]
        public async Task UnwatchAuction_Should_Return_200_When_Successfully_Removed()
        {
            var entry = new Watchlist { UserId = 5, AuctionId = 1 };

            _watchlistRepo.Setup(r => r.GetAsync(5, 1)).ReturnsAsync(entry);
            _watchlistRepo.Setup(r => r.RemoveAsync(entry)).Returns(Task.CompletedTask);
            _watchlistRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var result = await _sut.UnwatchAuctionAsync(1, 5);

            result.StatusCode.Should().Be(200);
            result.Data.Should().BeTrue();
            result.Message.Should().Contain("Removed from watchlist");

            _watchlistRepo.Verify(r => r.RemoveAsync(entry), Times.Once);
            _watchlistRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        }


        [Fact]
        public async Task GetWatchedAuctions_Should_Return_200_With_Empty_List_When_Nothing_Watched()
        {
            var filter = new WatchListFilterRequest();
            _watchlistRepo.Setup(r => r.GetByUserIdAsync(5, filter)).ReturnsAsync(new List<Watchlist>());

            var result = await _sut.GetWatchedAuctionsAsync(5, filter);

            result.StatusCode.Should().Be(200);
            result.Data.Should().NotBeNull();
            result.Data!.Count.Should().Be(0);
        }

        [Fact]
        public async Task GetWatchedAuctions_Should_Return_200_With_Mapped_Auctions()
        {
            var filter = new WatchListFilterRequest();

            var auction = BuildAuction(status: AuctionStatus.Upcoming);
            var entries = new List<Watchlist>
            {
                new Watchlist
                {
                    UserId    = 5,
                    AuctionId = 1,
                    Auction   = auction
                }
            };

            _watchlistRepo.Setup(r => r.GetByUserIdAsync(5, filter)).ReturnsAsync(entries);
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((HighestBidCacheDto?)null);
            _bidRepo.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync((Bid?)null);
            _bidRepo.Setup(r => r.GetBidCountAsync(1)).ReturnsAsync(0);

            var result = await _sut.GetWatchedAuctionsAsync(5, filter);

            result.StatusCode.Should().Be(200);
            result.Data.Should().HaveCount(1);
            result.Data![0].Id.Should().Be(1);
        }

        [Fact]
        public async Task GetWatchedAuctions_Should_Use_Cached_Highest_Bid_From_Redis()
        {
            var filter   = new WatchListFilterRequest();
            var auction  = BuildAuction(status: AuctionStatus.Live,  startingPrice: 100m);
            var cached   = new HighestBidCacheDto { BidId = 3, UserId = 44, Amount = 350m };
            var entries  = new List<Watchlist>
            {
                new Watchlist { UserId = 5, AuctionId = 1, Auction = auction }
            };

            _watchlistRepo.Setup(r => r.GetByUserIdAsync(5, filter)).ReturnsAsync(entries);
            _redis.Setup(r => r.GetHighestBidAsync(1)).ReturnsAsync(cached);
            _bidRepo.Setup(r => r.GetBidCountAsync(1)).ReturnsAsync(5);

            var result = await _sut.GetWatchedAuctionsAsync(5, filter);

            result.StatusCode.Should().Be(200);
            result.Data![0].CurrentHighestBid.Should().Be(350m); // from Redis cache
            result.Data[0].TotalBids.Should().Be(5);

            _bidRepo.Verify(r => r.GetHighestBidAsync(It.IsAny<int>()), Times.Never); // DB not hit
        }

        [Fact]
        public async Task GetWatchedAuctions_Should_Sort_Result_By_StartDate_When_Not_Empty()
        {
            var filter  = new WatchListFilterRequest();
            var later   = BuildAuction(status: AuctionStatus.Upcoming, startDate: TimeHelper.Now().AddDays(3));
            later.Id    = 2;
            var sooner  = BuildAuction(status: AuctionStatus.Upcoming, startDate: TimeHelper.Now().AddDays(1));
            sooner.Id   = 1;

            var entries = new List<Watchlist>
            {
                new Watchlist { UserId = 5, AuctionId = 2, Auction = later },
                new Watchlist { UserId = 5, AuctionId = 1, Auction = sooner }
            };

            _watchlistRepo.Setup(r => r.GetByUserIdAsync(5, filter)).ReturnsAsync(entries);
            _redis.Setup(r => r.GetHighestBidAsync(It.IsAny<int>())).ReturnsAsync((HighestBidCacheDto?)null);
            _bidRepo.Setup(r => r.GetHighestBidAsync(It.IsAny<int>())).ReturnsAsync((Bid?)null);
            _bidRepo.Setup(r => r.GetBidCountAsync(It.IsAny<int>())).ReturnsAsync(0);

            var result = await _sut.GetWatchedAuctionsAsync(5, filter);

            result.StatusCode.Should().Be(200);
            result.Data.Should().HaveCount(2);
            // Sorted ascending by StartDate → sooner (Id=1) should come first
            result.Data![0].Id.Should().Be(1);
            result.Data[1].Id.Should().Be(2);
        }


        private static Auction BuildAuction(
            AuctionStatus status      = AuctionStatus.Upcoming,
            decimal       startingPrice = 100m,
            DateTime?     startDate   = null) => new Auction
        {
            Id              = 1,
            ProductId       = 10,
            ProductName     = "A Product",
            Description     = "Desc",
            CreatedByUserId = 7,
            StartingPrice   = startingPrice,
            MinBidIncrement = 10m,
            StartDate       = startDate ?? TimeHelper.Now().AddHours(1),
            EndDate         = TimeHelper.Now().AddHours(5),
            Status          = status
        };
    }
}
