using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using USER.Model;

namespace USER.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly MACUTIONDB _db;

        public UserRepository(MACUTIONDB db)
        {
            _db = db;
        }

        public async Task<UserTable?> GetByEmailAsync(string email)
        {
            return await _db.USERS.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<UserTable?> GetByIdAsync(int id)
        {
            return await _db.USERS.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<UserTable> AddAsync(UserTable user)
        {
            _db.USERS.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task<UserTable> RemoveAsync(UserTable user)
        {
            _db.USERS.Remove(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task<UserTable> UpdateAsync(UserTable user)
        {
            _db.USERS.Update(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task<List<UserTable>> GetUsersByIdsAsync(IEnumerable<int> ids)
        {
            return await _db.USERS.Where(u => ids.Contains(u.Id)).ToListAsync();
        }

        public async Task<List<UserTable>> GetAllUsersAsync()
        {
            return await _db.USERS.ToListAsync();
        }
    }
}
