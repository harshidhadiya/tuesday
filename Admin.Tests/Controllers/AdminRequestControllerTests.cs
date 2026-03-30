using ADMIN.Controllers;
using ADMIN.Data.Dto;
using ADMIN.DTOs.Responses;
using ADMIN.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Admin.Tests.Controllers
{
    public class AdminRequestControllerTests
    {
        private readonly Mock<IRequestService> _service;
        private readonly AdminRequestController _sut;
        private readonly DefaultHttpContext _httpContext;

        public AdminRequestControllerTests()
        {
            _service = new Mock<IRequestService>();

            _httpContext = new DefaultHttpContext();
            _sut = new AdminRequestController(_service.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = _httpContext }
            };
        }

        private void SetUserIdInContext(int id)
        {
            _httpContext.Items["id"] = id;
        }

        // =========================
        // VERIFY REQUEST
        // =========================

        [Fact]
        public async Task VerifyRequest_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.VerifyRequest(1);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task VerifyRequest_Should_Return_200_When_Success()
        {
            SetUserIdInContext(5);

            var dto = new RequestDetailResponse { Id = 1 };

            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = true,
                Data = dto,
                Message = "Success"
            };

            _service.Setup(x => x.VerifyRequestAsync(1, 5))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.VerifyRequest(1);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;

            ok.Value.Should().BeEquivalentTo(
                ApiResponse<RequestDetailResponse>.SuccessResponse(dto, "Success")
            );
        }

        [Fact]
        public async Task VerifyRequest_Should_Return_404_When_NotFound()
        {
            SetUserIdInContext(5);

            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = false,
                StatusCode = 404,
                Message = "Not Found"
            };

            _service.Setup(x => x.VerifyRequestAsync(1, 5))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.VerifyRequest(1);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task VerifyRequest_Should_Return_403_When_Forbidden()
        {
            SetUserIdInContext(5);

            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = false,
                StatusCode = 403
            };

            _service.Setup(x => x.VerifyRequestAsync(1, 5))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.VerifyRequest(1);

            result.Should().BeOfType<ForbidResult>();
        }

        // =========================
        // GRANT USER RIGHTS
        // =========================

        [Fact]
        public async Task GrantUserRights_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.GrantUserRights(1);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GrantUserRights_Should_Return_200_When_Success()
        {
            SetUserIdInContext(5);

            var dto = new RequestDetailResponse { Id = 1 };

            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = true,
                Data = dto,
                Message = "Success"
            };

            _service.Setup(x => x.GrantUserRightsAsync(1, 5))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.GrantUserRights(1);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GrantUserRights_Should_Return_404_When_NotFound()
        {
            SetUserIdInContext(5);

            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = false,
                StatusCode = 404
            };

            _service.Setup(x => x.GrantUserRightsAsync(1, 5))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.GrantUserRights(1);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GrantUserRights_Should_Return_403_When_Forbidden()
        {
            SetUserIdInContext(5);

            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = false,
                StatusCode = 403
            };

            _service.Setup(x => x.GrantUserRightsAsync(1, 5))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.GrantUserRights(1);

            result.Should().BeOfType<ForbidResult>();
        }

        // =========================
        // REVOKE USER RIGHTS
        // =========================

        [Fact]
        public async Task RevokeUserRights_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.RevokeUserRights(1);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task RevokeUserRights_Should_Return_200_When_Success()
        {
            SetUserIdInContext(5);

            var dto = new RequestDetailResponse { Id = 1 };

            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = true,
                Data = dto
            };

            _service.Setup(x => x.RevokeUserRightsAsync(1, 5))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.RevokeUserRights(1);

            result.Should().BeOfType<OkObjectResult>();
        }

        // =========================
        // REVOKE VERIFICATION
        // =========================

        [Fact]
        public async Task RevokeVerification_Should_Return_400_When_No_UserId()
        {
            var result = await _sut.RevokeVerification(1);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task RevokeVerification_Should_Return_200_When_Success()
        {
            SetUserIdInContext(5);

            var dto = new RequestDetailResponse { Id = 1 };

            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = true,
                Data = dto
            };

            _service.Setup(x => x.RevokeVerificationAsync(1, 5))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.RevokeVerification(1);

            result.Should().BeOfType<OkObjectResult>();
        }

        // =========================
        // GET REQUEST DETAILS
        // =========================

        [Fact]
        public async Task GetRequestDetails_Should_Return_404_When_NotFound()
        {
            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = false,
                StatusCode = 404
            };

            _service.Setup(x => x.GetRequestDetailsAsync(1))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.GetRequestDetails(1);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetRequestDetails_Should_Return_200_When_Success()
        {
            var dto = new RequestDetailResponse { Id = 1 };

            var serviceResult = new ServiceResult<RequestDetailResponse>
            {
                Success = true,
                Data = dto
            };

            _service.Setup(x => x.GetRequestDetailsAsync(1))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.GetRequestDetails(1);

            result.Should().BeOfType<OkObjectResult>();
        }

        // =========================
        // FILTER
        // =========================

        [Fact]
        public async Task GetFilterdData_Should_Return_400_With_401_In_Body_When_No_UserId()
        {
            var result = await _sut.GetFilterdData(new Filter());

            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;

            bad.StatusCode.Should().Be(400);

            var api = bad.Value as ApiResponse<object>;

            api!.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task GetFilterdData_Should_Return_200_When_Success()
        {
            SetUserIdInContext(5);

            var filter = new Filter();

            var list = new List<RequestDetailResponse>
            {
                new RequestDetailResponse { Id = 1 }
            };

            var serviceResult = new ServiceResult<List<RequestDetailResponse>>
            {
                Success = true,
                Data = list
            };

            _service.Setup(x => x.getAllFilterRequest(It.Is<Filter>(f => f.mineId == 5)))
                    .ReturnsAsync(serviceResult);

            var result = await _sut.GetFilterdData(filter);

            result.Should().BeOfType<OkObjectResult>();
        }
    }
}