using System.Collections.Generic;
using System.Threading.Tasks;
using ADMIN.Data.Dto;
using ADMIN.DTOs.Responses;
using Microsoft.AspNetCore.Mvc;

namespace ADMIN.Services
{
    public interface IRequestService
    {
        Task<ServiceResult<RequestDetailResponse>> VerifyRequestAsync(int requestId, int userid);
        Task<ServiceResult<RequestDetailResponse>> GrantUserRightsAsync(int requestId, int userid);
        Task<ServiceResult<RequestDetailResponse>> RevokeUserRightsAsync(int requestId, int userid);
        Task<ServiceResult<RequestDetailResponse>> RevokeVerificationAsync(int requestId, int userid);
        Task<ServiceResult<RequestDetailResponse>> GetRequestDetailsAsync(int id);
        Task<ServiceResult<List<RequestDetailResponse>>> GetUserRequestsAsync(int userId);
        Task<ServiceResult<List<RequestDetailResponse>>> GetPendingRequestsAsync();
        Task<ServiceResult<List<RequestDetailResponse>>> GetVerifiedRequestsAsync(int id);
        Task<ServiceResult<object>> GetDashboardAsync();
        Task<ServiceResult<List<RequestDetailResponse>>> getAllFilterRequest(Filter filter);
    }
}
