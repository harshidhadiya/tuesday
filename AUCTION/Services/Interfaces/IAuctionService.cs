using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;

namespace AUCTION.Services.Interfaces;

public interface IAuctionService
{
    Task<ServiceResult<AuctionDetailResponse>> GetAuctionAsync(int auctionId,int userId);
    Task<ServiceResult<PagedResponse<AuctionResponse>>> GetAllAuctionsAsync(AuctionFilterRequest filter);
    Task<ServiceResult<AuctionResponse>> UpdateAuctionAsync(int auctionId, UpdateAuctionRequest request, int userId);
    Task<ServiceResult<bool>> CancelAuctionAsync(int auctionId, int userId);
    Task<ServiceResult<List<AuctionResponse>>> GetMyCreatedAuctionsAsync(int userId);
    Task<ServiceResult<PagedResponse<AuctionResponse>>> GetMyParticipatedAuctionsAsync(int userId, ParticipatedFilter filter);

    // Internal — called by the scheduler background job only
    Task<ServiceResult<bool>> StartAuctionAsync(int auctionId);
    Task<ServiceResult<WinnerResponse>> CloseAuctionAsync(int auctionId);
    Task ProductUnverifyHandling(int ProductId,int adminId);
    
    Task forceFullyclosed(int ProductId, int UserId);
}

public interface IBidService
{ 
    Task<ServiceResult<BidResponse>> PlaceBidAsync(int auctionId, PlaceBidRequest request, int userId, string? ipAddress);
    Task<ServiceResult<PagedResponse<BidResponse>>> GetBidHistoryAsync(int auctionId, int page, int pageSize,bool mine,int userId);
    Task<ServiceResult<HighestBidCacheDto?>> GetHighestBidAsync(int auctionId);

}

public interface IWatchlistService
{
    Task<ServiceResult<bool>> WatchAuctionAsync(int auctionId, int userId);
    Task<ServiceResult<bool>> UnwatchAuctionAsync(int auctionId, int userId);
    Task<ServiceResult<List<AuctionResponse>>> GetWatchedAuctionsAsync(int userId,WatchListFilterRequest filter);
}
