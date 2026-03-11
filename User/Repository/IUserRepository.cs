using System.Collections.Generic;
using System.Threading.Tasks;
using USER.Data.Dto;
using USER.Model;

namespace USER.Repository
{
    public interface IUserRepository
    {
        Task<UserTable?> GetByEmailAsync(string email);
        Task<UserTable?> GetByIdAsync(int id);
        Task<UserTable> AddAsync(UserTable user);
        Task<UserTable> RemoveAsync(UserTable user);
        Task<UserTable> UpdateAsync(UserTable user);
        Task<List<UserTable>> GetUsersByIdsAsync(IEnumerable<int> ids,int page,int size);
        Task<List<UserTable>> GetAllUsersAsync();
        Task<UserTable> changeFields(changeProfileDto profile,int id);
    }
}
