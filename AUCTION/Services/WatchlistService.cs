using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Services.Interfaces;

namespace AUCTION.Services;

public class WatchlistService : IWatchlistService
{
    private readonly IAuctionRepository   _auctionRepo;
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IBidRepository       _bidRepo;
    private readonly IRedisService        _redis;

    public WatchlistService(
        IAuctionRepository auctionRepo,
        IWatchlistRepository watchlistRepo,
        IBidRepository bidRepo,
        IRedisService redis)
    {
        _auctionRepo   = auctionRepo;
        _watchlistRepo = watchlistRepo;
        _bidRepo       = bidRepo;
        _redis         = redis;
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

    public async Task<ServiceResult<List<AuctionResponse>>> GetWatchedAuctionsAsync(int userId)
    {
        var entries = await _watchlistRepo.GetByUserIdAsync(userId);
        var result  = new List<AuctionResponse>();

        foreach (var w in entries)
        {
            var highest  = await _redis.GetHighestBidAsync(w.AuctionId);
            var bidCount = await _bidRepo.GetBidCountAsync(w.AuctionId);

            result.Add(new AuctionResponse
            {
                Id                   = w.Auction.Id,
                ProductId            = w.Auction.ProductId,
                CreatedByUserId      = w.Auction.CreatedByUserId,
                StartingPrice        = w.Auction.StartingPrice,
                ReservePrice         = w.Auction.ReservePrice,
                MinBidIncrement      = w.Auction.MinBidIncrement,
                StartDate            = w.Auction.StartDate,
                EndDate              = w.Auction.EndDate,
                Status               = w.Auction.Status.ToString(),
                CurrentHighestBid    = highest?.Amount ?? w.Auction.StartingPrice,
                TotalBids            = bidCount,
                TimeRemainingSeconds = w.Auction.Status == AuctionStatus.Live
                                        ? (w.Auction.EndDate - DateTime.UtcNow).TotalSeconds
                                        : null,
                CreatedAt            = w.Auction.CreatedAt
            });
        }

        return ServiceResult<List<AuctionResponse>>.Ok(result);
    }
}
