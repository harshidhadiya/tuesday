using ADMIN.Data.Dto;
using ADMIN.Middleware.EndPointfilters;
using ADMIN.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADMIN.Controllers
{
    [ApiController]
    [Route("api/Request/[controller]")]
    public class AdminProductController : ControllerBase
    {
        private readonly IAdminProductService _adminProductService;

        public AdminProductController(IAdminProductService adminProductService)
        {
            _adminProductService = adminProductService;
        }

        [HttpPost("verify")]
        [Authorize(Roles = "ADMIN")]
        [TypeFilter(typeof(VerifyFilter))]
        public IActionResult VerifyProduct(ProductVerify request)
        {
            var id = HttpContext.Items["id"];
            if (!int.TryParse(id?.ToString(), out int userid))
            {
                return new BadRequestObjectResult(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid request data"));
            }

            var response = _adminProductService.VerifyProduct(request, userid);
            return Ok(response);
        }

        [HttpDelete("unverify/{id:int}")]
        [Authorize(Roles = "ADMIN")]
        [TypeFilter(typeof(VerifyFilter))]
        public IActionResult UnverifyProduct(int id, string description = "")
        {
            var idq = HttpContext.Items["id"];
            if (!int.TryParse(idq?.ToString(), out int userid))
            {
                return new BadRequestObjectResult(ApiResponse<object>.ErrorResponse("Invalid user ID in context", 400));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid request data"));
            }

            var response = _adminProductService.UnverifyProduct(id, userid, description);
            return Ok(response);
        }
    }
}
