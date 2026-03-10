using PRODUCT.Data.Dto.Request;
using PRODUCT.Model;
using PRODUCT.Data.Dto.Response;

namespace PRODUCT.Repository
{
    public interface Irepository
    {
        public Task<ProductTable> Add(ProductTable product);
        public Task<bool> exist(string name);
        public Task<ProductTable> Update(ProductTable product);
        public Task<List<ProductTable>> getProduct();
        public Task<ProductTable> deleteProduct(ProductTable product);
        public Task<ProductTable> getByIdProduct(int id);
        public Task<IEnumerable<ProductTable>> AllProducts(ProductAll query);
    }
}