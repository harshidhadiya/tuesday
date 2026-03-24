using AUCTION.Data.Dto;
using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Helpers;
using AUCTION.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AUCTION.Controllers;

//  Auction Controller 

[ApiController]
[Route("api/auctions")]
[Authorize]
public class AuctionController : ControllerBase
{
    private readonly IAuctionService _auctionService;
    private readonly IHttpClientFactory factory;
    private readonly ILogger<AuctionController> logger;

    public AuctionController(ILogger<AuctionController> logger,IAuctionService auctionService, IHttpClientFactory factory)
    {
        _auctionService = auctionService;
        this.factory = factory;
        this.logger=logger;
    }

    /// GET /api/auctions?Status=Live&Page=1&PageSize=20
    [HttpGet]
    [Authorize(Roles ="USER,SELLER")]
    public async Task<IActionResult> GetAll([FromQuery] AuctionFilterRequest filter)
    {
        var userId=ClaimsHelper.GetUserId(User);
        filter.mineid=userId;
        var result = await _auctionService.GetAllAuctionsAsync(filter);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<PagedResponse<AuctionResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// GET /api/auctions/{id}
    [HttpGet("{auctionId:int}")]
    public async Task<IActionResult> GetById(int auctionId)
    {
    var userId=ClaimsHelper.GetUserId(User);
        var result = await _auctionService.GetAuctionAsync(auctionId,userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<AuctionDetailResponse>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

 


    /// PATCH /api/auctions/{id}
    [HttpPatch("{auctionId:int}")]
    public async Task<IActionResult> Update(int auctionId, [FromBody] UpdateAuctionRequest request)
    {
    logger.LogInformation(request.StartDate +"DateStart");
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _auctionService.UpdateAuctionAsync(auctionId, request, userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<AuctionResponse>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// DELETE /api/auctions/{id}
    [HttpDelete("{auctionId:int}")]
    public async Task<IActionResult> Cancel(int auctionId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _auctionService.CancelAuctionAsync(auctionId, userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<bool>.SuccessResponse(true, result.Message)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// GET /api/auctions/created  — auctions created by the logged-in user
    [HttpGet("created")]
    public async Task<IActionResult> GetMyCreated()
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _auctionService.GetMyCreatedAuctionsAsync(userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<List<AuctionResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// GET /api/auctions/participated  — auctions the user has bid in
    [HttpGet("participated")]
    public async Task<IActionResult> GetMyParticipated([FromQuery]AuctionFilterRequest filter)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _auctionService.GetMyParticipatedAuctionsAsync(userId, filter);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<PagedResponse<AuctionResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }
}




//  Admin Controller 

[ApiController]
[Route("api/admin/auctions")]
[Authorize(Roles = "ADMIN")]
public class AdminAuctionController : ControllerBase
{
    private readonly IAuctionService _auctionService;

    public AdminAuctionController(IAuctionService auctionService)
        => _auctionService = auctionService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AuctionFilterRequest filter)
    {
        var result = await _auctionService.GetAllAuctionsAsync(filter);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<PagedResponse<AuctionResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    [HttpPatch("{auctionId:int}/force-close")]
    public async Task<IActionResult> ForceClose(int auctionId)
    {
        var result = await _auctionService.CloseAuctionAsync(auctionId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<WinnerResponse>.SuccessResponse(result.Data!, "Auction force-closed by admin")
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }
}

