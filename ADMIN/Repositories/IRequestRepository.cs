using System.Collections.Generic;
using System.Threading.Tasks;
using ADMIN.Model;

namespace ADMIN.Repositories
{
    public interface IRequestRepository
    {
        Task<RequestTable?> GetRequestByUserIdAsync(int requestUserId);
        Task<bool> UpdateRequestAsync(RequestTable request);
        Task<List<RequestTable>> GetRequestsByVerifierIdAsync(int verifierId);
        Task<List<RequestTable>> GetPendingRequestsAsync();
        Task<List<RequestTable>> GetVerifiedRequestsAsync();
        Task<int> GetPendingCountAsync();
        Task<int> GetVerifiedCountAsync();
    }
}
