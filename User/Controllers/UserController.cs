using ADMIN.Data.Dto;
using MassTransit;
using Messaging.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USER.CloudinaryService;
using USER.Data.Dto;
using USER.Data.Interfaces;
using USER.Services;
using USER.Repository;

namespace USER.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IClodinaryService clodinary;
        private readonly IUserService _userService;
        private readonly IsellerLogin _loginInterface;
        private readonly ILogger<UserController> logger;
        private readonly IPublishEndpoint publish;
        private readonly IUserRepository repo;
        public UserController(IUserService userService, IsellerLogin loginInterface, ILogger<UserController> logger, IClodinaryService clodinary, IPublishEndpoint publish,IUserRepository repo)
        {
            _userService = userService;
            this.logger = logger;
            _loginInterface = loginInterface;
            this.clodinary = clodinary;
            this.publish = publish;
            this.repo=repo;
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
        public async Task<ActionResult> CreateUser([FromForm] UserCreateDto user)
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
        public async Task<ActionResult> ChangeProfile([FromForm] changeProfileDto docs)
        {
            var userId = getMyId(HttpContext);
            if (userId == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Token Not Valid Format", 400));

            logger.LogInformation(docs.Address);
            var responce = await _userService.ChangeProfileAsync((int)userId, docs);
            string? name = null, email = null;
            if (!string.IsNullOrWhiteSpace(docs.Email))
                email = docs.Email;
            if (!string.IsNullOrWhiteSpace(docs.Name))
                name = docs.Name;

            // I changed this: if (responce.Success) was returning a bad response even on success. Changed to if (!responce.Success)
            if (!responce.Success)
                return badResponce(responce.Message, responce.StatusCode, "CreateUser"); // Note: Method name here says CreateUser but it's ChangeProfile in original code.
            if ((name != null || email != null) && responce?.Data?.Role == "ADMIN")
                await publish.Publish(new AdminUpdate(
                      AdminId: userId.Value, Name: name, Email: email
                  ));

            return Ok(ApiResponse<object>.SuccessResponse(responce?.Data!, responce?.Message!));
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
        // [HttpDelete("profile")]
        // [Authorize]
        // public async Task<ActionResult> DeleteProfile()
        // {
        //     int? userId = getMyId(HttpContext);
        //     if (userId == null)
        //         return BadRequest(ApiResponse<object>.ErrorResponse("Token Not Valid Format", 400));
      
        //     var responce = await repo.GetByIdAsync((int)userId);
        //     if(responce == null)
        //     return BadRequest("Not deleted");
        //     var responce1 = await repo.RemoveAsync(responce);
        //     if(responce1 == null)
        //     return BadRequest("Not deleted");   

        //     return Ok("deleted successfully");
        // }





    }
}
