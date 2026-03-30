using ADMIN.Data.Dto;
using ADMIN.DTOs.Responses;
using ADMIN.Model;
using ADMIN.Repositories;
using ADMIN.Services;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Admin.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="RequestService"/>.
    /// Covers all public scenarios with xUnit + Moq + FluentAssertions.
    /// </summary>
    public class RequestServiceTests
    {
        // ---- shared mocks ----
        private readonly Mock<IRequestRepository>     _repo;
        private readonly Mock<IMapper>                _mapper;
        private readonly Mock<ILogger<RequestService>> _logger;
        private readonly RequestService               _sut;

        public RequestServiceTests()
        {
            _repo   = new Mock<IRequestRepository>();
            _mapper = new Mock<IMapper>();
            _logger = new Mock<ILogger<RequestService>>();
            _sut    = new RequestService(_repo.Object, _mapper.Object, _logger.Object);
        }

        [Fact]
        public async Task VerifyRequest_Should_Return_400_When_RequestId_Is_Zero()
        {
            // Act
            var result = await _sut.VerifyRequestAsync(0, 1);

            // Assert
            result.StatusCode.Should().Be(400);
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Invalid RequestId");
            _repo.Verify(r => r.GetRequestByUserIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task VerifyRequest_Should_Return_400_When_RequestId_Is_Negative()
        {
            var result = await _sut.VerifyRequestAsync(-5, 1);

            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task VerifyRequest_Should_Return_404_When_Request_Not_Found()
        {
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync((RequestTable?)null);

            var result = await _sut.VerifyRequestAsync(1, 10);

            result.StatusCode.Should().Be(404);
            result.Message.Should().Contain("not found");
        }

        [Fact]
        public async Task VerifyRequest_Should_Return_400_When_Already_Verified()
        {
            var request = new RequestTable { Id = 1, VerifiedByAdmin = true };
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);

            var result = await _sut.VerifyRequestAsync(1, 10);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("already been verified");
        }

        [Fact]
        public async Task VerifyRequest_Should_Return_200_And_Set_VerifiedFields_When_Valid()
        {
            var adminId  = 42;
            var request  = new RequestTable { Id = 1, VerifiedByAdmin = false };
            var response = new RequestDetailResponse { Id = 1, VerifiedByAdmin = true };

            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);
            _repo.Setup(r => r.UpdateRequestAsync(request)).ReturnsAsync(true);
            _mapper.Setup(m => m.Map<RequestDetailResponse>(request)).Returns(response);

            var result = await _sut.VerifyRequestAsync(1, adminId);

            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();

            // Service must set these fields before persisting
            request.VerifiedByAdmin.Should().BeTrue();
            request.VerifierId.Should().Be(adminId);
            request.VerifiedAt.Should().NotBeNull();

            _repo.Verify(r => r.UpdateRequestAsync(request), Times.Once);
        }

        [Fact]
        public async Task GrantUserRights_Should_Return_400_When_RequestId_Is_Zero()
        {
            var result = await _sut.GrantUserRightsAsync(0, 1);
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task GrantUserRights_Should_Return_404_When_Request_Not_Found()
        {
            _repo.Setup(r => r.GetRequestByUserIdAsync(5)).ReturnsAsync((RequestTable?)null);

            var result = await _sut.GrantUserRightsAsync(5, 1);

            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GrantUserRights_Should_Return_400_When_Request_Not_Verified_By_Admin()
        {
            var request = new RequestTable { Id = 1, VerifiedByAdmin = false };
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);

            var result = await _sut.GrantUserRightsAsync(1, 1);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("verified by admin first");
        }

        [Fact]
        public async Task GrantUserRights_Should_Return_400_When_User_Already_Has_Rights()
        {
            var request = new RequestTable { Id = 1, VerifiedByAdmin = true, RightToAdd = true, VerifierId = 1 };
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);

            var result = await _sut.GrantUserRightsAsync(1, 1);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("already has the right");
        }

        [Fact]
        public async Task GrantUserRights_Should_Return_403_When_Admin_Is_Different_From_Verifier()
        {
            var request = new RequestTable { Id = 1, VerifiedByAdmin = true, RightToAdd = false, VerifierId = 99 };
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);

            var result = await _sut.GrantUserRightsAsync(1, 1); // caller is admin 1 but verifier was 99

            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task GrantUserRights_Should_Return_200_And_Set_RightToAdd_When_Valid()
        {
            var adminId  = 7;
            var request  = new RequestTable { Id = 1, VerifiedByAdmin = true, RightToAdd = false, VerifierId = adminId };
            var response = new RequestDetailResponse { Id = 1, HasRightToAdd = true };

            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);
            _repo.Setup(r => r.UpdateRequestAsync(request)).ReturnsAsync(true);
            _mapper.Setup(m => m.Map<RequestDetailResponse>(request)).Returns(response);

            var result = await _sut.GrantUserRightsAsync(1, adminId);

            result.StatusCode.Should().Be(200);
            result.Success.Should().BeTrue();
            request.RightToAdd.Should().BeTrue();
            request.RightsGrantedAt.Should().NotBeNull();
        }

       

        [Fact]
        public async Task RevokeUserRights_Should_Return_400_When_RequestId_Is_Zero()
        {
            var result = await _sut.RevokeUserRightsAsync(0, 1);
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task RevokeUserRights_Should_Return_404_When_Request_Not_Found()
        {
            _repo.Setup(r => r.GetRequestByUserIdAsync(3)).ReturnsAsync((RequestTable?)null);
            var result = await _sut.RevokeUserRightsAsync(3, 1);
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task RevokeUserRights_Should_Return_400_When_User_Has_No_Rights()
        {
            var request = new RequestTable { Id = 1, RightToAdd = false };
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);

            var result = await _sut.RevokeUserRightsAsync(1, 1);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("does not have the right");
        }

        [Fact]
        public async Task RevokeUserRights_Should_Return_403_When_Admin_Is_Not_Original_Verifier()
        {
            var request = new RequestTable { Id = 1, RightToAdd = true, VerifierId = 99 };
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);

            var result = await _sut.RevokeUserRightsAsync(1, 1);

            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task RevokeUserRights_Should_Return_200_And_Clear_RightToAdd_When_Valid()
        {
            var adminId  = 5;
            var request  = new RequestTable { Id = 1, RightToAdd = true, VerifierId = adminId };
            var response = new RequestDetailResponse { Id = 1, HasRightToAdd = false };

            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);
            _repo.Setup(r => r.UpdateRequestAsync(request)).ReturnsAsync(true);
            _mapper.Setup(m => m.Map<RequestDetailResponse>(request)).Returns(response);

            var result = await _sut.RevokeUserRightsAsync(1, adminId);

            result.StatusCode.Should().Be(200);
            request.RightToAdd.Should().BeFalse();
        }

       

        [Fact]
        public async Task RevokeVerification_Should_Return_400_When_RequestId_Is_Zero()
        {
            var result = await _sut.RevokeVerificationAsync(0, 1);
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task RevokeVerification_Should_Return_404_When_Request_Not_Found()
        {
            _repo.Setup(r => r.GetRequestByUserIdAsync(2)).ReturnsAsync((RequestTable?)null);
            var result = await _sut.RevokeVerificationAsync(2, 1);
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task RevokeVerification_Should_Return_400_When_Request_Is_Already_Unverified()
        {
            var request = new RequestTable { Id = 1, VerifiedByAdmin = false };
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);

            var result = await _sut.RevokeVerificationAsync(1, 1);

            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("already unverified");
        }

        [Fact]
        public async Task RevokeVerification_Should_Return_403_When_Admin_Is_Not_Original_Verifier()
        {
            var request = new RequestTable { Id = 1, VerifiedByAdmin = true, VerifierId = 99 };
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);

            var result = await _sut.RevokeVerificationAsync(1, 1);

            result.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task RevokeVerification_Should_Return_200_And_Reset_Fields_When_Valid()
        {
            var adminId  = 3;
            var request  = new RequestTable
            {
                Id              = 1,
                VerifiedByAdmin = true,
                VerifierId      = adminId,
                RightToAdd      = true,
                VerifiedAt      = DateTime.UtcNow,
                RightsGrantedAt = DateTime.UtcNow
            };
            var response = new RequestDetailResponse { Id = 1, VerifiedByAdmin = false };

            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);
            _repo.Setup(r => r.UpdateRequestAsync(request)).ReturnsAsync(true);
            _mapper.Setup(m => m.Map<RequestDetailResponse>(request)).Returns(response);

            var result = await _sut.RevokeVerificationAsync(1, adminId);

            result.StatusCode.Should().Be(200);
            request.VerifiedByAdmin.Should().BeFalse();
            request.VerifierId.Should().Be(0);
            request.VerifiedAt.Should().BeNull();
            request.RightToAdd.Should().BeFalse();
            request.RightsGrantedAt.Should().BeNull();
        }

        

        [Fact]
        public async Task GetRequestDetails_Should_Return_400_When_Id_Is_Zero()
        {
            var result = await _sut.GetRequestDetailsAsync(0);
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task GetRequestDetails_Should_Return_404_When_Request_Not_Found()
        {
            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync((RequestTable?)null);
            var result = await _sut.GetRequestDetailsAsync(1);
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GetRequestDetails_Should_Return_200_With_Mapped_Response()
        {
            var request  = new RequestTable { Id = 1 };
            var response = new RequestDetailResponse { Id = 1 };

            _repo.Setup(r => r.GetRequestByUserIdAsync(1)).ReturnsAsync(request);
            _mapper.Setup(m => m.Map<RequestDetailResponse>(request)).Returns(response);

            var result = await _sut.GetRequestDetailsAsync(1);

            result.StatusCode.Should().Be(200);
            result.Data.Should().NotBeNull();
            result.Data!.Id.Should().Be(1);
        }

        

        [Fact]
        public async Task GetUserRequests_Should_Return_400_When_UserId_Is_Zero()
        {
            var result = await _sut.GetUserRequestsAsync(0);
            result.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task GetUserRequests_Should_Return_200_With_Mapped_List()
        {
            var requests = new List<RequestTable> { new RequestTable { Id = 1 } };
            var dtos     = new List<RequestDetailResponse> { new RequestDetailResponse { Id = 1 } };

            _repo.Setup(r => r.GetRequestsByVerifierIdAsync(5)).ReturnsAsync(requests);
            _mapper.Setup(m => m.Map<List<RequestDetailResponse>>(requests)).Returns(dtos);

            var result = await _sut.GetUserRequestsAsync(5);

            result.StatusCode.Should().Be(200);
            result.Data.Should().HaveCount(1);
        }

     



        [Fact]
        public async Task GetAllFilterRequest_Should_Return_Error_When_MineId_Is_Zero()
        {
            var filter = new Filter { mineId = 0 };
            var result = await _sut.getAllFilterRequest(filter);
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Id Is Not Found");
        }

        [Fact]
        public async Task GetAllFilterRequest_Should_Return_404_When_User_Request_Not_Found()
        {
            var filter = new Filter { mineId = 5 };
            _repo.Setup(r => r.GetRequestByUserIdAsync(5)).ReturnsAsync((RequestTable?)null);

            var result = await _sut.getAllFilterRequest(filter);

            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task GetAllFilterRequest_Should_Return_Empty_List_When_No_Filtered_Data()
        {
            var filter   = new Filter { mineId = 5 };
            var selfReq  = new RequestTable { Id = 5 };

            _repo.Setup(r => r.GetRequestByUserIdAsync(5)).ReturnsAsync(selfReq);
            _repo.Setup(r => r.getFilteredData(filter)).ReturnsAsync(new List<RequestTable>());

            var result = await _sut.getAllFilterRequest(filter);

            result.StatusCode.Should().Be(200);
            result.Data.Should().NotBeNull();
            result.Data!.Count.Should().Be(0);
        }

        [Fact]
        public async Task GetAllFilterRequest_Should_Return_200_With_Filtered_Data()
        {
            var filter   = new Filter { mineId = 5 };
            var selfReq  = new RequestTable { Id = 5 };
            var requests = new List<RequestTable> { new RequestTable { Id = 1 }, new RequestTable { Id = 2 } };
            var dtos     = new List<RequestDetailResponse> { new() { Id = 1 }, new() { Id = 2 } };

            _repo.Setup(r => r.GetRequestByUserIdAsync(5)).ReturnsAsync(selfReq);
            _repo.Setup(r => r.getFilteredData(filter)).ReturnsAsync(requests);
            _mapper.Setup(m => m.Map<List<RequestDetailResponse>>(requests)).Returns(dtos);

            var result = await _sut.getAllFilterRequest(filter);

            result.StatusCode.Should().Be(200);
            result.Data.Should().HaveCount(2);
        }
    }
}
