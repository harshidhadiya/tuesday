using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using AUCTION.Services.Interfaces;
using MassTransit;
using Messaging.Contracts;
namespace AUCTION.Services;

public class AuctionService : IAuctionService
{
  
    private readonly IAuctionRepository _auctionRepo;
    private readonly IBidRepository _bidRepo;
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IRedisService _redis;
    private readonly IPublishEndpoint _publish;
    private readonly IAuctionHubService _hub;
    private readonly ILogger<AuctionService> _logger;

    public AuctionService(
        IAuctionRepository auctionRepo,
        IBidRepository bidRepo,
        IWatchlistRepository watchlistRepo,
        IRedisService redis,
        IPublishEndpoint publish,
        IAuctionHubService hub,
        ILogger<AuctionService> logger)
    {
        _auctionRepo = auctionRepo;
        _bidRepo = bidRepo;
        _watchlistRepo = watchlistRepo;
        _redis = redis;
        _publish = publish;
        _hub = hub;
        _logger = logger;
    }

    //  Get single auction (with live Redis state) 

    public async Task<ServiceResult<AuctionDetailResponse>> GetAuctionAsync(int auctionId,int userID)
    {
        var auction = await _auctionRepo.GetByIdWithBidsAsync(auctionId);
        if (auction == null)
            return ServiceResult<AuctionDetailResponse>.NotFound("Auction not found");

        var highest = await GetHighestBidWithFallbackAsync(auctionId);
        var bidCount = await _bidRepo.GetBidCountAsync(auctionId);
        var watcherCount = await _watchlistRepo.GetWatcherCountAsync(auctionId);
        var viewerCount = await _redis.GetViewerCountAsync(auctionId);

        return ServiceResult<AuctionDetailResponse>.Ok(
            MapToDetail(auction, highest, bidCount, watcherCount, viewerCount,auction.CreatedByUserId==userID || auction.WinnerUserId.HasValue && auction.WinnerUserId==userID));
    }

    //  Get all auctions (paged + filtered) 

    public async Task<ServiceResult<PagedResponse<AuctionResponse>>> GetAllAuctionsAsync(AuctionFilterRequest filter)
    {
        var (items, total) = await _auctionRepo.GetAllAsync(filter);

        var responses = new List<AuctionResponse>();
        foreach (var a in items)
        {
            var highest = await GetHighestBidWithFallbackAsync(a.Id);
            var bidCount = await _bidRepo.GetBidCountAsync(a.Id);
            responses.Add(MapToResponse(a, highest, bidCount,filter.mineid));
        }

        return ServiceResult<PagedResponse<AuctionResponse>>.Ok(new PagedResponse<AuctionResponse>
        {
            Items = responses,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        });
    }


    //  Update auction (only before it goes live) 

    public async Task<ServiceResult<AuctionResponse>> UpdateAuctionAsync(
        int auctionId, UpdateAuctionRequest request, int userId)
    {
        var auction = await _auctionRepo.GetByIdAsync(auctionId);
        if (auction == null)
            return ServiceResult<AuctionResponse>.NotFound();

        if (auction.CreatedByUserId != userId)
            return ServiceResult<AuctionResponse>.Forbidden("You can only edit your own auctions");

        if (auction.Status != AuctionStatus.Upcoming && auction.Status != AuctionStatus.Ended && auction.Status!=AuctionStatus.Verified && auction.Status!=AuctionStatus.Cancelled)
            return ServiceResult<AuctionResponse>.Fail("Cannot edit an auction that has already started");

        var bidCount = await _bidRepo.GetBidCountAsync(auctionId);
        if (bidCount > 0 && request.StartingPrice.HasValue)
            return ServiceResult<AuctionResponse>.Fail("Cannot change starting price after bids have been placed");
        if(auction.Status is AuctionStatus.Verified or AuctionStatus.Cancelled && (request.StartDate.HasValue ||auction.StartDate>TimeHelper.Now())) auction.Status=AuctionStatus.Upcoming;
        if (request.StartingPrice.HasValue) auction.StartingPrice = request.StartingPrice.Value;
        if (request.ReservePrice.HasValue) auction.ReservePrice = request.ReservePrice.Value;
        if (request.MinBidIncrement.HasValue) auction.MinBidIncrement = request.MinBidIncrement.Value;
        if (request.StartDate.HasValue) auction.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) auction.EndDate = request.EndDate.Value;
        auction.UpdatedAt = TimeHelper.Now();
        if(request.StartDate.HasValue || request.EndDate.HasValue){
        await _hub.BroadcastAuctionUpdated(auctionId, new { StartDate = auction.StartDate, EndDate = auction.EndDate,status=auction.Status.ToString() });
        await _publish.Publish (new ProductAddAuctionDate(productId:auction.ProductId,StartDate:auction.StartDate,EndDate:auction.EndDate));
}
        await _auctionRepo.UpdateAsync(auction);
        await _auctionRepo.SaveChangesAsync();

        return ServiceResult<AuctionResponse>.Ok(MapToResponse(auction, null, bidCount,userId));
    }

    //  Cancel auction 

    public async Task<ServiceResult<bool>> CancelAuctionAsync(int auctionId, int userId)
    {
        var auction = await _auctionRepo.GetByIdAsync(auctionId);
        if (auction == null) return ServiceResult<bool>.NotFound();

        if (auction.CreatedByUserId != userId)
            return ServiceResult<bool>.Forbidden();

        if (auction.Status is AuctionStatus.Live or AuctionStatus.Ended)
            return ServiceResult<bool>.Fail("Cannot cancel an auction that is live or already ended");

        auction.Status = AuctionStatus.Cancelled;
        auction.UpdatedAt = TimeHelper.Now();
        await _auctionRepo.UpdateAsync(auction);
        await _auctionRepo.SaveChangesAsync();

        await _publish.Publish(new AuctionCancelled(
            auction.Id, auction.ProductId, "Cancelled by owner"));
            await _publish.Publish (new ProductAddAuctionDate(productId:auction.ProductId,StartDate:null,EndDate:null));

        await _hub.BroadcastAuctionClosed(auctionId, new { status = "cancelled" });

        return ServiceResult<bool>.Ok(true, "Auction cancelled");
    }

    //  My auctions (dashboard) 

    public async Task<ServiceResult<List<AuctionResponse>>> GetMyCreatedAuctionsAsync(int userId)
    {
        var auctions = await _auctionRepo.GetByUserIdAsync(userId);
        var result = new List<AuctionResponse>();
        foreach (var a in auctions)
        {
            var highest = await GetHighestBidWithFallbackAsync(a.Id);
            var bidCount = await _bidRepo.GetBidCountAsync(a.Id);
            result.Add(MapToResponse(a, highest, bidCount,userId));
        }
        return ServiceResult<List<AuctionResponse>>.Ok(result);
    }

    public async Task<ServiceResult<PagedResponse<AuctionResponse>>> GetMyParticipatedAuctionsAsync(int userId, AuctionFilterRequest filter)
    {
        var (items, total) = await _bidRepo.GetParticipatedAuctionsAsync(userId, filter);

        var responses = new List<AuctionResponse>();
        foreach (var a in items)
        {
            var highest = await GetHighestBidWithFallbackAsync(a.Id);
            var bidCount = await _bidRepo.GetBidCountAsync(a.Id);
            responses.Add(MapToResponse(a, highest, bidCount, userId));
        }

        return ServiceResult<PagedResponse<AuctionResponse>>.Ok(new PagedResponse<AuctionResponse>
        {
            Items = responses,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        });
    }
// called by AuctionScheduleJob


    public async Task<ServiceResult<bool>> StartAuctionAsync(int auctionId)
    {

        var auction = await _auctionRepo.GetByIdWithBidsAsync(auctionId);
        if (auction == null) return ServiceResult<bool>.NotFound();
      
        if (auction.Status != AuctionStatus.Upcoming)
            return ServiceResult<bool>.Fail("Auction is not in upcoming state");

       
        
            var highestBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
            if (highestBid != null)
                await _redis.SetHighestBidAsync(auction.Id, new HighestBidCacheDto
                {
                    Amount   = highestBid.Amount,
                    BidId    = highestBid.Id,
                    PlacedAt = highestBid.PlacedAt,
                    UserId   = highestBid.UserId
                });
        
        auction.Status = AuctionStatus.Live;
        auction.UpdatedAt = TimeHelper.Now();
        await _auctionRepo.UpdateAsync(auction);
        await _auctionRepo.SaveChangesAsync();

        await _publish.Publish(new AuctionStarted(
            auction.Id, auction.ProductId, auction.EndDate));

        await _hub.BroadcastAuctionStarted(auctionId);
       
        _logger.LogInformation("Auction {AuctionId} started", auctionId);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<WinnerResponse>> CloseAuctionAsync(int auctionId)
    {
        var auction = await _auctionRepo.GetByIdAsync(auctionId);
        if (auction == null) return ServiceResult<WinnerResponse>.NotFound();
        if (auction.Status != AuctionStatus.Live)
            return ServiceResult<WinnerResponse>.Fail("Auction is not live");

        

        var highestBid = await _bidRepo.GetHighestBidAsync(auctionId);

        auction.Status = AuctionStatus.Ended;
        auction.UpdatedAt = TimeHelper.Now();

        WinnerResponse? winner = null;

        if (highestBid != null)
        {
            bool reserveMet = auction.ReservePrice == null
                           || highestBid.Amount >= auction.ReservePrice;

            if (reserveMet)
            {
                auction.WinnerBidId = highestBid.Id;
                auction.WinnerUserId = highestBid.UserId;
                auction.FinalPrice = highestBid.Amount;

                highestBid.Status = BidStatus.Won;
                await _bidRepo.UpdateRangeAsync(new[] { highestBid });

                winner = new WinnerResponse
                {
                    AuctionId = auctionId,
                    WinnerUserId = highestBid.UserId,
                    FinalPrice = highestBid.Amount,
                    ClosedAt = TimeHelper.Now()
                };

                await _publish.Publish(new AuctionWinnerDeclared(
                    auctionId,
                    highestBid.UserId,
                    highestBid.Amount,
                    auction.ProductId));
            }
        }

        await _auctionRepo.UpdateAsync(auction);
        await _auctionRepo.SaveChangesAsync();

        await _publish.Publish(new AuctionClosed(
            auctionId,
            auction.WinnerUserId,
            auction.FinalPrice,
            auction.WinnerUserId.HasValue,
            TimeHelper.Now()));

        await _hub.BroadcastAuctionClosed(auctionId,
            winner ?? (object)new { auctionId, status = "ended_no_winner" });

        await _redis.DeleteAuctionCacheAsync(auctionId);

        _logger.LogInformation("Auction {AuctionId} closed. Winner: {WinnerId}",
            auctionId, auction.WinnerUserId);

        return ServiceResult<WinnerResponse>.Ok(
            winner ?? new WinnerResponse { AuctionId = auctionId },
            winner != null ? "Auction closed" : "Auction ended — no winner (reserve not met or no bids)");
    }


    //  this is used for the handling delete things by services okay
    public async Task ProductUnverifyHandling(int productId, int adminId)
    {
        var auctionDetail = await _auctionRepo.GetbyProductId(productId);

        if (auctionDetail == null || auctionDetail.CreatedByVerifyId != adminId)
            return;

        if (auctionDetail.Status == AuctionStatus.Live)
        {
            // Close the auction first before un-verifying
            await CloseAuctionAsync(auctionDetail.Id);
            await _hub.BroadcastProductUnverified(auctionDetail.Id);
        }

        // BUG FIX #3: Was using DateTime.Now (local time). Must use DateTime.UtcNow
        // to stay consistent with the rest of the codebase and avoid timezone bugs.
        auctionDetail.StartDate  = TimeHelper.Now();
        auctionDetail.EndDate    = TimeHelper.Now();
        auctionDetail.UpdatedAt  = TimeHelper.Now();
        auctionDetail.Status     = AuctionStatus.UnVerified;
        await _publish.Publish(new ProductAddAuctionDate(productId:auctionDetail.ProductId,StartDate:null,EndDate:null));
        
        // BUG FIX #4: Was calling SaveChangesAsync without calling UpdateAsync first.
        // Without UpdateAsync the EF change-tracker may not mark the entity as Modified,
        // so the UPDATE statement would never be sent to the database.
        await _auctionRepo.UpdateAsync(auctionDetail);
        await _auctionRepo.SaveChangesAsync();
    }



    private static AuctionResponse MapToResponse(
        Auction auction, HighestBidCacheDto? highest, int bidCount,int ownId=0) => new()
        {
            Id = auction.Id,
            ProductId = auction.ProductId,
            CreatedByUserId = auction.CreatedByUserId,
            StartingPrice = auction.StartingPrice,
            ReservePrice = ownId==auction.CreatedByUserId?auction.ReservePrice:null,
            MinBidIncrement = auction.MinBidIncrement,
            StartDate = auction.StartDate,
            EndDate = auction.EndDate,
            Status = auction.Status.ToString(),
            CurrentHighestBid = highest?.Amount ?? auction.StartingPrice, 
            TotalBids = bidCount,
            TimeRemainingSeconds = auction.Status == AuctionStatus.Live
                                ? (auction.EndDate - TimeHelper.Now()).TotalSeconds
                                : null,
            CreatedAt = auction.CreatedAt,
            productDescription=auction.Description,
            productName=auction.ProductName
        };

    private static AuctionDetailResponse MapToDetail(
        Auction auction, HighestBidCacheDto? highest,
        int bidCount, int watcherCount, long viewerCount,bool flag=false)
    {
        var response = new AuctionDetailResponse
        {
            Id = auction.Id,
            ProductId = auction.ProductId,
            CreatedByUserId = auction.CreatedByUserId,
            StartingPrice = auction.StartingPrice,
            ReservePrice = auction.ReservePrice,
            MinBidIncrement = auction.MinBidIncrement,
            StartDate = auction.StartDate,
            productDescription=auction.Description,
            productName=auction.ProductName,
            EndDate = auction.EndDate,
            Status = auction.Status.ToString(),
            CurrentHighestBid = highest?.Amount ?? auction.StartingPrice,
            TotalBids = bidCount,
            TimeRemainingSeconds = auction.Status == AuctionStatus.Live
                                    ? (auction.EndDate - TimeHelper.Now()).TotalSeconds
                                    : null,
            CreatedAt = auction.CreatedAt,
            WatcherCount = watcherCount,
            LiveViewerCount = viewerCount
        };

        if (highest != null)
        {
            response.HighestBid = new BidResponse
            {
                Id = highest.BidId,
                AuctionId = auction.Id,
                MaskedBidder = MaskUserId(highest.UserId),
                Amount = highest.Amount,
                Status = "Active",
                PlacedAt = highest.PlacedAt
            };
        }

        if (auction.WinnerUserId.HasValue && auction.FinalPrice.HasValue )
        {
        response.win=true;
        if(flag)
            response.Winner = new WinnerResponse
            {
                AuctionId = auction.Id,
                WinnerUserId = auction.WinnerUserId.Value,
                FinalPrice = auction.FinalPrice.Value,
                ClosedAt = auction.UpdatedAt
            };
        }

        response.RecentBids = auction.Bids.Select(b => new BidResponse
        {
            Id = b.Id,
            AuctionId = b.AuctionId,
            MaskedBidder = MaskUserId(b.UserId),
            Amount = b.Amount,
            Status = b.Status.ToString(),
            PlacedAt = b.PlacedAt
        }).ToList();

        return response;
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

    // this is the used by the consumer when the aucition started 
    public async Task forceFullyclosed(int productId, int userId)
    {
        var auction = await _auctionRepo.GetbyProductId(productId);
        if (auction == null || auction.CreatedByUserId != userId)
            return;

        // BUG FIX #5: Must broadcast BEFORE deleting from Redis / DB so that the
        // SignalR message is sent while the auction data is still available.
        if (auction.Status == AuctionStatus.Live)
            await _hub.BroadcastProductDeleted(auction.Id);

        await _redis.DeleteAuctionCacheAsync(auction.Id);

        // removeAuction() is synchronous (Remove() + return Entity) — no need to await
        // its return value. We just call it and then SaveChangesAsync to persist.
       await _auctionRepo.removeAuction(auction);
        await _auctionRepo.SaveChangesAsync();
    }
    private static string MaskUserId(int userId)
    {
        var s = userId.ToString();
        return s.Length <= 2 ? "***" : $"{s[0]}***{s[^1]}";
    }

    
}
