using System.Threading.Tasks;
using ADMIN.Data.Dto;
using ADMIN.DTOs.Responses;
using ADMIN.Middleware.EndPointfilters;
using ADMIN.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADMIN.Controllers
{
    [ApiController]
    [Route("api/admin-request")]
    [Authorize(Roles = "ADMIN")]
    public class AdminRequestController : ControllerBase
    {
        private readonly IRequestService _requestService;

        public AdminRequestController(IRequestService requestService)
        {
            _requestService = requestService;
        }
        //  THIS IS THE DIRECTLY USED HERE 
        [HttpGet("verify/{RequestId:int}")]
        [TypeFilter(typeof(VerifyFilter))]
        public async Task<IActionResult> VerifyRequest(int RequestId)
        {
            if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));

            var response = await _requestService.VerifyRequestAsync(RequestId, userid);
            return ToActionResult(response);
        }

        [HttpGet("grant-rights/{requestId:int}")]
        [TypeFilter(typeof(VerifyFilter))]
        public async Task<IActionResult> GrantUserRights(int requestId)
        {
            if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));

            var response = await _requestService.GrantUserRightsAsync(requestId, userid);
            return ToActionResult(response);
        }

        [HttpGet("revoke-rights/{requestId:int}")]
        [TypeFilter(typeof(VerifyFilter))]
        public async Task<IActionResult> RevokeUserRights(int requestId)
        {
            if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));

            var response = await _requestService.RevokeUserRightsAsync(requestId, userid);
            return ToActionResult(response);
        }

        [HttpGet("revoke-verification/{requestId:int}")]
        [TypeFilter(typeof(VerifyFilter))]
        public async Task<IActionResult> RevokeVerification(int requestId)
        {
            if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));

            var response = await _requestService.RevokeVerificationAsync(requestId, userid);
            return ToActionResult(response);
        }

        [HttpGet("details/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRequestDetails(int id)
        {
            var response = await _requestService.GetRequestDetailsAsync(id);
            return ToActionResult(response);
        }

        [HttpGet("user/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUserRequests(int userId)
        {
            var response = await _requestService.GetUserRequestsAsync(userId);
            return ToActionResult(response);
        }

        [HttpGet("pending")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPendingRequests()
        {
            var response = await _requestService.GetPendingRequestsAsync();
            return ToActionResult(response);
        }

        [HttpGet("verified")]
        
        public async Task<IActionResult> GetVerifiedRequests(bool mine = false,int page=1,int pagesize=10)
        {
            int id=0;
            if(mine)
            {
                if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));
                id=userid;
            }
            var response = await _requestService.GetVerifiedRequestsAsync(id);
            return ToActionResult(response);
        }
        [HttpPost ("filter")]
        public async Task<IActionResult> GetFilterdData([FromBody] Filter filter)
        {
             if (!int.TryParse(HttpContext.Items["id"]?.ToString(), out int userid))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 401));

            filter.mineId=userid;
            var data= await _requestService.getAllFilterRequest(filter);
            return ToActionResult(data);     
        }

        [HttpGet("dashboard")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDashboard()
        {
            var response = await _requestService.GetDashboardAsync();
            return ToActionResult(response);
        }

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
