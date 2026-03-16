using AUCTION.Data.Dto;
using AUCTION.Data.Dto.Request;
using AUCTION.Data.Dto.Response;
using AUCTION.Helpers;
using AUCTION.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AUCTION.Controllers;

// ── Auction Controller ────────────────────────────────────────────────────────

[ApiController]
[Route("api/auctions")]
[Authorize]
public class AuctionController : ControllerBase
{
    private readonly IAuctionService _auctionService;

    public AuctionController(IAuctionService auctionService)
        => _auctionService = auctionService;

    /// GET /api/auctions?Status=Live&Page=1&PageSize=20
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] AuctionFilterRequest filter)
    {
        var result = await _auctionService.GetAllAuctionsAsync(filter);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<PagedResponse<AuctionResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// GET /api/auctions/{id}
    [HttpGet("{auctionId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int auctionId)
    {
        var result = await _auctionService.GetAuctionAsync(auctionId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<AuctionDetailResponse>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// GET /api/auctions/{id}/winner
    [HttpGet("{auctionId:int}/winner")]
    [AllowAnonymous]
    public async Task<IActionResult> GetWinner(int auctionId)
    {
        var result = await _auctionService.GetAuctionAsync(auctionId);
        if (!result.Success)
            return StatusCode(result.StatusCode, ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));

        var winner = result.Data?.Winner;
        return winner != null
            ? Ok(ApiResponse<WinnerResponse>.SuccessResponse(winner))
            : NotFound(ApiResponse<object>.ErrorResponse("No winner yet — auction may still be live", 404));
    }

    /// POST /api/auctions  — verified users only
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAuctionRequest request)
    {
        if (!ClaimsHelper.IsVerified(User))
            return StatusCode(403, ApiResponse<object>.ErrorResponse(
                "Only verified users can create auctions", 403));

        var userId   = ClaimsHelper.GetUserId(User);
        var verifyId = ClaimsHelper.GetVerifyId(User);

        var result = await _auctionService.CreateAuctionAsync(request, userId, verifyId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<AuctionResponse>.SuccessResponse(result.Data!, result.Message, 201)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// PATCH /api/auctions/{id}
    [HttpPatch("{auctionId:int}")]
    public async Task<IActionResult> Update(int auctionId, [FromBody] UpdateAuctionRequest request)
    {
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
    public async Task<IActionResult> GetMyParticipated()
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _auctionService.GetMyParticipatedAuctionsAsync(userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<List<AuctionResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }
}

// ── Bid Controller ────────────────────────────────────────────────────────────

// ── Watchlist Controller ──────────────────────────────────────────────────────

[ApiController]
[Route("api/auctions")]
[Authorize]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _watchlistService;

    public WatchlistController(IWatchlistService watchlistService)
        => _watchlistService = watchlistService;

    /// POST /api/auctions/{id}/watch
    [HttpPost("{auctionId:int}/watch")]
    public async Task<IActionResult> Watch(int auctionId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _watchlistService.WatchAuctionAsync(auctionId, userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<bool>.SuccessResponse(true, result.Message)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// DELETE /api/auctions/{id}/watch
    [HttpDelete("{auctionId:int}/watch")]
    public async Task<IActionResult> Unwatch(int auctionId)
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _watchlistService.UnwatchAuctionAsync(auctionId, userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<bool>.SuccessResponse(true, result.Message)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// GET /api/auctions/watched
    [HttpGet("watched")]
    public async Task<IActionResult> GetWatched()
    {
        var userId = ClaimsHelper.GetUserId(User);
        var result = await _watchlistService.GetWatchedAuctionsAsync(userId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<List<AuctionResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }
}

// ── Admin Controller ──────────────────────────────────────────────────────────

[ApiController]
[Route("api/admin/auctions")]
[Authorize(Roles = "Admin")]
public class AdminAuctionController : ControllerBase
{
    private readonly IAuctionService _auctionService;

    public AdminAuctionController(IAuctionService auctionService)
        => _auctionService = auctionService;

    /// GET /api/admin/auctions  — all auctions with full metadata
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AuctionFilterRequest filter)
    {
        var result = await _auctionService.GetAllAuctionsAsync(filter);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<PagedResponse<AuctionResponse>>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// PATCH /api/admin/auctions/{id}/force-close
    [HttpPatch("{auctionId:int}/force-close")]
    public async Task<IActionResult> ForceClose(int auctionId)
    {
        var result = await _auctionService.CloseAuctionAsync(auctionId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<WinnerResponse>.SuccessResponse(result.Data!, "Auction force-closed by admin")
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }
}

// ── Internal Controller — NOT exposed through API Gateway ────────────────────
// Only the scheduler background job calls these endpoints.
// Protected by a secret header key (X-Internal-Key).

[ApiController]
[Route("internal/auctions")]
public class InternalAuctionController : ControllerBase
{
    private readonly IAuctionService _auctionService;
    private readonly IConfiguration  _config;

    public InternalAuctionController(IAuctionService auctionService, IConfiguration config)
    {
        _auctionService = auctionService;
        _config         = config;
    }

    private IActionResult Forbidden403() =>
        StatusCode(403, ApiResponse<object>.ErrorResponse("Forbidden", 403));

    private bool IsValidInternalKey(string? key) =>
        key == _config["InternalApi:Key"];

    /// POST /internal/auctions/{id}/start
    [HttpPost("{auctionId:int}/start")]
    public async Task<IActionResult> Start(
        int auctionId,
        [FromHeader(Name = "X-Internal-Key")] string? key)
    {
        if (!IsValidInternalKey(key)) return Forbidden403();

        var result = await _auctionService.StartAuctionAsync(auctionId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<bool>.SuccessResponse(true)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }

    /// POST /internal/auctions/{id}/close
    [HttpPost("{auctionId:int}/close")]
    public async Task<IActionResult> Close(
        int auctionId,
        [FromHeader(Name = "X-Internal-Key")] string? key)
    {
        if (!IsValidInternalKey(key)) return Forbidden403();

        var result = await _auctionService.CloseAuctionAsync(auctionId);
        return StatusCode(result.StatusCode, result.Success
            ? ApiResponse<WinnerResponse>.SuccessResponse(result.Data!)
            : ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode));
    }
}
