using Microsoft.EntityFrameworkCore;
using VERIFY.Model;

namespace VERIFY.Repositories
{
      public class VerifyRepository : IVerifyRepository
    {
        private readonly VerifyDbContext _db;

        public VerifyRepository(VerifyDbContext db)
        {
            _db = db;
        }

        public async Task<VerifyProductTable?> GetByProductIdAsync(int productId)
        {
            return await _db.VERIFY_PRODUCTS
                .FirstOrDefaultAsync(v => v.ProductId == productId);
        }

        public async Task<List<VerifyProductTable>> GetVerifiedByAdminAsync(int adminId, string? searchName = null)
        {
            var query = _db.VERIFY_PRODUCTS
                .AsNoTracking()
                .Where(v => v.VerifierId == adminId && v.isProductVerified);

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(v => EF.Functions.Like(v.ProductName, $"%{searchName}%"));
            }

            return await query
                .OrderByDescending(v => v.VerifiedTime)
                .ToListAsync();
        }

        public async Task<HashSet<int>> GetVerifiedProductIdsAsync()
        {
            var ids = await _db.VERIFY_PRODUCTS
                .AsNoTracking()
                .Where(v => v.isProductVerified)
                .Select(v => v.ProductId)
                .ToListAsync();

            return new HashSet<int>(ids);
        }

        public async Task AddAsync(VerifyProductTable entity)
        {
            await _db.VERIFY_PRODUCTS.AddAsync(entity);
        }

        public void Update(VerifyProductTable entity)
        {
            _db.VERIFY_PRODUCTS.Update(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
