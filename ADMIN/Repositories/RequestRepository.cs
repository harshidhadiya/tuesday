using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
