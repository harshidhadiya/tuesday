using Microsoft.EntityFrameworkCore;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Model;
using PRODUCT.Data.Dto.Response;

namespace PRODUCT.Repository
{

    public class Repository : Irepository
    {
        private MACUTIONDB db { get; set; }

        public Repository(MACUTIONDB db)
        {
            this.db = db;
        }
        public async Task<ProductTable> Add(ProductTable product)
        {
            await db.PRODUCTS.AddAsync(product);
            await db.SaveChangesAsync();
            return product;
        }

        public async Task<ProductTable> deleteProduct(ProductTable product)
        {
            db.PRODUCTS.Remove(product);
            await db.SaveChangesAsync();
            return product;
        }

        public async Task<ProductTable> getByIdProduct(int id)
        {
            var response = await db.PRODUCTS.Where(x => x.Id == id).FirstOrDefaultAsync();
            return response;
        }

        public Task<List<ProductTable>> getProduct()
        {
            throw new NotImplementedException();
        }

        public async Task<ProductTable> Update(ProductTable product)
        {
            db.PRODUCTS.Update(product);
            await db.SaveChangesAsync();
            return product;
        }
        public async Task<IEnumerable<ProductTable>> AllProducts(ProductAll query)
        {
            IQueryable<ProductTable> products = db.PRODUCTS.AsQueryable();

            if (query.id != null)
                products = products.Where(x => x.Id == query.id);

            if (query.productId != null)
                products = products.Where(x => x.Id == query.productId);

            if (!string.IsNullOrEmpty(query.searchName))
                products = products.Where(x => x.product_name.Contains(query.searchName));

            if (query.buyFrom != null && query.buyFrom <= DateTime.Now)
                products = products.Where(x => x.Buy_Date >= query.buyFrom);

            if (query.buyTo != null && query.buyTo <= DateTime.Now)
                products = products.Where(x => x.Buy_Date <= query.buyTo);

            if (query.createdFrom != null && query.createdFrom <= DateTime.Now)
                products = products.Where(x => x.creation_date >= query.createdFrom);

            if (query.createdTo != null && query.createdTo <= DateTime.Now)
                products = products.Where(x => x.creation_date <= query.createdTo);

            if (query.verified)
                products = products.Where(x => x.isVerified == true);

            if (query.mine)
            {
                products = products.Where(x => x.user_id == query.id);
            }

            products = products
                .Skip((query.page - 1) * query.size)
                .Take(query.size);

            return await products.ToListAsync();
        }
    

        public async Task<bool> exist(string name)
        {
            var data = await db.PRODUCTS.Where(x => x.product_name == name).SingleOrDefaultAsync();
            if (data != null)
            {
                return true;
            }
            return false;
        }
    }
}