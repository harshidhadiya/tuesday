using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using USER.Data.Dto;

namespace USER.Services
{
    public interface IUserService
    {
        Task<ActionResult> CreateUserAsync(UserCreateDto user);
        Task<ActionResult> ChangePasswordAsync(int userId, changePasswordDto pass_obj);
        Task<ActionResult> ChangeProfileAsync(int userId, changeProfileDto docs);
        Task<ActionResult> GetProfileAsync(int userId);
        Task<ActionResult> GetUserByIdAsync(int id);
        Task<ActionResult> GetUserDashboardAsync(int userId);
    }
}
