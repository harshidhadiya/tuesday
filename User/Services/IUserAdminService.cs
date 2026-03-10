using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using USER.Data.Dto;

namespace USER.Services
{
    public interface IUserAdminService
    {
        Task<ActionResult> RequestSignupAsync(UserCreateDto request);
        Task<ActionResult> GetAllVerifiedRequestsAsync(int userId);
        Task<ActionResult> GetAllPendingRequestsAsync();
        Task<ActionResult> GetAdminDashboardAsync(int userId);
    }
}
