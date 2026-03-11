using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using USER.Data.Dto;
using USER.Data.Dto.Response;

namespace USER.Services
{
    public interface IUserService
    {
        Task<ServiceResult<UserDetail>> CreateUserAsync(UserCreateDto user);
        Task<ServiceResult<UserDetail>> ChangeProfileAsync(int userId, changeProfileDto docs);
        Task<ServiceResult<UserDetail>> GetProfileAsync(int userId);
    }
}
