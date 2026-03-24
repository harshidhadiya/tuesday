using AUCTION.Data.Dto;
using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Helpers;
using AUCTION.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AUCTION.Controllers;


[ApiController]
[Route("api/auctions/{auctionId:int}/bids")]
[Authorize]
public class BidController : ControllerBase
{
    private readonly IBidService _bidService;

    public BidController(IBidService bidService) => _bidService = bidService;

    /// POST /api/auctions/{id}/bids
    [HttpPost]
    public async Task<IActionResult> PlaceBid(int auctionId, [FromBody] PlaceBidRequest request)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var ip     = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _bidService.PlaceBidAsync(auctionId, request, userId, ip);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<BidResponse>.SuccessResponse(result.Data!, result.Message, 201)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// GET /api/auctions/{id}/bids?page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        int auctionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,[FromQuery] bool mine=true)
    {
       var userId = ClaimsHelper.GetUserId(User); 
        var result = await _bidService.GetBidHistoryAsync(auctionId, page, pageSize,mine,userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<PagedResponse<BidResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

}
