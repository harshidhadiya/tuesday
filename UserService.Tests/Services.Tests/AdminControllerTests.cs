using Microsoft.AspNetCore.Http;
using USER.Controllers;
using USER.Services;
using USER.Data.Dto;
using USER.Data.Dto.Response;
using USER.Data.Interfaces;
using ADMIN.Data.Dto;
using Moq;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
namespace UserService_Tests.Services.Tests
{
    public class AdminControllerTests
    {
        private readonly Mock<IUserAdminService> _adminServiceMock = new();
        private readonly Mock<IadminLogin> _loginMock = new();
        private readonly Mock<IUserService> _userServiceMock = new();
        private readonly Mock<ILogger<AdminController>> _loggerMock = new();
        private readonly Mock<IHttpClientFactory> _httpFactoryMock = new();

        private AdminController GetController()
        {
            _httpFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                            .Returns(new HttpClient());

            return new AdminController(
                _adminServiceMock.Object,
                _loginMock.Object,
                _httpFactoryMock.Object,
                _userServiceMock.Object,
                _loggerMock.Object
            );
        }

   
        [Fact]
        public async Task Signup_ShouldReturnOk_WhenSuccess()
        {
            var dto = new UserCreateDto { Email = "admin@test.com" };

            var data = new OwnDetail { id = 1 };

            var serviceResult = ServiceResult<OwnDetail>.Ok(data, "Created");

            _userServiceMock.Setup(x => x.CreateUserAsync(dto))
                            .ReturnsAsync(serviceResult);

            var controller = GetController();

            var result = await controller.requestSignup(dto);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;

            ok.Value.Should().BeEquivalentTo(
                ApiResponse<object>.SuccessResponse(data, "Created")
            );
        }

        [Fact]
        public async Task Signup_ShouldReturnBadRequest_WhenFail()
        {
            var dto = new UserCreateDto { Email = "admin@test.com" };

            var serviceResult = ServiceResult<OwnDetail>.Fail("Error", 400);

            _userServiceMock.Setup(x => x.CreateUserAsync(dto))
                            .ReturnsAsync(serviceResult);

            var controller = GetController();

            var result = await controller.requestSignup(dto);

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;

            bad.Value.Should().BeEquivalentTo(
                ApiResponse<object>.ErrorResponse("Error")
            );
        }

        [Fact]
        public async Task Signup_ShouldReturnNotFound_WhenFail404()
        {
            var dto = new UserCreateDto { Email = "admin@test.com" };

            var serviceResult = ServiceResult<OwnDetail>.NotFound("Not Found");

            _userServiceMock.Setup(x => x.CreateUserAsync(dto))
                            .ReturnsAsync(serviceResult);

            var controller = GetController();

            var result = await controller.requestSignup(dto);

            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;

            notFound.Value.Should().BeEquivalentTo(
                ApiResponse<object>.ErrorResponse("Not Found", 404)
            );
        }

    
        [Fact]
        public async Task Login_ShouldReturnFromLoginService()
        {
            var dto = new UserLoginDto();

            var expected = new OkObjectResult("TOKEN");

            _loginMock.Setup(x => x.Login(dto, It.IsAny<HttpClient>()))
                      .ReturnsAsync(expected);

            var controller = GetController();

            var result = await controller.Login(dto);

            result.Should().BeSameAs(expected);
        }

        [Fact]
        public async Task GetProfile_ShouldReturnBadRequest_WhenTokenInvalid()
        {
            var controller = GetController();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var result = await controller.GetProfile(null);

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;

            bad.Value.Should().BeEquivalentTo(
                ApiResponse<AdminDetail>.ErrorResponse("Token Not Valid Format", 400)
            );
        }

        [Fact]
        public async Task GetProfile_ShouldReturnOk_WhenSuccess()
        {
            var controller = GetController();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Items["id"] = "1";

            var data = new AdminDetail { id = 1 };

            var serviceResult = ServiceResult<AdminDetail>.Ok(data, "Success");

            _adminServiceMock.Setup(x => x.GetProfileAsync(1))
                             .ReturnsAsync(serviceResult);

            var result = await controller.GetProfile(null);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;

            ok.Value.Should().BeEquivalentTo(
                ApiResponse<AdminDetail>.SuccessResponse(data, "Success", serviceResult.StatusCode)
            );
        }

   
        [Fact]
        public async Task GetProfile_ShouldReturnBadRequest_WhenServiceFail()
        {
            var controller = GetController();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Items["id"] = "1";

            var serviceResult = ServiceResult<AdminDetail>.Fail("Error", 400);

            _adminServiceMock.Setup(x => x.GetProfileAsync(1))
                             .ReturnsAsync(serviceResult);

            var result = await controller.GetProfile(null);

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;

            bad.Value.Should().BeEquivalentTo(
                ApiResponse<object>.ErrorResponse("Error")
            );
        }

      
        [Fact]
        public async Task GetProfile_ShouldReturnNotFound_WhenServiceNotFound()
        {
            var controller = GetController();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Items["id"] = "1";

            var serviceResult = ServiceResult<AdminDetail>.NotFound("Not Found");

            _adminServiceMock.Setup(x => x.GetProfileAsync(1))
                             .ReturnsAsync(serviceResult);

            var result = await controller.GetProfile(null);

            var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;

            notFound.Value.Should().BeEquivalentTo(
                ApiResponse<object>.ErrorResponse("Not Found", 404)
            );
        }
    }
}