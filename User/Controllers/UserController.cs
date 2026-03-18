using System.Threading.Tasks;
using ADMIN.Data.Dto;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USER.CloudinaryService;
using USER.Data.Dto;
using USER.Data.Interfaces;
using USER.Services;

namespace USER.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ClodinaryService clodinary;
        private readonly IUserService _userService;
        private readonly IsellerLogin _loginInterface;
        private readonly ILogger<UserController> logger;
        public UserController(IUserService userService, IsellerLogin loginInterface, ILogger<UserController> logger,ClodinaryService clodinary)
        {
            _userService = userService;
            this.logger = logger;
            _loginInterface = loginInterface;
            this.clodinary=clodinary;
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

        [HttpPost("create")]
        public async Task<ActionResult> CreateUser([FromForm]UserCreateDto user)
        {
            var responce = await _userService.CreateUserAsync(user);
            // I changed this: if (responce.Success) was returning a bad response even on success. Changed to if (!responce.Success)
            if (!responce.Success)
                return badResponce(responce.Message, responce.StatusCode, "CreateUser");
            return Ok(ApiResponse<object>.SuccessResponse(responce.Data!, responce.Message));
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(UserLoginDto user)
        {
            return await _loginInterface.Login(user, null);
        }



        [HttpPatch("profile")]
        [Authorize]
        public async Task<ActionResult> ChangeProfile([FromForm]changeProfileDto docs)
        {
            var userId = getMyId(HttpContext);
            if (userId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Token Not Valid Format", 400));

            logger.LogInformation(docs.Address);
            var responce = await _userService.ChangeProfileAsync((int)userId, docs);
            // I changed this: if (responce.Success) was returning a bad response even on success. Changed to if (!responce.Success)
            if (!responce.Success)
                return badResponce(responce.Message, responce.StatusCode, "CreateUser"); // Note: Method name here says CreateUser but it's ChangeProfile in original code.

            return Ok(ApiResponse<object>.SuccessResponse(responce.Data!, responce.Message));
        }

        [HttpGet("profile/{id:int}")]
        [Authorize]
        public async Task<ActionResult> getProfile(int? id)
        {
            int? userId = getMyId(HttpContext);
            if (userId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Token Not Valid Format", 400));

            var responce = await _userService.GetProfileAsync((id != null && id != 0) ? (int)id : (int)userId);
            // I changed this: if (responce.Success) was returning a bad response even on success. Changed to if (!responce.Success)
            if (!responce.Success)
                return badResponce(responce.Message, responce.StatusCode, "getProfile");

            return Ok(ApiResponse<object>.SuccessResponse(responce.Data!, responce.Message, responce.StatusCode));
        }
        




    }
}
