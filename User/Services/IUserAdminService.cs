using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using USER.Data.Dto;
using USER.Data.Dto.Response;

namespace USER.Services
{
    public interface IUserAdminService
    {
        Task<ServiceResult<List<verifiedAdminResponse>>> GetAllVerifiedRequestsAsync(int userId,int page,int size);
        Task<ServiceResult<List<pendingVerificationResponse>>> GetAllPendingRequestsAsync(int page=1, int size=10);
        Task<ServiceResult<AdminDetail>> GetProfileAsync(int userId);
    }
}
