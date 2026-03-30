using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using USER.Controllers;
using USER.Services;
using USER.Data.Dto;
using USER.Data.Dto.Response;
using USER.Repository;
using MassTransit;
using USER.Data.Interfaces;
using ADMIN.Data.Dto;
namespace UserService_Tests.Services.Tests
{
    public class UserControllerTests
    {
        private readonly Mock<IUserService> _serviceMock = new();
        private readonly Mock<IsellerLogin> _loginMock = new();
        private readonly Mock<ILogger<UserController>> _loggerMock = new();
        private readonly Mock<IPublishEndpoint> _publishMock = new();
        private readonly Mock<IUserRepository> _repoMock = new();

        private UserController GetController()
        {
            return new UserController(
                _serviceMock.Object,
                _loginMock.Object,
                _loggerMock.Object,
                null!,
                _publishMock.Object,
                _repoMock.Object
            );
        }

        // CREATE USER SUCCESS
        [Fact]
        public async Task CreateUser_ShouldReturnOk_WhenSuccess()
        {
            var dto = new UserCreateDto { Email = "test@test.com" };

            var data = new OwnDetail { id = 1 };

            var serviceResult = ServiceResult<OwnDetail>.Ok(data, "Created");

            _serviceMock.Setup(x => x.CreateUserAsync(dto))
                        .ReturnsAsync(serviceResult);

            var controller = GetController();

            var result = await controller.CreateUser(dto);

            var okResult = result as OkObjectResult;

            okResult.Should().NotBeNull();
            okResult!.Value.Should().BeEquivalentTo(
                ApiResponse<object>.SuccessResponse(data, "Created")
            );
        }

        // CREATE USER FAIL
        [Fact]
        public async Task CreateUser_ShouldReturnBadRequest_WhenFail()
        {
            var dto = new UserCreateDto { Email = "test@test.com" };

            var serviceResult = ServiceResult<OwnDetail>.Fail("Error", 400);

            _serviceMock.Setup(x => x.CreateUserAsync(dto))
                        .ReturnsAsync(serviceResult);

            var controller = GetController();

            var result = await controller.CreateUser(dto);

            var badResult = result as BadRequestObjectResult;

            badResult.Should().NotBeNull();
            badResult!.Value.Should().BeEquivalentTo(
                ApiResponse<object>.ErrorResponse("Error")
            );
        }

        // GET PROFILE SUCCESS
        [Fact]
        public async Task GetProfile_ShouldReturnOk_WhenUserExists()
        {
            var controller = GetController();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Items["id"] = "1";

            var data = new UserDetail { id = 1 };

            var serviceResult = ServiceResult<UserDetail>.Ok(data, "Success");

            _serviceMock.Setup(x => x.GetProfileAsync(1))
                        .ReturnsAsync(serviceResult);

            var result = await controller.getProfile(null);

            var okResult = result as OkObjectResult;

            okResult.Should().NotBeNull();
            okResult!.Value.Should().BeEquivalentTo(
                ApiResponse<object>.SuccessResponse(data, "Success", serviceResult.StatusCode)
            );
        }

        // GET PROFILE INVALID TOKEN
        [Theory]
        [InlineData(400, "Token Not Valid Format")]
        [InlineData(404, "User Not Found")]
        public async Task GetProfile_ShouldReturnBadRequest_WhenTokenInvalid(int statusCode, string message)
        {
            var controller = GetController();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            if (statusCode != 400)
            {
                controller.HttpContext.Items["id"] = "1";

                _serviceMock.Setup(x => x.GetProfileAsync(It.IsAny<int>()))
                            .ReturnsAsync(ServiceResult<UserDetail>.NotFound(message));
            }
            var result = await controller.getProfile(null);

            var badResult = result as BadRequestObjectResult;
            var notFoundResult = result as NotFoundObjectResult;



            if (statusCode == 400)
            {
                badResult.Should().NotBeNull();
                badResult!.Value.Should().BeEquivalentTo(
                    ApiResponse<object>.ErrorResponse(message, statusCode)
                );
                _serviceMock.Verify(x => x.GetProfileAsync(It.IsAny<int>()), Times.Never);
            }
            else
            {
                notFoundResult.Should().NotBeNull();
                notFoundResult!.Value.Should().BeEquivalentTo(
                    ApiResponse<object>.ErrorResponse(message, statusCode)
                );
                _serviceMock.Verify(x => x.GetProfileAsync(It.IsAny<int>()), Times.Once);
            }
        }

        // CHANGE PROFILE SUCCESS
        [Fact]
        public async Task ChangeProfile_ShouldReturnOk_WhenSuccess()
        {
            var controller = GetController();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Items["id"] = "1";

            var dto = new changeProfileDto { Name = "New Name" };

            var data = new OwnDetail { id = 1, Role = "USER" };

            var serviceResult = ServiceResult<OwnDetail>.Ok(data, "Updated");

            _serviceMock.Setup(x => x.ChangeProfileAsync(1, dto))
                        .ReturnsAsync(serviceResult);

            var result = await controller.ChangeProfile(dto);

            var okResult = result as OkObjectResult;

            okResult.Should().NotBeNull();
            okResult!.Value.Should().BeEquivalentTo(
                ApiResponse<object>.SuccessResponse(data, "Updated")
            );
        }

        // CHANGE PROFILE FAIL
        [Theory]
        [InlineData(400)]
        [InlineData(404)]
        public async Task ChangeProfile_ShouldReturnBadRequest_WhenFail(int statusCode)
        {
            var controller = GetController();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Items["id"] = "1";

            var dto = new changeProfileDto();

            var serviceResult = statusCode switch
            {
                400 => ServiceResult<OwnDetail>.Fail("Error", 400),
                404 => ServiceResult<OwnDetail>.NotFound("Error"),
                _ => ServiceResult<OwnDetail>.Fail("Error", statusCode)
            };

            _serviceMock.Setup(x => x.ChangeProfileAsync(1, dto))
                        .ReturnsAsync(serviceResult);

            var result = await controller.ChangeProfile(dto);

            ObjectResult objectResult = statusCode switch
            {
                400 => result.Should().BeOfType<BadRequestObjectResult>().Subject,
                404 => result.Should().BeOfType<NotFoundObjectResult>().Subject,
                _ => result.Should().BeOfType<ObjectResult>().Subject
            };

            objectResult.Should().NotBeNull();
            objectResult!.Value.Should().BeEquivalentTo(
                ApiResponse<object>.ErrorResponse("Error",statusCode)
            );
        }
    }
}