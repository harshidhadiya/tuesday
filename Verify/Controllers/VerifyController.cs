using ADMIN.Data.Dto;
using VERIFY.DTOs.Requests;
using VERIFY.DTOs.Responses;
using VERIFY.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Verify.Messaging.Events;

namespace VERIFY.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class VerifyController : ControllerBase
    {
        private readonly IVerifyService _verifyService;

        public VerifyController( IVerifyService verifyService)
        {
            
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
            var adminId = GetCurrentUserId();
            if (adminId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid admin id in context.", 400));

            var result = await _verifyService.VerifyProductAsync(adminId.Value, request);
            return ToActionResult(result);
        }


        [HttpDelete("product/{productId:int}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> UnverifyProduct(int productId, [FromBody] string? description = null)
        {
            var adminId = GetCurrentUserId();
            if (adminId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid admin id in context.", 400));

            var result = await _verifyService.UnverifyProductAsync(adminId.Value, productId, description);
            return ToActionResult(result);
        }


        [HttpGet("status/{productId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVerifyStatus(int productId)
        {
            var result = await _verifyService.GetVerifyStatusAsync(productId);
            return ToActionResult(result);
        }


        [HttpGet("my-products")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetProductsVerifiedByMe([FromQuery] string? searchName = null, [FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var adminId = GetCurrentUserId();
            if (adminId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid admin id in context.", 400));

            string? authHeader = Request.Headers.TryGetValue("Authorization", out var headerValue)
                ? headerValue.ToString()
                : null;

            var result = await _verifyService.GetProductsVerifiedByMeAsync(adminId.Value, searchName, authHeader, page, size);
            return ToActionResult(result);
        }


        [HttpGet("unverified-products")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> GetUnverifiedProducts([FromQuery] string? searchName = null, [FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var adminId = GetCurrentUserId();
            if (adminId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid admin id in context.", 400));

            string? authHeader = Request.Headers.TryGetValue("Authorization", out var headerValue)
                ? headerValue.ToString()
                : null;

            var result = await _verifyService.GetUnverifiedProductsAsync(adminId.Value, searchName, authHeader, page, size);
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
