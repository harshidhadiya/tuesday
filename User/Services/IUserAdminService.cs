using USER.Data.Dto;
using USER.Data.Dto.Response;

namespace USER.Services
{
    public interface IUserAdminService
    {
     
        Task<ServiceResult<AdminDetail>> GetProfileAsync(int userId);
    }
}
