using ADMIN.Data.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USER.Data.Dto;
using USER.Data.Dto.Response;
using USER.Data.Interfaces;
using USER.Helper;
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
        private readonly IUserService _userService;
        private readonly ILogger<AdminController> logger;
        private readonly IRefreshToken refreshToken;

        public AdminController(
            IUserAdminService userAdminService,
            IadminLogin adminLogin,
            IHttpClientFactory httpClientFactory, IUserService _userService, ILogger<AdminController> logger,IRefreshToken refreshToken)
        {
            _userAdminService = userAdminService;
            _adminLogin = adminLogin;
            _httpClient = httpClientFactory.CreateClient("DefaultClient");
            this._userService = _userService;
            this.logger = logger;
            this.refreshToken=refreshToken;
        }
        [NonAction]
        public ActionResult badResponce(string message, int code, string methodName)
        {
            logger.LogWarning($"{message} comes from {methodName}");

            switch (code)
            {
                case 400:
                    return new BadRequestObjectResult(ApiResponse<object>.ErrorResponse(message));

                case 404:
                    return new NotFoundObjectResult(ApiResponse<object>.ErrorResponse(message, 404));

                default:
                    return StatusCode(500, ApiResponse<object>.ErrorResponse("Internal Server Error"));
            }
        }
          [NonAction]
        public int? getMyId(HttpContext context)
        {
            var id1 = HttpContext.Items["id"];
            if (!int.TryParse(id1?.ToString(), out var userId))
                return null;
            return userId;
        }

        [HttpPost("signup")]
        public async Task<ActionResult> requestSignup(UserCreateDto request)
        {
            logger.LogInformation("entered here also ");
            // this is comes from in the userservices
            var responce = await _userService.CreateUserAsync(request);
            // I changed this: if (responce.Success) was returning a bad response even on success. Changed to if (!responce.Success)
            if (!responce.Success)
                return badResponce(responce.Message, responce.StatusCode, "CreateUser");
            return Ok(ApiResponse<object>.SuccessResponse(responce.Data!, responce.Message));
        }

        [HttpPost("Login")]
        public async Task<ActionResult> Login(UserLoginDto user)
        {
            return await _adminLogin.Login(user, _httpClient);
        }

        [HttpGet("profile")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult> GetProfile([FromQuery]int ?userid=null)
        {
            var userId = userid ?? getMyId(HttpContext);
            if (userId == null)
                return BadRequest(ApiResponse<AdminDetail>.ErrorResponse("Token Not Valid Format", 400));
            var response = await _userAdminService.GetProfileAsync((userid != null && userid != 0) ? (int)userid : (int)userId);
            if (!response.Success)
                return badResponce(response.Message, response.StatusCode, "GetProfile");
            return Ok(ApiResponse<AdminDetail>.SuccessResponse(response.Data!, response.Message, response.StatusCode));
        }


        [HttpGet("refresh")]
        public async Task<IActionResult> getToken()
        {
           return await refreshToken.getResponse(HttpContext,"/api/admin/refresh");   
        }
        [HttpDelete("refresh/logout")]
        public async Task<IActionResult> logout()
        {
            return await refreshToken.Logout(HttpContext ,"/api/admin/refresh");
        }
    }
}
