using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace AUCTION.Data.Repositories;

public class AuctionRepository : IAuctionRepository
{
    private readonly AuctionDbContext _ctx;
    public AuctionRepository(AuctionDbContext ctx) => _ctx = ctx;

    private DateTime IndianNow => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow, 
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));

    public Task<Auction?> GetByIdAsync(int id)
        => _ctx.Auctions.FirstOrDefaultAsync(x => x.Id == id);

    public Task<Auction?> GetByIdWithBidsAsync(int id)
        => _ctx.Auctions
               .Include(x => x.Bids.OrderByDescending(b => b.Amount).Take(10))
               .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<(List<Auction> Items, int Total)> GetAllAsync(AuctionFilterRequest filter)
    {
        var q = _ctx.Auctions.AsQueryable();
        if (filter.mine) q = q.Where(x => x.CreatedByUserId == filter.mineid);
        if (filter.Status.HasValue) q = q.Where(x => x.Status == filter.Status.Value);
        if (filter.MinPrice.HasValue) q = q.Where(x => x.StartingPrice >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue) q = q.Where(x => x.StartingPrice <= filter.MaxPrice.Value);
        if(filter.productId.HasValue) q=q.Where(x=>x.ProductId==filter.productId);
        if (!string.IsNullOrWhiteSpace(filter.name)) q = q.Where(x => EF.Functions.Like(x.ProductName, $"%{filter.name}%"));
       
        var total = await q.CountAsync();
        var items = await q
            .OrderBy(x => x.StartDate)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<Auction> removeAuction(Auction auction)
    {
        var entity = _ctx.Auctions.Remove(auction).Entity;
        return Task.FromResult(entity);
    }

    public Task<List<Auction>> GetByUserIdAsync(int userId)
        => _ctx.Auctions.Where(x => x.CreatedByUserId == userId).OrderBy(X=>X.StartDate).ToListAsync();

    public Task<List<Auction>> GetLiveAuctionsDueToCloseAsync()
        => _ctx.Auctions
               .Where(x => x.Status == AuctionStatus.Live && x.EndDate <= IndianNow)
               .ToListAsync();

    public Task<List<Auction>> GetUpcomingAuctionsDueToStartAsync()
        => _ctx.Auctions
               .Where(x => x.Status == AuctionStatus.Upcoming && x.StartDate <= IndianNow)
               .ToListAsync();

    public Task<List<Auction>> GetLiveAuctionsEndingSoonAsync(int withinMinutes)
    {
        var now = IndianNow;
        var threshold = now.AddMinutes(withinMinutes);
        return _ctx.Auctions
                   .Where(x => x.Status == AuctionStatus.Live
                             && x.EndDate > now
                             && x.EndDate <= threshold)
                   .ToListAsync();
    }
    public async Task<Auction?> getHighestBidder(int auctionId)
    {
        return await _ctx.Auctions
                   .Include(x => x.Bids.OrderByDescending(b => b.Amount).Take(1))
                   .Where(x => x.Id == auctionId).FirstOrDefaultAsync();
    }

    public async Task AddAsync(Auction auction) => await _ctx.Auctions.AddAsync(auction);
    public Task UpdateAsync(Auction auction) { _ctx.Auctions.Update(auction); return Task.CompletedTask; }
    public Task SaveChangesAsync() => _ctx.SaveChangesAsync();

    public async Task<Auction?> GetbyProductId(int productId)
        => await _ctx.Auctions.Include(x=>x.Bids).Where(x => x.ProductId == productId).FirstOrDefaultAsync();
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

    public async Task<(List<Auction> Items, int Total)> GetParticipatedAuctionsAsync(int userId, AuctionFilterRequest filter)
    {
        var q = _ctx.Bids
            .Where(x => x.UserId == userId)
            .Select(x => x.Auction)
            .Distinct()
            .AsQueryable();

        // Apply filters
        if (filter.Status.HasValue) 
            q = q.Where(x => x.Status == filter.Status.Value);
        
        if (filter.MinPrice.HasValue) 
            q = q.Where(x => x.StartingPrice >= filter.MinPrice.Value);
        
        if (filter.MaxPrice.HasValue) 
            q = q.Where(x => x.StartingPrice <= filter.MaxPrice.Value);
        
        if (!string.IsNullOrWhiteSpace(filter.name)) 
            q = q.Where(x => EF.Functions.Like(x.ProductName, $"%{filter.name}%"));

        if (filter.FilterStartDate.HasValue)
            q = q.Where(x => x.StartDate >= filter.FilterStartDate.Value);

        if (filter.FilterEndDate.HasValue)
            q = q.Where(x => x.EndDate <= filter.FilterEndDate.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, total);
    }

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
// i changed here if there is any error comes resolve here okay so do this complete fast 
    public Task<List<Watchlist>> GetByUserIdAsync(int userId)
        => _ctx.Watchlists.Include(x => x.Auction).Where(x => x.UserId == userId).OrderBy(x=>x.Auction.StartDate).ToListAsync();

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
