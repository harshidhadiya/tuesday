using System.Collections.Generic;
using System.Threading.Tasks;
using ADMIN.Data.Dto;
using Microsoft.AspNetCore.Mvc;

namespace ADMIN.Services
{
    public interface IRequestService
    {
        Task<ApiResponse<RequestDetailDto>> VerifyRequestAsync(int requestId, int userid);
        Task<ApiResponse<RequestDetailDto>> GrantUserRightsAsync(int requestId, int userid);
        Task<ApiResponse<RequestDetailDto>> RevokeUserRightsAsync(int requestId, int userid);
        Task<ApiResponse<RequestDetailDto>> RevokeVerificationAsync(int requestId, int userid);
        Task<ApiResponse<RequestDetailDto>> GetRequestDetailsAsync(int id);
        Task<ApiResponse<List<RequestDetailDto>>> GetUserRequestsAsync(int userId);
        Task<ApiResponse<List<RequestDetailDto>>> GetPendingRequestsAsync();
        Task<ApiResponse<List<RequestDetailDto>>> GetVerifiedRequestsAsync();
        Task<ApiResponse<object>> GetDashboardAsync();
    }
}
