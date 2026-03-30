using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using USER.Data.Dto;
using USER.Model;

namespace USER.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly MACUTIONDB _db;
        private readonly PasswordHasher<object> _hash;
        public UserRepository(MACUTIONDB db)
        {
            _db = db;
            this._hash=new();
            
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


       
        public async Task<UserTable> changeFields(changeProfileDto profile,int userId)
        {
             var currentUser = await _db.USERS.FindAsync(userId);

                if (currentUser == null)
                    return null;

                if (!string.IsNullOrWhiteSpace(profile.Name))
                    currentUser.Name = profile.Name;

                if (!string.IsNullOrWhiteSpace(profile.Email))
                    currentUser.Email = profile.Email;

                if (!string.IsNullOrWhiteSpace(profile.Phone))
                    currentUser.Phone = profile.Phone;

                if (!string.IsNullOrWhiteSpace(profile.Address))
                    currentUser.Address = profile.Address;

                if (!string.IsNullOrWhiteSpace(profile.ProfilePicture))
                    currentUser.ProfilePicture = profile.ProfilePicture;

                if(!string.IsNullOrEmpty(profile.Password))
                {
                    var hashedPassword = _hash.HashPassword(new object(), profile.Password);
                    currentUser.HashPassword=hashedPassword;
                }
                if(!string.IsNullOrEmpty(profile.publicId))
                currentUser.publicPictureId=profile.publicId;
                await _db.SaveChangesAsync();
                return currentUser;
        }
    }
}
