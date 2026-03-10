using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USER.Data.Dto;
using USER.Data.Interfaces;
using USER.Services;

namespace USER.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IUserAdminService _userAdminService;
        private readonly IadminLogin _adminLogin;
        private readonly HttpClient _httpClient;

        public AdminController(
            IUserAdminService userAdminService,
            IadminLogin adminLogin,
            IHttpClientFactory httpClientFactory)
        {
            _userAdminService = userAdminService;
            _adminLogin = adminLogin;
            _httpClient = httpClientFactory.CreateClient("DefaultClient");
        }

        [HttpPost("request/signup")]
        public async Task<ActionResult> requestSignup(UserCreateDto request)
        {
            return await _userAdminService.RequestSignupAsync(request);
        }

        [HttpPost("Login")]
        public async Task<ActionResult> Login(UserLoginDto user)
        {
            return await _adminLogin.Login(user, _httpClient);
        }

        [HttpGet("getallverifiedrequests")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult> GetAllVerifiedRequests()
        {
            var currentUserId = HttpContext.Items["id"]?.ToString();
            if (!int.TryParse(currentUserId, out var userId))
                return BadRequest("Invalid User ID in token");

            return await _userAdminService.GetAllVerifiedRequestsAsync(userId);
        }

        [HttpGet("pendingrequests")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult> GetAllPendingRequests()
        {
            return await _userAdminService.GetAllPendingRequestsAsync();
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult> GetAdminDashboard()
        {
            var currentUserId = HttpContext.Items["id"]?.ToString();
            if (!int.TryParse(currentUserId, out var userId))
                return BadRequest("Invalid User ID in token");

            return await _userAdminService.GetAdminDashboardAsync(userId);
        }
    }
}
