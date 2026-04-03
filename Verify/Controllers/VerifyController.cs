using ADMIN.Data.Dto;
using VERIFY.DTOs.Requests;
using VERIFY.DTOs.Responses;
using VERIFY.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Verify.Messaging.Events;
using VERIFY.Data.Dto;
using RabbitMQ.Client;

namespace VERIFY.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class VerifyController : ControllerBase
    {
        private readonly IVerifyService _verifyService;
        ILogger<VerifyController> logger;
        public VerifyController(IVerifyService verifyService,ILogger<VerifyController> logger)
        {
             this.logger=logger;
            _verifyService = verifyService;
        }

        private int? GetCurrentUserId()
        {
            var id = HttpContext.Items["id"];
            if (int.TryParse(id?.ToString(), out var userId))
                return userId;
            return null;
        }


        [HttpPost("product")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> VerifyProduct([FromBody] VerifyProductRequest request)
        {
            logger.LogInformation("productid"+request.ProductId);
            logger.LogInformation("sellerid"+request.SellerId);
            logger.LogInformation("description"+request.description);
            var adminId = GetCurrentUserId();
            if (adminId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid admin id in context.", 400));

            var result = await _verifyService.VerifyProductAsync(adminId.Value, request);
            return ToActionResult(result);
        }


        [HttpPatch("product")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> UnverifyProduct([FromBody] ProductUnverify product)
        {
            var adminId = GetCurrentUserId();
            if (adminId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid admin id in context.", 400));

            var result = await _verifyService.UnverifyProductAsync(adminId.Value, product,HttpContext);
            return ToActionResult(result);
        }


        [HttpGet("status/{productId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVerifyStatus(int productId)
        {
            var result = await _verifyService.GetVerifyStatusAsync(productId);
            return ToActionResult(result);
        }

//  not used right now not used in the ai 
        // [HttpGet("my-products")]
        // [Authorize(Roles = "ADMIN")]
        // public async Task<IActionResult> GetProductsVerifiedByMe([FromQuery] string? searchName = null, [FromQuery] int page = 1, [FromQuery] int size = 10)
        // {
        //     var adminId = GetCurrentUserId();
        //     if (adminId == null)
        //         return BadRequest(ApiResponse<object>.ErrorResponse("Invalid admin id in context.", 400));

        //     string? authHeader = Request.Headers.TryGetValue("Authorization", out var headerValue)
        //         ? headerValue.ToString()
        //         : null;

        //     var result = await _verifyService.GetProductsVerifiedByMeAsync(adminId.Value, searchName, authHeader, page, size);
        //     return ToActionResult(result);
        // }


        // [HttpGet("unverified-products")]
        // [Authorize(Roles = "ADMIN")]
        // public async Task<IActionResult> GetUnverifiedProducts([FromQuery] string? searchName = null, [FromQuery] int page = 1, [FromQuery] int size = 10)
        // {
        //     var adminId = GetCurrentUserId();
        //     if (adminId == null)
        //         return BadRequest(ApiResponse<object>.ErrorResponse("Invalid admin id in context.", 400));

        //     string? authHeader = Request.Headers.TryGetValue("Authorization", out var headerValue)
        //         ? headerValue.ToString()
        //         : null;

        //     var result = await _verifyService.GetUnverifiedProductsAsync(adminId.Value, searchName, authHeader, page, size);
        //     return ToActionResult(result);
        // }
        // this will help ful for the fetching the product right like verified and unverified all of that htings
        [HttpPost("products")]
        [Authorize(Roles ="ADMIN")]
        public async Task<IActionResult> GetProductsUniversal(FilterVerify filter)
        {
            logger.LogInformation(filter.name);
            var adminId = GetCurrentUserId();
            if (adminId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid admin id in context.", 400));
                filter.verifierId=adminId.Value;
            var result=await _verifyService.getUniverSalVerified(filter);
            return ToActionResult(result);
        }
        

        // THis endpoint is used for the event based creation of the auction 
        [HttpPost("auction")]
        [Authorize(Roles ="SELLER,USER")]
        public async Task<IActionResult> createAuctions(CreateAuctionRequest request)
        {
            logger.LogInformation("entered her okay ");
            var userId=GetCurrentUserId();
            if(userId == null)
                   return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user id in context.", 400));
           var result=await _verifyService.CreatAuctionEvent(request,userId.Value);

            return ToActionResult(result);
        }



        [NonAction]
        private IActionResult ToActionResult<T>(ServiceResult<T> result)
        {
            if (result.Success)
            {
                return Ok(ApiResponse<T>.SuccessResponse(result.Data!, result.Message));
            }

            return result.StatusCode switch
            {
                403 => Forbid(),
                404 => NotFound(ApiResponse<object>.ErrorResponse(result.Message, 404)),
                _ => BadRequest(ApiResponse<object>.ErrorResponse(result.Message, result.StatusCode))
            };
        }
    }
}
