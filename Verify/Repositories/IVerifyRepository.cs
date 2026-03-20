using VERIFY.Data.Dto;
using VERIFY.Model;

namespace VERIFY.Repositories
{
    /// <summary>
    /// Data-access layer for verification records.
    /// Only contains database queries — no business logic.
    /// </summary>
    public interface IVerifyRepository
    {
        /// <summary>Get a verification record by product ID.</summary>
        Task<VerifyProductTable?> GetByProductIdAsync(int productId);

        /// <summary>Get all products verified by a specific admin, optionally filtered by name.</summary>
        Task<List<VerifyProductTable>> GetVerifiedByAdminAsync(int adminId, string? searchName = null);

        /// <summary>Get all product IDs that are currently verified.</summary>
        Task<HashSet<int>> GetVerifiedProductIdsAsync();

        /// <summary>Add a new verification record.</summary>
        Task AddAsync(VerifyProductTable entity);

        /// <summary>Mark an existing verification record as updated.</summary>
        void Update(VerifyProductTable entity);
        Task<List<VerifyProductTable>> GetFilterdProduct(FilterVerify filter);
        /// <summary>Save all pending changes to the database.</summary>
        Task SaveChangesAsync();
    }
}
