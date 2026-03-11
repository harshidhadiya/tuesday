using System.Threading.Tasks;
using ADMIN.DTOs.Requests;

namespace ADMIN.Services
{
    public interface IAdminProductService
    {
        ServiceResult<object> VerifyProduct(ProductVerifyRequest request, int userid);
        ServiceResult<object> UnverifyProduct(int productId, int userid, string description);
    }
}
