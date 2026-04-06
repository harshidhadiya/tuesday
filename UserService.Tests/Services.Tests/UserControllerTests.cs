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
using Messaging.Contracts;
using USER.Helper;
namespace UserService_Tests.Services.Tests
{
    public class UserControllerTests
    {
        private readonly Mock<IUserService> _serviceMock = new();
        private readonly Mock<IsellerLogin> _loginMock = new();
        private readonly Mock<ILogger<UserController>> _loggerMock = new();
        private readonly Mock<IPublishEndpoint> _publishMock = new();
        private readonly Mock<IUserRepository> _repoMock = new();
        private readonly Mock<IRefreshToken> refreshToken=new();
        private UserController GetController()
        {
            return new UserController(
                _serviceMock.Object,
                _loginMock.Object,
                _loggerMock.Object,
                null!,
                _publishMock.Object,
                _repoMock.Object,
                refreshToken.Object
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
        [InlineData(401, "Token Not Valid Format")]
        [InlineData(404, "User Not Found")]
        public async Task GetProfile_ShouldReturnBadRequest_WhenTokenInvalid(int statusCode, string message)
        {
            var controller = GetController();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            if (statusCode != 401)
            {
                controller.HttpContext.Items["id"] = "1";

                _serviceMock.Setup(x => x.GetProfileAsync(It.IsAny<int>()))
                            .ReturnsAsync(ServiceResult<UserDetail>.NotFound(message));
            }
            var result = await controller.getProfile(null);

            var badResult = result as UnauthorizedObjectResult;
            var notFoundResult = result as NotFoundObjectResult;



            if (statusCode == 401)
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
        [Fact]
        public async Task ChangeProfile_ShouldReturnOk_WhenAdminIsRole()
        {
            var controller = GetController();
            CancellationToken token=default;
            
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Items["id"] = "1";

            var dto = new changeProfileDto { Name = "New Name" };

            var data = new OwnDetail { id = 1, Role = "ADMIN" };

            var serviceResult = ServiceResult<OwnDetail>.Ok(data, "Updated");

            _serviceMock.Setup(x => x.ChangeProfileAsync(1, dto))
                        .ReturnsAsync(serviceResult);

            var result = await controller.ChangeProfile(dto);

            var okResult = result as OkObjectResult;

            okResult.Should().NotBeNull();
            okResult!.Value.Should().BeEquivalentTo(
                ApiResponse<object>.SuccessResponse(data, "Updated")
            );
            _publishMock.Verify(x=>x.Publish(It.IsAny<AdminUpdate>(),token),Times.Once);
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
        [Fact]
        public async Task ChangeProfile_ShouldReturnBadRequest_WhenUserIdIsNull()
        {
            var controller=GetController();
            controller.ControllerContext=new ControllerContext
            {
                HttpContext=new DefaultHttpContext()
            };
            var data=new changeProfileDto{Address="nothing"};
            var result=await controller.ChangeProfile(data);
            result.Should().BeOfType<UnauthorizedObjectResult>();
            var badresult=result as UnauthorizedObjectResult;
            badresult!.StatusCode.Should().Be(401);
            badresult.Value.Should().BeEquivalentTo(ApiResponse<object>.ErrorResponse("Token Not Valid Format",401));
        }
        
        [Theory]
        [InlineData(400,"BAD REQUEST")]
        [InlineData(404,"NOT FOUND")]
        [InlineData(500,"Internal Server Error")]
        [InlineData(403,"FORBIDDEN")]
        [InlineData(401,"UNAUTHORIZED")]
        public async Task badResponce_Should_Return_Correct_Status_Code_And_Message(int statusCode, string message)
        {
            var service = GetController();
            var result = service.badResponce(message, statusCode,"all");

            result.Should().NotBeNull();
            result.Should().BeAssignableTo<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult.Should().NotBeNull(); 
            objectResult!.Value.Should().BeEquivalentTo(ApiResponse<object>.ErrorResponse(message, statusCode));
            objectResult.StatusCode.Should().Be(statusCode);
            
        
        
        }
    }
}