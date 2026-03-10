using System.Threading.Tasks;
using ADMIN.Data.Dto;
using ADMIN.Middleware.EndPointfilters;
using ADMIN.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADMIN.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "ADMIN")]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService _requestService;

        public RequestController(IRequestService requestService)
        {
            _requestService = requestService;
        }

        [HttpGet("verify/{RequestId:int}")]
        [TypeFilter(typeof(VerifyFilter))]
        public async Task<IActionResult> VerifyRequest(int RequestId)
        {
            if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));

            var response = await _requestService.VerifyRequestAsync(RequestId, userid);
            if (response.StatusCode >= 400)
                return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        [HttpGet("grant-rights/{requestId:int}")]
        [TypeFilter(typeof(VerifyFilter))]
        public async Task<IActionResult> GrantUserRights(int requestId)
        {
            if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));

            var response = await _requestService.GrantUserRightsAsync(requestId, userid);
            if (response.StatusCode == 403) return Forbid();
            if (response.StatusCode >= 400) return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        [HttpGet("revoke-rights/{requestId:int}")]
        [TypeFilter(typeof(VerifyFilter))]
        public async Task<IActionResult> RevokeUserRights(int requestId)
        {
            if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));

            var response = await _requestService.RevokeUserRightsAsync(requestId, userid);
            if (response.StatusCode == 403) return Forbid();
            if (response.StatusCode >= 400) return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        [HttpGet("revoke-verification/{requestId:int}")]
        [TypeFilter(typeof(VerifyFilter))]
        public async Task<IActionResult> RevokeVerification(int requestId)
        {
            if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));

            var response = await _requestService.RevokeVerificationAsync(requestId, userid);
            if (response.StatusCode == 403) return Forbid();
            if (response.StatusCode >= 400) return StatusCode(response.StatusCode, response);

            return Ok(response);
        }

        [HttpGet("details/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRequestDetails(int id)
        {
            var response = await _requestService.GetRequestDetailsAsync(id);
            if (response.StatusCode >= 400) return StatusCode(response.StatusCode, response);
            return Ok(response);
        }

        [HttpGet("user/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUserRequests(int userId)
        {
            var response = await _requestService.GetUserRequestsAsync(userId);
            if (response.StatusCode >= 400) return StatusCode(response.StatusCode, response);
            return Ok(response);
        }

        [HttpGet("pending")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPendingRequests()
        {
            var response = await _requestService.GetPendingRequestsAsync();
            if (response.StatusCode >= 400) return StatusCode(response.StatusCode, response);
            return Ok(response);
        }

        [HttpGet("verified")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVerifiedRequests()
        {
            var response = await _requestService.GetVerifiedRequestsAsync();
            if (response.StatusCode >= 400) return StatusCode(response.StatusCode, response);
            return Ok(response);
        }

        [HttpGet("dashboard")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDashboard()
        {
            var response = await _requestService.GetDashboardAsync();
            if (response.StatusCode >= 400) return StatusCode(response.StatusCode, response);
            return Ok(response);
        }
    }
}
