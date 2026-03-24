using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;

namespace AUCTION.Data.Repositories.Interfaces;

public interface IAuctionRepository
{
    Task<Auction?> GetByIdAsync(int id);
    Task<Auction?> GetByIdWithBidsAsync(int id);
    Task<(List<Auction> Items, int Total)> GetAllAsync(AuctionFilterRequest filter);
    Task<List<Auction>> GetByUserIdAsync(int userId);
    Task<List<Auction>> GetLiveAuctionsDueToCloseAsync();
    Task<List<Auction>> GetUpcomingAuctionsDueToStartAsync();
    Task<List<Auction>> GetLiveAuctionsEndingSoonAsync(int withinMinutes);
    Task AddAsync(Auction auction);
    Task UpdateAsync(Auction auction);
    Task<Auction?> GetbyProductId(int productId);
    Task SaveChangesAsync();
    Task<Auction> removeAuction(Auction auction);
    Task<Auction?> getHighestBidder(int auctionId);
}

public interface IBidRepository
{
    Task<Bid?> GetByIdAsync(int id);
    Task<List<Bid>> GetByAuctionIdAsync(int auctionId, int page, int pageSize);
    Task<List<Bid>> GetByUserAndAuctionAsync(int userId, int auctionId);
    Task<List<Bid>> GetByUserIdAsync(int userId);
    Task<(List<Auction> Items, int Total)> GetParticipatedAuctionsAsync(int userId, AuctionFilterRequest filter);
    Task<Bid?> GetHighestBidAsync(int auctionId);
    Task<int> GetBidCountAsync(int auctionId);
    Task AddAsync(Bid bid);
    Task UpdateRangeAsync(IEnumerable<Bid> bids);
    Task SaveChangesAsync();
}

public interface IWatchlistRepository
{
    Task<Watchlist?> GetAsync(int userId, int auctionId);
    Task<List<Watchlist>> GetByUserIdAsync(int userId);
    Task<List<int>> GetWatcherUserIdsAsync(int auctionId);
    Task<int> GetWatcherCountAsync(int auctionId);
    Task AddAsync(Watchlist watchlist);
    Task RemoveAsync(Watchlist watchlist);
    Task SaveChangesAsync();
}
