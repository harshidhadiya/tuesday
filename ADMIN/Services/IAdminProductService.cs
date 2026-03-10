using System.Threading.Tasks;
using ADMIN.Data.Dto;

namespace ADMIN.Services
{
    public interface IAdminProductService
    {
        ApiResponse<object> VerifyProduct(ProductVerify request, int userid);
        ApiResponse<object> UnverifyProduct(int productId, int userid, string description);
    }
}
