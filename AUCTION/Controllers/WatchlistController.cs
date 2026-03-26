using AUCTION.Data.Dto;
using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Helpers;
using AUCTION.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AUCTION.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _watchlistService;

    public WatchlistController(IWatchlistService watchlistService)
        => _watchlistService = watchlistService;

    [HttpPost("{auctionId:int}/watch")]
    public async Task<IActionResult> Watch(int auctionId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _watchlistService.WatchAuctionAsync(auctionId, userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<bool>.SuccessResponse(true, result.Message)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    [HttpDelete("{auctionId:int}/watch")]
    public async Task<IActionResult> Unwatch(int auctionId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _watchlistService.UnwatchAuctionAsync(auctionId, userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<bool>.SuccessResponse(true, result.Message)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    [HttpGet("watched")]
    public async Task<IActionResult> GetWatched([FromQuery]WatchListFilterRequest filter)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _watchlistService.GetWatchedAuctionsAsync(userId,filter);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<List<AuctionResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }
}
