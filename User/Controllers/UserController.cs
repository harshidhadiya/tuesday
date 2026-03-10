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
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IsellerLogin _loginInterface;

        public UserController(IUserService userService, IsellerLogin loginInterface)
        {
            _userService = userService;
            _loginInterface = loginInterface;
        }

        [HttpPost("createUser")]
        public async Task<ActionResult> CreateUser(UserCreateDto user)
        {
            return await _userService.CreateUserAsync(user);
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(UserLoginDto user)
        {
            return await _loginInterface.Login(user, null);
        }

        [HttpPost("changepassword")]
        [Authorize]
        public async Task<ActionResult> changePassword(changePasswordDto pass_obj)
        {
            var id = HttpContext.Items["id"];
            if (!int.TryParse(id?.ToString(), out var userId))
                return BadRequest("Token Id is not valid.");

            return await _userService.ChangePasswordAsync(userId, pass_obj);
        }

        [HttpPatch("changeprofile")]
        [Authorize]
        public async Task<ActionResult> ChangeProfile(changeProfileDto docs)
        {
            var id = HttpContext.Items["id"];
            if (!int.TryParse(id?.ToString(), out var userId))
                return BadRequest("Token Id is not valid.");

            return await _userService.ChangeProfileAsync(userId, docs);
        }

        [HttpGet("getprofile")]
        [Authorize]
        public async Task<ActionResult> getProfile()
        {
            var id = HttpContext.Items["id"];
            if (!int.TryParse(id?.ToString(), out var userId))
                return BadRequest("Token Id is not valid.");

            return await _userService.GetProfileAsync(userId);
        }

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetUserById(int id)
        {
            return await _userService.GetUserByIdAsync(id);
        }

        [HttpGet("dashboard")]
        [Authorize]
        public async Task<ActionResult> GetUserDashboard()
        {
            var id = HttpContext.Items["id"];
            if (!int.TryParse(id?.ToString(), out var userId))
                return BadRequest("Invalid token.");

            return await _userService.GetUserDashboardAsync(userId);
        }
    }
}
