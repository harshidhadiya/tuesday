using USER.Data.Dto;
using USER.Data.Dto.Response;

namespace USER.Services
{
    public interface IUserService
    {
        Task<ServiceResult<OwnDetail>> CreateUserAsync(UserCreateDto user);
        Task<ServiceResult<OwnDetail>> ChangeProfileAsync(int userId, changeProfileDto docs);
        Task<ServiceResult<UserDetail>> GetProfileAsync(int userId);
    }
}
