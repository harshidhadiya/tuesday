using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Services.Interfaces;

namespace AUCTION.Services;

public class WatchlistService : IWatchlistService
{
    private readonly IAuctionRepository _auctionRepo;
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IBidRepository _bidRepo;
    private readonly IRedisService _redis;
    private readonly ILogger<WatchlistService> _logger;

    public WatchlistService(
        IAuctionRepository auctionRepo,
        IWatchlistRepository watchlistRepo,
        IBidRepository bidRepo,
        IRedisService redis,
        ILogger<WatchlistService> logger)
    {
        _auctionRepo = auctionRepo;
        _watchlistRepo = watchlistRepo;
        _bidRepo = bidRepo;
        _redis = redis;
        _logger = logger;
    }

    public async Task<ServiceResult<bool>> WatchAuctionAsync(int auctionId, int userId)
    {
        var auction = await _auctionRepo.GetByIdAsync(auctionId);
        if (auction == null) return ServiceResult<bool>.NotFound("Auction not found");

        if (auction.Status is AuctionStatus.Ended or AuctionStatus.Cancelled)
            return ServiceResult<bool>.Fail("Cannot watch an ended or cancelled auction");

        var existing = await _watchlistRepo.GetAsync(userId, auctionId);
        if (existing != null) return ServiceResult<bool>.Conflict("Already watching this auction");

        await _watchlistRepo.AddAsync(new Watchlist { UserId = userId, AuctionId = auctionId });
        await _watchlistRepo.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true, "Added to watchlist");
    }

    public async Task<ServiceResult<bool>> UnwatchAuctionAsync(int auctionId, int userId)
    {
        var entry = await _watchlistRepo.GetAsync(userId, auctionId);
        if (entry == null) return ServiceResult<bool>.NotFound("Watchlist entry not found");

        await _watchlistRepo.RemoveAsync(entry);
        await _watchlistRepo.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true, "Removed from watchlist");
    }

    public async Task<ServiceResult<List<AuctionResponse>>> GetWatchedAuctionsAsync(int userId,WatchListFilterRequest filter)
    {
        var entries = await _watchlistRepo.GetByUserIdAsync(userId,filter);
        var result = new List<AuctionResponse>();

        // 1. Get current Indian Standard Time
      

        foreach (var w in entries)
        {
            var highest = await GetHighestBidWithFallbackAsync(w.AuctionId);
            var bidCount = await _bidRepo.GetBidCountAsync(w.AuctionId);

            result.Add(new AuctionResponse
            {
                Id = w.Auction.Id,
                ProductId = w.Auction.ProductId,
                CreatedByUserId = w.Auction.CreatedByUserId,
                StartingPrice = w.Auction.StartingPrice,
                ReservePrice = w.Auction.CreatedByUserId==userId?w.Auction.ReservePrice:null,
                MinBidIncrement = w.Auction.MinBidIncrement,
                StartDate = w.Auction.StartDate,
                EndDate = w.Auction.EndDate,
                Status = w.Auction.Status.ToString(),
                CurrentHighestBid = highest?.Amount ?? w.Auction.StartingPrice,
                TotalBids = bidCount,
                productName=w.Auction.ProductName,
                productDescription=w.Auction.Description,

                // 2. Calculate remaining seconds using Indian Time
                TimeRemainingSeconds = w.Auction.Status == AuctionStatus.Live
                                        ? (w.Auction.EndDate - TimeHelper.Now()).TotalSeconds
                                        : null,

                CreatedAt = w.Auction.CreatedAt
            });
        }
        if(result.Count()!=0)
        result=result.OrderBy(x=>x.StartDate).ToList();
        return ServiceResult<List<AuctionResponse>>.Ok(result);
    }

    // BACKUP MECHANISM: Get highest bid from Redis, fallback to Database if Redis fails
    private async Task<HighestBidCacheDto?> GetHighestBidWithFallbackAsync(int auctionId)
    {
        try
        {
            // Try Redis first (fast cache)
            var redisResult = await _redis.GetHighestBidAsync(auctionId);
            if (redisResult != null)
                return redisResult;

            // If Redis is empty, fallback to database
            _logger.LogInformation("Highest bid not found in Redis for auction {AuctionId}. Falling back to database.", auctionId);
            var dbBid = await _bidRepo.GetHighestBidAsync(auctionId);
            
            if (dbBid != null)
            {
                // Repopulate Redis cache for future requests
                var cacheDto = new HighestBidCacheDto
                {
                    Amount = dbBid.Amount,
                    BidId = dbBid.Id,
                    PlacedAt = dbBid.PlacedAt,
                    UserId = dbBid.UserId
                };
                
                await _redis.SetHighestBidAsync(auctionId, cacheDto);
                return cacheDto;
            }

            return null;
        }
        catch (Exception ex)
        {
            // If Redis operation fails, fall back to database
            _logger.LogWarning(ex, "Redis GetHighestBidAsync failed for auction {AuctionId}. Falling back to database.", auctionId);
            var dbBid = await _bidRepo.GetHighestBidAsync(auctionId);
            
            if (dbBid != null)
            {
                return new HighestBidCacheDto
                {
                    Amount = dbBid.Amount,
                    BidId = dbBid.Id,
                    PlacedAt = dbBid.PlacedAt,
                    UserId = dbBid.UserId
                };
            }

            return null;
        }
    }
}
