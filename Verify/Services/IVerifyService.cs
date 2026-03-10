using VERIFY.DTOs.Requests;
using VERIFY.DTOs.Responses;

namespace VERIFY.Services
{
 
    public interface IVerifyService
    {
        Task<ServiceResult<object>> VerifyProductAsync(int adminId, VerifyProductRequest request);

        Task<ServiceResult<object>> UnverifyProductAsync(int adminId, int productId, string? description);

        Task<ServiceResult<VerifyStatusResponse>> GetVerifyStatusAsync(int productId);

        Task<ServiceResult<List<VerifiedProductDetail>>> GetProductsVerifiedByMeAsync(
            int adminId, string? searchName, string? authorizationHeader,int page,int size);

        Task<ServiceResult<List<object>>> GetUnverifiedProductsAsync(
            int adminId, string? searchName, string? authorizationHeader,int page,int size);
    }
}
