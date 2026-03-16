using AUCTION.Data.Dto.Response;
using StackExchange.Redis;
using System.Text.Json;

namespace AUCTION.Services;

public interface IRedisService
{
    Task SetHighestBidAsync(int auctionId, HighestBidCacheDto bid);
    Task<HighestBidCacheDto?> GetHighestBidAsync(int auctionId);
    Task<bool> SetBidLockAsync(int auctionId, int userId, TimeSpan expiry);
    Task ReleaseBidLockAsync(int auctionId, int userId);
    Task IncrementViewerCountAsync(int auctionId);
    Task DecrementViewerCountAsync(int auctionId);
    Task<long> GetViewerCountAsync(int auctionId);
    Task DeleteAuctionCacheAsync(int auctionId);
}

public class RedisService : IRedisService
{
    private readonly IDatabase _db;

    public RedisService(IConnectionMultiplexer redis)
        => _db = redis.GetDatabase();

    public async Task SetHighestBidAsync(int auctionId, HighestBidCacheDto bid)
        => await _db.StringSetAsync(
               $"auction:{auctionId}:highest_bid",
               JsonSerializer.Serialize(bid),
               TimeSpan.FromHours(24));

    public async Task<HighestBidCacheDto?> GetHighestBidAsync(int auctionId)
    {
        var val = await _db.StringGetAsync($"auction:{auctionId}:highest_bid");
        return val.IsNullOrEmpty ? null : JsonSerializer.Deserialize<HighestBidCacheDto>(val.ToString());
    }

    // Returns true if lock was acquired (no concurrent bid in flight)
    public async Task<bool> SetBidLockAsync(int auctionId, int userId, TimeSpan expiry)
        => await _db.StringSetAsync(
               $"auction:{auctionId}:lock:{userId}", "1", expiry, When.NotExists);

    public async Task ReleaseBidLockAsync(int auctionId, int userId)
        => await _db.KeyDeleteAsync($"auction:{auctionId}:lock:{userId}");

    public Task IncrementViewerCountAsync(int auctionId)
        => _db.StringIncrementAsync($"auction:{auctionId}:viewers");

    public Task DecrementViewerCountAsync(int auctionId)
        => _db.StringDecrementAsync($"auction:{auctionId}:viewers");

    public async Task<long> GetViewerCountAsync(int auctionId)
    {
        var val = await _db.StringGetAsync($"auction:{auctionId}:viewers");
        return val.IsNullOrEmpty ? 0 : (long)val;
    }

    public async Task DeleteAuctionCacheAsync(int auctionId)
    {
        await _db.KeyDeleteAsync($"auction:{auctionId}:highest_bid");
        await _db.KeyDeleteAsync($"auction:{auctionId}:viewers");
    }
}
