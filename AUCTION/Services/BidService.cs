using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using AUCTION.Services.Interfaces;
using MassTransit;
using Messaging.Contracts;
using Microsoft.AspNetCore.SignalR;

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
        _bidRepo     = bidRepo;
        _redis       = redis;
        _publish     = publish;
        _hub         = hub;
        _logger      = logger;
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

            // 5. Validate amount against current highest bid (served from Redis cache)
            var currentHighest = await _redis.GetHighestBidAsync(auctionId);
            var minimumBid     = currentHighest != null
                ? currentHighest.Amount + auction.MinBidIncrement
                : auction.StartingPrice;

            if (request.Amount < minimumBid)
                return ServiceResult<BidResponse>.Fail(
                    $"Bid must be at least {minimumBid:F2}. Current: {(currentHighest?.Amount ?? auction.StartingPrice):F2}");

            // 6. Save new bid
            var newBid = new Bid
            {
                AuctionId = auctionId,
                UserId    = userId,
                Amount    = request.Amount,
                Status    = BidStatus.Active,
                IpAddress = ipAddress
            };
            await _bidRepo.AddAsync(newBid);

            // 7. Mark previous highest as outbid
            int? previousBidderId = null;
            decimal? previousAmount = null;

            if (currentHighest != null)
            {
                previousBidderId = currentHighest.UserId;
                previousAmount   = currentHighest.Amount;

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
                BidId    = newBid.Id,
                UserId   = userId,
                Amount   = request.Amount,
                PlacedAt = newBid.PlacedAt
            });

            

            // 9. Auto-extend: if bid placed in last 2 minutes, extend by 2 more minutes
            if (auction.EndDate - DateTime.UtcNow <= TimeSpan.FromMinutes(2) && auction.Extension <= auction.maxExtension)
            {
                auction.EndDate   = auction.EndDate.AddMinutes(2);
                auction.UpdatedAt = DateTime.UtcNow;
                auction.Extension++;
                await _auctionRepo.UpdateAsync(auction);
                await _auctionRepo.SaveChangesAsync();
                await _hub.BroadcastTimerTick(auction.Id,(auction.EndDate - DateTime.UtcNow).TotalSeconds);
                await _hub.AuctionMessage(auction.Id,"Bid Placed Last 2 minituse auction is Extende by 2 minitues More");
                _logger.LogInformation("Auction {AuctionId} auto-extended by 2 minutes", auctionId);
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
                bidId        = newBid.Id,
                maskedBidder = MaskUserId(userId),
                amount       = request.Amount,
                placedAt     = newBid.PlacedAt,
                newEndDate   = auction.EndDate
            });

            _logger.LogInformation("Bid {BidId} placed: {Amount} on auction {AuctionId} by user {UserId}",
                newBid.Id, request.Amount, auctionId, userId);

            return ServiceResult<BidResponse>.Created(new BidResponse
            {
                Id           = newBid.Id,
                AuctionId    = auctionId,
                MaskedBidder = MaskUserId(userId),
                Amount       = request.Amount,
                Status       = BidStatus.Active.ToString(),
                PlacedAt     = newBid.PlacedAt
            }, "Bid placed successfully");
        }
        finally
        {
            // Always release the lock, even if something throws
            await _redis.ReleaseBidLockAsync(auctionId, userId);
        }
    }

    public async Task<ServiceResult<PagedResponse<BidResponse>>> GetBidHistoryAsync(
        int auctionId, int page, int pageSize)
    {
        var auction = await _auctionRepo.GetByIdAsync(auctionId);
        if (auction == null)
            return ServiceResult<PagedResponse<BidResponse>>.NotFound();

        var bids  = await _bidRepo.GetByAuctionIdAsync(auctionId, page, pageSize);
        var total = await _bidRepo.GetBidCountAsync(auctionId);

        return ServiceResult<PagedResponse<BidResponse>>.Ok(new PagedResponse<BidResponse>
        {
            Items = bids.Select(b => new BidResponse
            {
                Id           = b.Id,
                AuctionId    = b.AuctionId,
                MaskedBidder = MaskUserId(b.UserId),
                Amount       = b.Amount,
                Status       = b.Status.ToString(),
                PlacedAt     = b.PlacedAt
            }).ToList(),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        });
    }

    public async Task<ServiceResult<HighestBidCacheDto?>> GetHighestBidAsync(int auctionId)
    {
        var auction = await _auctionRepo.GetByIdAsync(auctionId);
        if (auction == null)
            return ServiceResult<HighestBidCacheDto?>.NotFound();

        var cached = await _redis.GetHighestBidAsync(auctionId);

        // Cache miss → fall back to DB and repopulate cache
        if (cached == null)
        {
            var dbBid = await _bidRepo.GetHighestBidAsync(auctionId);
            if (dbBid != null)
            {
                cached = new HighestBidCacheDto
                {
                    BidId    = dbBid.Id,
                    UserId   = dbBid.UserId,
                    Amount   = dbBid.Amount,
                    PlacedAt = dbBid.PlacedAt
                };
                await _redis.SetHighestBidAsync(auctionId, cached);
            }
        }

        return ServiceResult<HighestBidCacheDto?>.Ok(cached);
    }

    public async Task<ServiceResult<List<MyBidResponse>>> GetMyBidsAsync(int auctionId, int userId)
    {
        var auction = await _auctionRepo.GetByIdAsync(auctionId);
        if (auction == null)
            return ServiceResult<List<MyBidResponse>>.NotFound();

        var bids    = await _bidRepo.GetByUserAndAuctionAsync(userId, auctionId);
        var highest = await _redis.GetHighestBidAsync(auctionId);

        return ServiceResult<List<MyBidResponse>>.Ok(
            bids.Select(b => new MyBidResponse
            {
                Id                = b.Id,
                AuctionId         = b.AuctionId,
                UserId            = b.UserId,
                MaskedBidder      = MaskUserId(b.UserId),
                Amount            = b.Amount,
                Status            = b.Status.ToString(),
                PlacedAt          = b.PlacedAt,
                IsCurrentlyWinning = highest?.UserId == userId && b.Amount == highest?.Amount
            }).ToList());
    }

    private static string MaskUserId(int userId)
    {
        var s = userId.ToString();
        return s.Length <= 2 ? "***" : $"{s[0]}***{s[^1]}";
    }
}
