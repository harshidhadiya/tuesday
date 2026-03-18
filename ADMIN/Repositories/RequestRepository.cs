using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ADMIN.Data.Dto;
using ADMIN.Model;
using Microsoft.EntityFrameworkCore;

namespace ADMIN.Repositories
{
    public class RequestRepository : IRequestRepository
    {
        private readonly MACUTIONDB _db;

        public RequestRepository(MACUTIONDB db)
        {
            _db = db;
        }

        public async Task<RequestTable?> GetRequestByUserIdAsync(int requestUserId)
        {
            return await _db.REQUESTS.Where(x => x.RequestUserId == requestUserId).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateRequestAsync(RequestTable request)
        {
            _db.REQUESTS.Update(request);
            var result = await _db.SaveChangesAsync();
            return result > 0;
        }

        public async Task<List<RequestTable>> GetRequestsByVerifierIdAsync(int verifierId)
        {
            return await _db.REQUESTS
                .Where(r => r.VerifierId == verifierId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<RequestTable>> GetPendingRequestsAsync()
        {
            return await _db.REQUESTS
                .Where(r => !r.VerifiedByAdmin)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<RequestTable>> GetVerifiedRequestsAsync()
        {
            return await _db.REQUESTS
                .Where(r => r.VerifiedByAdmin)
                .AsNoTracking()
                .ToListAsync();
        }
        public async Task<List<RequestTable>> getFilteredData(Filter filter)
        {
            var data = _db.REQUESTS.AsNoTracking().AsQueryable();

            // Email filter
            if (!string.IsNullOrWhiteSpace(filter.email))
                data = data.Where(x => EF.Functions.Like(x.Email, $"%{filter.email}%"));

            // From date
            if (filter.From.HasValue)
                data = data.Where(x => x.VerifiedAt >= filter.From.Value);

            // To date (FIXED)
            if (filter.To.HasValue)
            {
                var toDate = filter.To.Value.Date.AddDays(1);
                data = data.Where(x => x.VerifiedAt < toDate);
            }
            if(filter.pending)
             data=data.Where(x=>!x.VerifiedByAdmin);
             else
             data=data.Where(x=>x.VerifiedByAdmin);
            // Mine filter
            if (filter.mine)
                data = data.Where(x => x.VerifierId == filter.mineId);

            // Name filter
            if (!string.IsNullOrWhiteSpace(filter.name))
                data = data.Where(x => EF.Functions.Like(x.Name, $"%{filter.name}%"));
            
            return await data.Skip((filter.page-1)*filter.pageSize).Take(filter.pageSize).ToListAsync();


        }
        public async Task<int> GetPendingCountAsync()
        {
            return await _db.REQUESTS.CountAsync(r => !r.VerifiedByAdmin);
        }

        public async Task<int> GetVerifiedCountAsync()
        {
            return await _db.REQUESTS.CountAsync(r => r.VerifiedByAdmin);
        }
    }
}
