using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AUCTION.Data.Repositories;

public class AuctionRepository : IAuctionRepository
{
    private readonly AuctionDbContext _ctx;
    public AuctionRepository(AuctionDbContext ctx) => _ctx = ctx;

    public Task<Auction?> GetByIdAsync(int id)
        => _ctx.Auctions.FirstOrDefaultAsync(x => x.Id == id);
    
    public Task<Auction?> GetByIdWithBidsAsync(int id)
        => _ctx.Auctions
               .Include(x => x.Bids.OrderByDescending(b => b.Amount).Take(10))
               .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<(List<Auction> Items, int Total)> GetAllAsync(AuctionFilterRequest filter)
    {
        var q = _ctx.Auctions.AsQueryable();

        if (filter.Status.HasValue)   q = q.Where(x => x.Status == filter.Status.Value);
        if (filter.MinPrice.HasValue) q = q.Where(x => x.StartingPrice >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue) q = q.Where(x => x.StartingPrice <= filter.MaxPrice.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, total);
    }
    
    // BUG FIX: Was marked async but had no await — this causes a compiler warning
    // and creates a needless state machine. It's purely synchronous.
    public Task<Auction> removeAuction(Auction auction)
    {
        var entity = _ctx.Auctions.Remove(auction).Entity;
        return Task.FromResult(entity);
    }

    public Task<List<Auction>> GetByUserIdAsync(int userId)
        => _ctx.Auctions.Where(x => x.CreatedByUserId == userId).ToListAsync();

    public Task<List<Auction>> GetLiveAuctionsDueToCloseAsync()
        => _ctx.Auctions
               .Where(x => x.Status == AuctionStatus.Live && x.EndDate <= DateTime.UtcNow)
               .ToListAsync();

    public Task<List<Auction>> GetUpcomingAuctionsDueToStartAsync()
        => _ctx.Auctions
               .Where(x => x.Status == AuctionStatus.Upcoming && x.StartDate <= DateTime.UtcNow)
               .ToListAsync();

    public Task<List<Auction>> GetLiveAuctionsEndingSoonAsync(int withinMinutes)
    { 
        var threshold = DateTime.UtcNow.AddMinutes(withinMinutes);
        return _ctx.Auctions
                   .Where(x => x.Status == AuctionStatus.Live
                             && x.EndDate > DateTime.UtcNow
                             && x.EndDate <= threshold)
                   .ToListAsync();
    }

    public async Task AddAsync(Auction auction)   => await _ctx.Auctions.AddAsync(auction);
    public Task UpdateAsync(Auction auction)      { _ctx.Auctions.Update(auction); return Task.CompletedTask; }
    public Task SaveChangesAsync()                => _ctx.SaveChangesAsync();

    public async Task<Auction?> GetbyProductId(int productId)
    {
        return await _ctx.Auctions.Where(x=>x.ProductId==productId).FirstOrDefaultAsync();
    }
}

public class BidRepository : IBidRepository
{
    private readonly AuctionDbContext _ctx;
    public BidRepository(AuctionDbContext ctx) => _ctx = ctx;

    public Task<Bid?> GetByIdAsync(int id)
        => _ctx.Bids.FirstOrDefaultAsync(x => x.Id == id);

    public Task<List<Bid>> GetByAuctionIdAsync(int auctionId, int page, int pageSize)
        => _ctx.Bids
               .Where(x => x.AuctionId == auctionId)
               .OrderByDescending(x => x.PlacedAt)
               .Skip((page - 1) * pageSize)
               .Take(pageSize)
               .ToListAsync();

    public Task<List<Bid>> GetByUserAndAuctionAsync(int userId, int auctionId)
        => _ctx.Bids
               .Where(x => x.UserId == userId && x.AuctionId == auctionId)
               .OrderByDescending(x => x.PlacedAt)
               .ToListAsync();

    public Task<List<Bid>> GetByUserIdAsync(int userId)
        => _ctx.Bids
               .Include(x => x.Auction)
               .Where(x => x.UserId == userId)
               .OrderByDescending(x => x.PlacedAt)
               .ToListAsync();

    public Task<Bid?> GetHighestBidAsync(int auctionId)
        => _ctx.Bids
               .Where(x => x.AuctionId == auctionId && x.Status == BidStatus.Active)
               .OrderByDescending(x => x.Amount)
               .FirstOrDefaultAsync();

    public Task<int> GetBidCountAsync(int auctionId)
        => _ctx.Bids.CountAsync(x => x.AuctionId == auctionId);

    public async Task AddAsync(Bid bid) => await _ctx.Bids.AddAsync(bid);

    public Task UpdateRangeAsync(IEnumerable<Bid> bids)
    {
        _ctx.Bids.UpdateRange(bids);
        return Task.CompletedTask;
    }
    

    public Task SaveChangesAsync() => _ctx.SaveChangesAsync();
}

public class WatchlistRepository : IWatchlistRepository
{
    private readonly AuctionDbContext _ctx;
    public WatchlistRepository(AuctionDbContext ctx) => _ctx = ctx;

    public Task<Watchlist?> GetAsync(int userId, int auctionId)
        => _ctx.Watchlists.FirstOrDefaultAsync(x => x.UserId == userId && x.AuctionId == auctionId);

    public Task<List<Watchlist>> GetByUserIdAsync(int userId)
        => _ctx.Watchlists.Include(x => x.Auction).Where(x => x.UserId == userId).ToListAsync();

    public Task<List<int>> GetWatcherUserIdsAsync(int auctionId)
        => _ctx.Watchlists.Where(x => x.AuctionId == auctionId).Select(x => x.UserId).ToListAsync();

    public Task<int> GetWatcherCountAsync(int auctionId)
        => _ctx.Watchlists.CountAsync(x => x.AuctionId == auctionId);

    public async Task AddAsync(Watchlist watchlist) => await _ctx.Watchlists.AddAsync(watchlist);

    public Task RemoveAsync(Watchlist watchlist)
    {
        _ctx.Watchlists.Remove(watchlist);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => _ctx.SaveChangesAsync();
}
