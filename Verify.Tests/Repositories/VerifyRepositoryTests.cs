using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VERIFY.Data.Dto;
using VERIFY.Model;
using VERIFY.Repositories;
using Xunit;

namespace Verify.Tests.Repositories
{
    public class VerifyRepositoryTests : IDisposable
    {
        private readonly VerifyDbContext _db;
        private readonly VerifyRepository _sut;

        public VerifyRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<VerifyDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _db = new VerifyDbContext(options);
            _sut = new VerifyRepository(_db);
        }

        public void Dispose()
        {
            _db.Dispose();
        }

        // ───────────────────────────────────────────────────────────────
        // Helper
        // ───────────────────────────────────────────────────────────────
        private async Task SeedAsync(params VerifyProductTable[] items)
        {
            _db.VERIFY_PRODUCTS.AddRange(items);
            await _db.SaveChangesAsync();
        }

        // ───────────────────────────────────────────────────────────────
        // GetByProductIdAsync
        // ───────────────────────────────────────────────────────────────
        [Fact]
        public async Task GetByProductIdAsync_Should_Return_Entity_When_Found()
        {
            await SeedAsync(new VerifyProductTable
            {
                ProductId = 10,
                SellerId = 1,
                ProductName = "Laptop",
                Product_description = "A laptop",
                isProductVerified = false
            });

            var result = await _sut.GetByProductIdAsync(10);

            result.Should().NotBeNull();
            result!.ProductId.Should().Be(10);
            result.ProductName.Should().Be("Laptop");
        }

        [Fact]
        public async Task GetByProductIdAsync_Should_Return_Null_When_Not_Found()
        {
            var result = await _sut.GetByProductIdAsync(999);

            result.Should().BeNull();
        }

        // ───────────────────────────────────────────────────────────────
        // GetVerifiedByAdminAsync
        // ───────────────────────────────────────────────────────────────
        [Fact]
        public async Task GetVerifiedByAdminAsync_Should_Return_Only_Verified_Products_By_Admin()
        {
            await SeedAsync(
                new VerifyProductTable
                {
                    ProductId = 1, SellerId = 1, VerifierId = 5,
                    isProductVerified = true, ProductName = "Item A",
                    Product_description = "desc", VerifiedTime = DateTime.UtcNow.AddMinutes(-10)
                },
                new VerifyProductTable
                {
                    ProductId = 2, SellerId = 2, VerifierId = 5,
                    isProductVerified = false, ProductName = "Item B",
                    Product_description = "desc"
                },
                new VerifyProductTable
                {
                    ProductId = 3, SellerId = 3, VerifierId = 99,
                    isProductVerified = true, ProductName = "Item C",
                    Product_description = "desc", VerifiedTime = DateTime.UtcNow
                }
            );

            var result = await _sut.GetVerifiedByAdminAsync(5);

            result.Should().HaveCount(1);
            result[0].ProductId.Should().Be(1);
        }

        [Fact]
        public async Task GetVerifiedByAdminAsync_Should_Return_Empty_When_No_Match()
        {
            await SeedAsync(new VerifyProductTable
            {
                ProductId = 1, SellerId = 1, VerifierId = 5,
                isProductVerified = false, ProductName = "Item",
                Product_description = "desc"
            });

            var result = await _sut.GetVerifiedByAdminAsync(5);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetVerifiedByAdminAsync_With_SearchName_Should_Filter_By_Name()
        {
            await SeedAsync(
                new VerifyProductTable
                {
                    ProductId = 1, SellerId = 1, VerifierId = 5,
                    isProductVerified = true, ProductName = "Gaming Laptop",
                    Product_description = "desc", VerifiedTime = DateTime.UtcNow
                },
                new VerifyProductTable
                {
                    ProductId = 2, SellerId = 2, VerifierId = 5,
                    isProductVerified = true, ProductName = "Office Chair",
                    Product_description = "desc", VerifiedTime = DateTime.UtcNow
                }
            );

            var result = await _sut.GetVerifiedByAdminAsync(5, "Laptop");

            result.Should().HaveCount(1);
            result[0].ProductName.Should().Be("Gaming Laptop");
        }

        [Fact]
        public async Task GetVerifiedByAdminAsync_With_Null_SearchName_Should_Return_All_Verified()
        {
            await SeedAsync(
                new VerifyProductTable
                {
                    ProductId = 1, SellerId = 1, VerifierId = 5,
                    isProductVerified = true, ProductName = "Item A",
                    Product_description = "desc", VerifiedTime = DateTime.UtcNow.AddMinutes(-5)
                },
                new VerifyProductTable
                {
                    ProductId = 2, SellerId = 2, VerifierId = 5,
                    isProductVerified = true, ProductName = "Item B",
                    Product_description = "desc", VerifiedTime = DateTime.UtcNow
                }
            );

            var result = await _sut.GetVerifiedByAdminAsync(5, null);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetVerifiedByAdminAsync_With_Empty_SearchName_Should_Return_All_Verified()
        {
            await SeedAsync(
                new VerifyProductTable
                {
                    ProductId = 1, SellerId = 1, VerifierId = 5,
                    isProductVerified = true, ProductName = "Item A",
                    Product_description = "desc", VerifiedTime = DateTime.UtcNow
                },
                new VerifyProductTable
                {
                    ProductId = 2, SellerId = 2, VerifierId = 5,
                    isProductVerified = true, ProductName = "Item B",
                    Product_description = "desc", VerifiedTime = DateTime.UtcNow
                }
            );

            var result = await _sut.GetVerifiedByAdminAsync(5, "   ");

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetVerifiedByAdminAsync_Should_Order_By_VerifiedTime_Descending()
        {
            var older = DateTime.UtcNow.AddHours(-2);
            var newer = DateTime.UtcNow;

            await SeedAsync(
                new VerifyProductTable
                {
                    ProductId = 1, SellerId = 1, VerifierId = 5,
                    isProductVerified = true, ProductName = "Older",
                    Product_description = "desc", VerifiedTime = older
                },
                new VerifyProductTable
                {
                    ProductId = 2, SellerId = 2, VerifierId = 5,
                    isProductVerified = true, ProductName = "Newer",
                    Product_description = "desc", VerifiedTime = newer
                }
            );

            var result = await _sut.GetVerifiedByAdminAsync(5);

            result[0].ProductName.Should().Be("Newer");
            result[1].ProductName.Should().Be("Older");
        }

        // ───────────────────────────────────────────────────────────────
        // GetVerifiedProductIdsAsync
        // ───────────────────────────────────────────────────────────────
        [Fact]
        public async Task GetVerifiedProductIdsAsync_Should_Return_Only_Verified_Ids()
        {
            await SeedAsync(
                new VerifyProductTable
                {
                    ProductId = 1, SellerId = 1, isProductVerified = true,
                    ProductName = "A", Product_description = "desc"
                },
                new VerifyProductTable
                {
                    ProductId = 2, SellerId = 2, isProductVerified = false,
                    ProductName = "B", Product_description = "desc"
                },
                new VerifyProductTable
                {
                    ProductId = 3, SellerId = 3, isProductVerified = true,
                    ProductName = "C", Product_description = "desc"
                }
            );

            var result = await _sut.GetVerifiedProductIdsAsync();

            result.Should().BeOfType<HashSet<int>>();
            result.Should().HaveCount(2);
            result.Should().Contain(1);
            result.Should().Contain(3);
            result.Should().NotContain(2);
        }

        [Fact]
        public async Task GetVerifiedProductIdsAsync_Should_Return_Empty_When_None_Verified()
        {
            await SeedAsync(new VerifyProductTable
            {
                ProductId = 1, SellerId = 1, isProductVerified = false,
                ProductName = "A", Product_description = "desc"
            });

            var result = await _sut.GetVerifiedProductIdsAsync();

            result.Should().BeEmpty();
        }

        // ───────────────────────────────────────────────────────────────
        // AddAsync
        // ───────────────────────────────────────────────────────────────
        [Fact]
        public async Task AddAsync_Should_Add_Entity_To_Context()
        {
            var entity = new VerifyProductTable
            {
                ProductId = 42, SellerId = 1,
                ProductName = "New Product", Product_description = "desc",
                isProductVerified = false
            };

            await _sut.AddAsync(entity);
            await _sut.SaveChangesAsync();

            var saved = await _db.VERIFY_PRODUCTS.FirstOrDefaultAsync(v => v.ProductId == 42);
            saved.Should().NotBeNull();
            saved!.ProductName.Should().Be("New Product");
        }

        // ───────────────────────────────────────────────────────────────
        // Update
        // ───────────────────────────────────────────────────────────────
        [Fact]
        public async Task Update_Should_Mark_Entity_As_Modified()
        {
            var entity = new VerifyProductTable
            {
                ProductId = 10, SellerId = 1,
                ProductName = "Before", Product_description = "desc",
                isProductVerified = false
            };
            await SeedAsync(entity);

            entity.ProductName = "After";
            entity.isProductVerified = true;
            _sut.Update(entity);
            await _sut.SaveChangesAsync();

            var updated = await _db.VERIFY_PRODUCTS.FirstOrDefaultAsync(v => v.ProductId == 10);
            updated!.ProductName.Should().Be("After");
            updated.isProductVerified.Should().BeTrue();
        }

        // ───────────────────────────────────────────────────────────────
        // SaveChangesAsync
        // ───────────────────────────────────────────────────────────────
        [Fact]
        public async Task SaveChangesAsync_Should_Persist_Changes()
        {
            var entity = new VerifyProductTable
            {
                ProductId = 77, SellerId = 1,
                ProductName = "Persisted", Product_description = "desc",
                isProductVerified = false
            };

            await _db.VERIFY_PRODUCTS.AddAsync(entity);
            await _sut.SaveChangesAsync();

            var count = await _db.VERIFY_PRODUCTS.CountAsync();
            count.Should().Be(1);
        }

        // ───────────────────────────────────────────────────────────────
        // GetFilterdProduct — full branch coverage
        // ───────────────────────────────────────────────────────────────
        private async Task SeedFilterData()
        {
            await SeedAsync(
                new VerifyProductTable
                {
                    ProductId = 1, SellerId = 1, VerifierId = 10,
                    isProductVerified = false, ProductName = "Pending Phone",
                    Product_description = "desc"
                },
                new VerifyProductTable
                {
                    ProductId = 2, SellerId = 2, VerifierId = 10,
                    isProductVerified = true, ProductName = "Verified Laptop",
                    Product_description = "desc"
                },
                new VerifyProductTable
                {
                    ProductId = 3, SellerId = 3, VerifierId = 20,
                    isProductVerified = true, ProductName = "Verified Tablet",
                    Product_description = "desc"
                },
                new VerifyProductTable
                {
                    ProductId = 4, SellerId = 4, VerifierId = 20,
                    isProductVerified = false, ProductName = "Pending Camera",
                    Product_description = "desc"
                }
            );
        }

        [Fact]
        public async Task GetFilterdProduct_No_Filters_Should_Return_All_Paged()
        {
            await SeedFilterData();

            var filter = new FilterVerify { page = 1, pagesize = 10 };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(4);
        }

        [Fact]
        public async Task GetFilterdProduct_Pending_Only_Should_Return_Unverified()
        {
            await SeedFilterData();

            var filter = new FilterVerify
            {
                pending = true, verified = false,
                page = 1, pagesize = 10
            };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(2);
            result.Should().OnlyContain(x => !x.isProductVerified);
        }

        [Fact]
        public async Task GetFilterdProduct_Verified_Only_Should_Return_Verified()
        {
            await SeedFilterData();

            var filter = new FilterVerify
            {
                verified = true, pending = false,
                page = 1, pagesize = 10
            };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(2);
            result.Should().OnlyContain(x => x.isProductVerified);
        }

        [Fact]
        public async Task GetFilterdProduct_Both_Pending_And_Verified_Should_Skip_Both_Filters()
        {
            await SeedFilterData();

            // When both pending=true and verified=true, neither status filter fires
            var filter = new FilterVerify
            {
                pending = true, verified = true,
                page = 1, pagesize = 10
            };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(4);
        }

        [Fact]
        public async Task GetFilterdProduct_Mine_Should_Filter_By_VerifierId()
        {
            await SeedFilterData();

            var filter = new FilterVerify
            {
                mine = true, verifierId = 10,
                page = 1, pagesize = 10
            };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(2);
            result.Should().OnlyContain(x => x.VerifierId == 10);
        }

        [Fact]
        public async Task GetFilterdProduct_Mine_With_Pending_Should_Combine_Filters()
        {
            await SeedFilterData();

            var filter = new FilterVerify
            {
                mine = true, verifierId = 10,
                pending = true, verified = false,
                page = 1, pagesize = 10
            };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(1);
            result[0].ProductId.Should().Be(1);
            result[0].isProductVerified.Should().BeFalse();
            result[0].VerifierId.Should().Be(10);
        }

        [Fact]
        public async Task GetFilterdProduct_Name_Filter_Should_Search_By_Name()
        {
            await SeedFilterData();

            var filter = new FilterVerify
            {
                name = "Laptop",
                page = 1, pagesize = 10
            };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(1);
            result[0].ProductName.Should().Be("Verified Laptop");
        }

        [Fact]
        public async Task GetFilterdProduct_Name_Filter_Whitespace_Should_Be_Ignored()
        {
            await SeedFilterData();

            var filter = new FilterVerify
            {
                name = "   ",
                page = 1, pagesize = 10
            };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(4);
        }

        [Fact]
        public async Task GetFilterdProduct_Pagination_Page1_Should_Return_First_Page()
        {
            await SeedFilterData();

            var filter = new FilterVerify { page = 1, pagesize = 2 };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetFilterdProduct_Pagination_Page2_Should_Return_Second_Page()
        {
            await SeedFilterData();

            var filter = new FilterVerify { page = 2, pagesize = 2 };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetFilterdProduct_Pagination_Beyond_Data_Should_Return_Empty()
        {
            await SeedFilterData();

            var filter = new FilterVerify { page = 10, pagesize = 10 };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetFilterdProduct_All_Filters_Combined()
        {
            await SeedFilterData();

            var filter = new FilterVerify
            {
                verified = true, pending = false,
                mine = true, verifierId = 10,
                name = "Laptop",
                page = 1, pagesize = 10
            };

            var result = await _sut.GetFilterdProduct(filter);

            result.Should().HaveCount(1);
            result[0].ProductId.Should().Be(2);
        }
    }
}
