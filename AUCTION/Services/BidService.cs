using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using AUCTION.Services.Interfaces;
using MassTransit;
using Messaging.Contracts;
namespace AUCTION.Services;

public class BidService : IBidService
{
    private readonly IAuctionRepository _auctionRepo;
    private readonly IBidRepository _bidRepo;
    private readonly IRedisService _redis;
    private readonly IPublishEndpoint _publish;
    private readonly IAuctionHubService _hub;
    private readonly ILogger<BidService> _logger;

    public BidService(
        IAuctionRepository auctionRepo,
        IBidRepository bidRepo,
        IRedisService redis,
        IPublishEndpoint publish,
        IAuctionHubService hub,
        ILogger<BidService> logger)
    {
        _auctionRepo = auctionRepo;
        _bidRepo = bidRepo;
        _redis = redis;
        _publish = publish;
        _hub = hub;
        _logger = logger;
    }

    public async Task<ServiceResult<BidResponse>> PlaceBidAsync(
        int auctionId, PlaceBidRequest request, int userId, string? ipAddress)
    {
        // 1. Acquire Redis lock — stops two simultaneous bids from same user
        var lockAcquired = await _redis.SetBidLockAsync(auctionId, userId, TimeSpan.FromSeconds(5));
        if (!lockAcquired)
            return ServiceResult<BidResponse>.Fail("Please wait before placing another bid");

        try
        {
            // 2. Load auction
            var auction = await _auctionRepo.GetByIdAsync(auctionId);
            if (auction == null)
                return ServiceResult<BidResponse>.NotFound("Auction not found");

            // 3. Must be live
            if (auction.Status != AuctionStatus.Live)
                return ServiceResult<BidResponse>.Fail("Auction is not currently live");

            // 4. Creator cannot bid on their own auction
            if (auction.CreatedByUserId == userId)
                return ServiceResult<BidResponse>.Forbidden("You cannot bid on your own auction");

            // 5. Validate amount against current highest bid (with Redis fallback to DB)
            var currentHighest = await GetHighestBidWithFallbackAsync(auctionId);
            var minimumBid = currentHighest != null
                ? currentHighest.Amount + auction.MinBidIncrement
                : auction.StartingPrice;

            if (request.Amount < minimumBid)
                return ServiceResult<BidResponse>.Fail(
                    $"Bid must be at least {minimumBid:F2}. Current: {(currentHighest?.Amount ?? auction.StartingPrice):F2}");

            // 6. Save new bid
            var newBid = new Bid
            {
                AuctionId = auctionId,
                UserId = userId,
                Amount = request.Amount,
                Status = BidStatus.Active,
                IpAddress = ipAddress
            };
            await _bidRepo.AddAsync(newBid);

            // 7. Mark previous highest as outbid
            int? previousBidderId = null;
            decimal? previousAmount = null;

            if (currentHighest != null)
            {
                previousBidderId = currentHighest.UserId;
                previousAmount = currentHighest.Amount;

                var prevBid = await _bidRepo.GetByIdAsync(currentHighest.BidId);
                if (prevBid != null)
                {
                    prevBid.Status = BidStatus.Outbid;
                    await _bidRepo.UpdateRangeAsync(new[] { prevBid });
                }
            }

            await _bidRepo.SaveChangesAsync();

            // 8. Update Redis cache with new highest bid
            await _redis.SetHighestBidAsync(auctionId, new HighestBidCacheDto
            {
                BidId = newBid.Id,
                UserId = userId,
                Amount = request.Amount,
                PlacedAt = newBid.PlacedAt
            });



            // 9. Auto-extend: if bid placed in last 2 minutes, extend by 2 more minutes
            // 1. Get the current Indian Time
            

            // 2. Perform the logic using indianNow
            if (auction.EndDate - TimeHelper.Now() <= TimeSpan.FromMinutes(2) && auction.Extension <= auction.maxExtension)
            {
                // Extend the end date by 2 minutes
                auction.EndDate = auction.EndDate.AddMinutes(2);

                // Set UpdatedAt to current Indian Time
                auction.UpdatedAt = TimeHelper.Now();

                auction.Extension++;

                await _auctionRepo.UpdateAsync(auction);
                await _auctionRepo.SaveChangesAsync();

                // Broadcast the new remaining seconds based on Indian Time
                double remainingSeconds = (auction.EndDate - TimeHelper.Now()).TotalSeconds;
                await _hub.BroadcastTimerTick(auction.Id, remainingSeconds);

                await _hub.AuctionMessage(auction.Id, "Bid Placed in last 2 minutes. Auction extended by 2 minutes!");

                _logger.LogInformation("Auction {AuctionId} auto-extended by 2 minutes at {Time}", auction.Id, TimeHelper.Now());
            }


            // 10. Publish event via MassTransit → RabbitMQ
            await _publish.Publish(new AuctionBidPlaced(
                auctionId,
                newBid.Id,
                userId,
                request.Amount,
                previousBidderId,
                previousAmount,
                newBid.PlacedAt));

            // 11. Broadcast to all users in the auction room via SignalR
            await _hub.BroadcastBidPlaced(auctionId, new
            {
                bidId = newBid.Id,
                maskedBidder = MaskUserId(userId),
                amount = request.Amount,
                placedAt = newBid.PlacedAt,
                newEndDate = auction.EndDate
            });

            _logger.LogInformation("Bid {BidId} placed: {Amount} on auction {AuctionId} by user {UserId}",
                newBid.Id, request.Amount, auctionId, userId);

            return ServiceResult<BidResponse>.Created(new BidResponse
            {
                Id = newBid.Id,
                AuctionId = auctionId,
                MaskedBidder = MaskUserId(userId),
                Amount = request.Amount,
                Status = BidStatus.Active.ToString(),
                PlacedAt = newBid.PlacedAt
            }, "Bid placed successfully");
        }
        finally
        {
            // Always release the lock, even if something throws
            await _redis.ReleaseBidLockAsync(auctionId, userId);
        }
    }

    public async Task<ServiceResult<PagedResponse<BidResponse>>> GetBidHistoryAsync(
        int auctionId, int page, int pageSize,bool mine,int userId)
    {
        var auction = await _auctionRepo.GetByIdAsync(auctionId);
        if (auction == null)
            return ServiceResult<PagedResponse<BidResponse>>.NotFound();

        var bids = await _bidRepo.GetByAuctionIdAsync(auctionId, page, pageSize);
        var total = await _bidRepo.GetBidCountAsync(auctionId);
        if(mine)
        bids =  bids.Where(x=>x.UserId==userId).ToList();
        return ServiceResult<PagedResponse<BidResponse>>.Ok(new PagedResponse<BidResponse>
        {
            Items = bids.Select(b => new BidResponse
            {
                Id = b.Id,
                AuctionId = b.AuctionId,
                MaskedBidder = MaskUserId(b.UserId),
                Amount = b.Amount,
                Status = b.Status.ToString(),
                PlacedAt = b.PlacedAt
            }).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<ServiceResult<HighestBidCacheDto?>> GetHighestBidAsync(int auctionId)
    {
        var auction = await _auctionRepo.GetByIdAsync(auctionId);
        if (auction == null)
            return ServiceResult<HighestBidCacheDto?>.NotFound();

        var cached = await GetHighestBidWithFallbackAsync(auctionId);
        return ServiceResult<HighestBidCacheDto?>.Ok(cached);
    }


    // BACKUP MECHANISM: Get highest bid from Redis, fallback to Database if Redis fails or empty
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
                    BidId = dbBid.Id,
                    UserId = dbBid.UserId,
                    Amount = dbBid.Amount,
                    PlacedAt = dbBid.PlacedAt
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
                    BidId = dbBid.Id,
                    UserId = dbBid.UserId,
                    Amount = dbBid.Amount,
                    PlacedAt = dbBid.PlacedAt
                };
            }

            return null;
        }
    }

    private static string MaskUserId(int userId)
    {
        var s = userId.ToString();
        return s.Length <= 2 ? "***" : $"{s[0]}***{s[^1]}";
    }
}
