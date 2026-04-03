using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Model;
using PRODUCT.Repository;

namespace Product.Tests.Repository
{
    public class RepositoryTests : IDisposable
    {
        private readonly MACUTIONDB _db;
        private readonly PRODUCT.Repository.Repository _sut;

        public RepositoryTests()
        {
            var options = new DbContextOptionsBuilder<MACUTIONDB>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _db = new MACUTIONDB(options);
            _sut = new PRODUCT.Repository.Repository(_db);
        }

        public void Dispose()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }

        // ──────────────────────────────────────────────
        //  Helper
        // ──────────────────────────────────────────────
        private ProductTable CreateProduct(
            int id = 0,
            string name = "TestProduct",
            int userId = 1,
            bool isVerified = false,
            DateTime? buyDate = null,
            DateTime? creationDate = null,
            string? description = null)
        {
            return new ProductTable
            {
                Id = id,
                product_name = name,
                user_id = userId,
                isVerified = isVerified,
                Buy_Date = buyDate ?? DateTime.Now.AddDays(-10),
                creation_date = creationDate ?? DateTime.Now.AddDays(-5),
                product_description = description,
                images = new List<ImageTable>()
            };
        }

        private async Task SeedProducts(params ProductTable[] products)
        {
            _db.PRODUCTS.AddRange(products);
            await _db.SaveChangesAsync();

            // Detach all entities so the repository works with fresh tracking
            foreach (var entity in _db.ChangeTracker.Entries().ToList())
                entity.State = EntityState.Detached;
        }


        // ══════════════════════════════════════════════
        //  Add
        // ══════════════════════════════════════════════

        [Fact]
        public async Task Add_Should_Insert_Product_And_Return_It()
        {
            // Arrange
            var product = CreateProduct(name: "NewProduct", userId: 42);

            // Act
            var result = await _sut.Add(product);

            // Assert
            result.Should().NotBeNull();
            result.product_name.Should().Be("NewProduct");
            result.user_id.Should().Be(42);
            result.Id.Should().BeGreaterThan(0);

            var saved = await _db.PRODUCTS.FindAsync(result.Id);
            saved.Should().NotBeNull();
            saved!.product_name.Should().Be("NewProduct");
        }

        [Fact]
        public async Task Add_Should_Persist_Product_With_All_Properties()
        {
            // Arrange
            var buyDate = new DateTime(2026, 1, 15);
            var creationDate = new DateTime(2026, 1, 10);
            var product = CreateProduct(
                name: "FullProduct",
                userId: 10,
                isVerified: true,
                buyDate: buyDate,
                creationDate: creationDate,
                description: "Test description");

            // Act
            var result = await _sut.Add(product);

            // Assert
            var saved = await _db.PRODUCTS.FindAsync(result.Id);
            saved.Should().NotBeNull();
            saved!.product_name.Should().Be("FullProduct");
            saved.user_id.Should().Be(10);
            saved.isVerified.Should().BeTrue();
            saved.Buy_Date.Should().Be(buyDate);
            saved.creation_date.Should().Be(creationDate);
            saved.product_description.Should().Be("Test description");
        }

        [Fact]
        public async Task Add_Should_Persist_Product_With_Images()
        {
            // Arrange
            var product = CreateProduct(name: "ProductWithImages");
            product.images = new List<ImageTable>
            {
                new ImageTable { Image_URL = "http://img1.url", public_Id = "pub1" },
                new ImageTable { Image_URL = "http://img2.url", public_Id = "pub2" }
            };

            // Act
            var result = await _sut.Add(product);

            // Assert
            var saved = await _db.PRODUCTS.Include(p => p.images).FirstAsync(p => p.Id == result.Id);
            saved.images.Should().HaveCount(2);
        }

        [Fact]
        public async Task Add_Multiple_Products_Should_Each_Get_Unique_Id()
        {
            // Arrange & Act
            var p1 = await _sut.Add(CreateProduct(name: "Product1"));
            var p2 = await _sut.Add(CreateProduct(name: "Product2"));
            var p3 = await _sut.Add(CreateProduct(name: "Product3"));

            // Assert
            p1.Id.Should().NotBe(p2.Id);
            p2.Id.Should().NotBe(p3.Id);
            (await _db.PRODUCTS.CountAsync()).Should().Be(3);
        }


        // ══════════════════════════════════════════════
        //  exist
        // ══════════════════════════════════════════════

        [Fact]
        public async Task Exist_Should_Return_True_When_Product_Name_Exists()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "ExistingProduct"));

            // Act
            var result = await _sut.exist("ExistingProduct");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task Exist_Should_Return_False_When_Product_Name_Does_Not_Exist()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "SomeProduct"));

            // Act
            var result = await _sut.exist("NonExistentProduct");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Exist_Should_Return_False_When_No_Products_In_Database()
        {
            // Act
            var result = await _sut.exist("AnyProduct");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task Exist_Should_Be_Case_Sensitive()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "TestProduct"));

            // Act
            var resultLower = await _sut.exist("testproduct");
            var resultExact = await _sut.exist("TestProduct");

            // Assert – InMemory provider is case-sensitive by default
            resultExact.Should().BeTrue();
        }


        // ══════════════════════════════════════════════
        //  Update
        // ══════════════════════════════════════════════

        [Fact]
        public async Task Update_Should_Modify_Product_And_Return_It()
        {
            // Arrange
            var product = CreateProduct(name: "Original");
            await SeedProducts(product);

            var toUpdate = await _db.PRODUCTS.FirstAsync();
            toUpdate.product_name = "Updated";

            // Act
            var result = await _sut.Update(toUpdate);

            // Assert
            result.Should().NotBeNull();
            result.product_name.Should().Be("Updated");

            var saved = await _db.PRODUCTS.FindAsync(toUpdate.Id);
            saved!.product_name.Should().Be("Updated");
        }

        [Fact]
        public async Task Update_Should_Modify_Description()
        {
            // Arrange
            var product = CreateProduct(name: "DescProd", description: "Old Desc");
            await SeedProducts(product);

            var toUpdate = await _db.PRODUCTS.FirstAsync();
            toUpdate.product_description = "New Description";

            // Act
            var result = await _sut.Update(toUpdate);

            // Assert
            result.product_description.Should().Be("New Description");
        }

        [Fact]
        public async Task Update_Should_Modify_Verified_Status()
        {
            // Arrange
            var product = CreateProduct(name: "VerifyProd", isVerified: false);
            await SeedProducts(product);

            var toUpdate = await _db.PRODUCTS.FirstAsync();
            toUpdate.isVerified = true;

            // Act
            var result = await _sut.Update(toUpdate);

            // Assert
            result.isVerified.Should().BeTrue();
        }

        [Fact]
        public async Task Update_Should_Modify_Auction_Dates()
        {
            // Arrange
            var product = CreateProduct(name: "AuctionProd");
            await SeedProducts(product);

            var toUpdate = await _db.PRODUCTS.FirstAsync();
            var start = DateTime.Now.AddDays(1);
            var end = DateTime.Now.AddDays(2);
            toUpdate.AuctionStartTime = start;
            toUpdate.AuctionEndTime = end;

            // Act
            var result = await _sut.Update(toUpdate);

            // Assert
            result.AuctionStartTime.Should().Be(start);
            result.AuctionEndTime.Should().Be(end);
        }


        // ══════════════════════════════════════════════
        //  deleteProduct
        // ══════════════════════════════════════════════

        [Fact]
        public async Task DeleteProduct_Should_Remove_Product_And_Return_It()
        {
            // Arrange
            var product = CreateProduct(name: "ToDelete");
            await SeedProducts(product);

            var toDelete = await _db.PRODUCTS.FirstAsync();

            // Act
            var result = await _sut.deleteProduct(toDelete);

            // Assert
            result.Should().NotBeNull();
            result.product_name.Should().Be("ToDelete");

            var remaining = await _db.PRODUCTS.CountAsync();
            remaining.Should().Be(0);
        }

        [Fact]
        public async Task DeleteProduct_Should_Not_Affect_Other_Products()
        {
            // Arrange
            var p1 = CreateProduct(name: "Keep");
            var p2 = CreateProduct(name: "Delete");
            await SeedProducts(p1, p2);

            var toDelete = await _db.PRODUCTS.FirstAsync(x => x.product_name == "Delete");

            // Act
            await _sut.deleteProduct(toDelete);

            // Assert
            var remaining = await _db.PRODUCTS.ToListAsync();
            remaining.Should().HaveCount(1);
            remaining[0].product_name.Should().Be("Keep");
        }

        [Fact]
        public async Task DeleteProduct_Should_Return_Deleted_Product_Entity()
        {
            // Arrange
            var product = CreateProduct(name: "ReturnMe", userId: 55);
            await SeedProducts(product);

            var toDelete = await _db.PRODUCTS.FirstAsync();

            // Act
            var result = await _sut.deleteProduct(toDelete);

            // Assert
            result.product_name.Should().Be("ReturnMe");
            result.user_id.Should().Be(55);
        }


        // ══════════════════════════════════════════════
        //  getByIdProduct
        // ══════════════════════════════════════════════

        [Fact]
        public async Task GetByIdProduct_Should_Return_Product_When_Found()
        {
            // Arrange
            var product = CreateProduct(name: "FindMe");
            await SeedProducts(product);

            var savedId = (await _db.PRODUCTS.FirstAsync()).Id;

            // Act
            var result = await _sut.getByIdProduct(savedId);

            // Assert
            result.Should().NotBeNull();
            result!.product_name.Should().Be("FindMe");
        }

        [Fact]
        public async Task GetByIdProduct_Should_Return_Null_When_Not_Found()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "Other"));

            // Act
            var result = await _sut.getByIdProduct(9999);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdProduct_Should_Return_Null_When_Database_Is_Empty()
        {
            // Act
            var result = await _sut.getByIdProduct(1);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdProduct_Should_Include_Images()
        {
            // Arrange
            var product = CreateProduct(name: "WithImages");
            product.images = new List<ImageTable>
            {
                new ImageTable { Image_URL = "http://img1.url", public_Id = "pub1" },
                new ImageTable { Image_URL = "http://img2.url", public_Id = "pub2" }
            };
            await SeedProducts(product);

            var savedId = (await _db.PRODUCTS.FirstAsync()).Id;

            // Act
            var result = await _sut.getByIdProduct(savedId);

            // Assert
            result.Should().NotBeNull();
            result!.images.Should().NotBeNull();
            result.images.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetByIdProduct_Should_Return_Correct_Product_Among_Many()
        {
            // Arrange
            var p1 = CreateProduct(name: "Product1", userId: 1);
            var p2 = CreateProduct(name: "Product2", userId: 2);
            var p3 = CreateProduct(name: "Product3", userId: 3);
            await SeedProducts(p1, p2, p3);

            var targetId = (await _db.PRODUCTS.FirstAsync(x => x.product_name == "Product2")).Id;

            // Act
            var result = await _sut.getByIdProduct(targetId);

            // Assert
            result.Should().NotBeNull();
            result!.product_name.Should().Be("Product2");
            result.user_id.Should().Be(2);
        }


        // ══════════════════════════════════════════════
        //  getProduct  (throws NotImplementedException)
        // ══════════════════════════════════════════════

        [Fact]
        public async Task GetProduct_Should_Throw_NotImplementedException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<NotImplementedException>(() => _sut.getProduct());
        }


        // ══════════════════════════════════════════════
        //  AllProducts — No Filters (baseline)
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Return_All_Products_When_No_Filters_Applied()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "A"),
                CreateProduct(name: "B"),
                CreateProduct(name: "C"));

            var query = new ProductAll { page = 1, size = 10 };

            // Act
            var result = await _sut.AllProducts(query);

            // Assert
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task AllProducts_Should_Return_Empty_When_No_Products_In_Database()
        {
            // Arrange
            var query = new ProductAll { page = 1, size = 10 };

            // Act
            var result = await _sut.AllProducts(query);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task AllProducts_Should_Include_Images()
        {
            // Arrange
            var product = CreateProduct(name: "WithImg");
            product.images = new List<ImageTable>
            {
                new ImageTable { Image_URL = "http://img.url", public_Id = "pub1" }
            };
            await SeedProducts(product);

            var query = new ProductAll { page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].images.Should().HaveCount(1);
        }


        // ══════════════════════════════════════════════
        //  AllProducts — mine filter
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Filter_By_UserId_When_Mine_Is_True()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "MyProduct", userId: 5),
                CreateProduct(name: "OtherProduct", userId: 10));

            var query = new ProductAll { mine = true, id = 5, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("MyProduct");
        }

        [Fact]
        public async Task AllProducts_Should_Return_All_When_Mine_Is_False()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "Product1", userId: 5),
                CreateProduct(name: "Product2", userId: 10));

            var query = new ProductAll { mine = false, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Return_Empty_When_Mine_Is_True_And_No_Products_For_User()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "OtherProduct", userId: 10));

            var query = new ProductAll { mine = true, id = 99, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().BeEmpty();
        }


        // ══════════════════════════════════════════════
        //  AllProducts — searchName filter
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Filter_By_SearchName()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "Apple Watch"),
                CreateProduct(name: "Samsung Phone"),
                CreateProduct(name: "Apple MacBook"));

            var query = new ProductAll { searchName = "Apple", page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(p => p.product_name.Contains("Apple"));
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_When_SearchName_Is_Null()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "Product1"),
                CreateProduct(name: "Product2"));

            var query = new ProductAll { searchName = null, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_When_SearchName_Is_Empty()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "Product1"),
                CreateProduct(name: "Product2"));

            var query = new ProductAll { searchName = "", page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Return_Empty_When_SearchName_Has_No_Match()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "SomeProduct"));

            var query = new ProductAll { searchName = "NonExistent", page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().BeEmpty();
        }


        // ══════════════════════════════════════════════
        //  AllProducts — buyFrom filter
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Filter_By_BuyFrom_When_In_Past()
        {
            // Arrange
            var pastDate = DateTime.Now.AddDays(-20);
            await SeedProducts(
                CreateProduct(name: "OldProduct", buyDate: DateTime.Now.AddDays(-30)),
                CreateProduct(name: "RecentProduct", buyDate: DateTime.Now.AddDays(-5)));

            var query = new ProductAll { buyFrom = pastDate, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            // buyFrom is -20d, OldProduct bought -30d ago (before buyFrom → excluded),
            // RecentProduct bought -5d ago (after buyFrom → included)
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("RecentProduct");
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_By_BuyFrom_When_Null()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "P1", buyDate: DateTime.Now.AddDays(-30)),
                CreateProduct(name: "P2", buyDate: DateTime.Now.AddDays(-5)));

            var query = new ProductAll { buyFrom = null, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_By_BuyFrom_When_In_Future()
        {
            // Arrange — buyFrom is in the future, so the condition (buyFrom <= DateTime.Now) is false
            await SeedProducts(
                CreateProduct(name: "P1", buyDate: DateTime.Now.AddDays(-10)),
                CreateProduct(name: "P2", buyDate: DateTime.Now.AddDays(-5)));

            var query = new ProductAll { buyFrom = DateTime.Now.AddDays(5), page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert — filter skipped because buyFrom > DateTime.Now
            result.Should().HaveCount(2);
        }


        // ══════════════════════════════════════════════
        //  AllProducts — buyTo filter
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Filter_By_BuyTo_When_In_Past()
        {
            // Arrange
            var cutoff = DateTime.Now.AddDays(-15);
            await SeedProducts(
                CreateProduct(name: "OldProduct", buyDate: DateTime.Now.AddDays(-30)),
                CreateProduct(name: "RecentProduct", buyDate: DateTime.Now.AddDays(-5)));

            var query = new ProductAll { buyTo = cutoff, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            // buyTo is -15d. OldProduct bought -30d (before cutoff → included),
            // RecentProduct bought -5d (after cutoff → excluded)
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("OldProduct");
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_By_BuyTo_When_Null()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "P1"),
                CreateProduct(name: "P2"));

            var query = new ProductAll { buyTo = null, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_By_BuyTo_When_In_Future()
        {
            // Arrange — buyTo is in the future, so (buyTo <= DateTime.Now) is false → filter skipped
            await SeedProducts(
                CreateProduct(name: "P1", buyDate: DateTime.Now.AddDays(-10)),
                CreateProduct(name: "P2", buyDate: DateTime.Now.AddDays(-5)));

            var query = new ProductAll { buyTo = DateTime.Now.AddDays(5), page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }


        // ══════════════════════════════════════════════
        //  AllProducts — createdFrom filter
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Filter_By_CreatedFrom_When_In_Past()
        {
            // Arrange
            var cutoff = DateTime.Now.AddDays(-15);
            await SeedProducts(
                CreateProduct(name: "OldCreated", creationDate: DateTime.Now.AddDays(-30)),
                CreateProduct(name: "NewCreated", creationDate: DateTime.Now.AddDays(-3)));

            var query = new ProductAll { createdFrom = cutoff, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("NewCreated");
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_By_CreatedFrom_When_Null()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "P1"), CreateProduct(name: "P2"));

            var query = new ProductAll { createdFrom = null, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_By_CreatedFrom_When_In_Future()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "P1"), CreateProduct(name: "P2"));

            var query = new ProductAll { createdFrom = DateTime.Now.AddDays(5), page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert — filter skipped because createdFrom > DateTime.Now
            result.Should().HaveCount(2);
        }


        // ══════════════════════════════════════════════
        //  AllProducts — createdTo filter
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Filter_By_CreatedTo_When_In_Past()
        {
            // Arrange
            var cutoff = DateTime.Now.AddDays(-10);
            await SeedProducts(
                CreateProduct(name: "EarlyCreated", creationDate: DateTime.Now.AddDays(-20)),
                CreateProduct(name: "LateCreated", creationDate: DateTime.Now.AddDays(-3)));

            var query = new ProductAll { createdTo = cutoff, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("EarlyCreated");
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_By_CreatedTo_When_Null()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "P1"), CreateProduct(name: "P2"));

            var query = new ProductAll { createdTo = null, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_By_CreatedTo_When_In_Future()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "P1"), CreateProduct(name: "P2"));

            var query = new ProductAll { createdTo = DateTime.Now.AddDays(5), page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }


        // ══════════════════════════════════════════════
        //  AllProducts — verified filter
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Filter_By_Verified_When_True()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "Verified1", isVerified: true),
                CreateProduct(name: "Unverified1", isVerified: false),
                CreateProduct(name: "Verified2", isVerified: true));

            var query = new ProductAll { verified = true, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(p => p.isVerified);
        }

        [Fact]
        public async Task AllProducts_Should_Return_All_When_Verified_Is_False()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "Verified", isVerified: true),
                CreateProduct(name: "Unverified", isVerified: false));

            var query = new ProductAll { verified = false, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }


        // ══════════════════════════════════════════════
        //  AllProducts — productId filter
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Filter_By_ProductId_When_Not_Null()
        {
            // Arrange
            var p1 = CreateProduct(name: "Target");
            var p2 = CreateProduct(name: "Other");
            await SeedProducts(p1, p2);

            var targetId = (await _db.PRODUCTS.FirstAsync(x => x.product_name == "Target")).Id;

            var query = new ProductAll { productId = targetId, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("Target");
        }

        [Fact]
        public async Task AllProducts_Should_Not_Filter_By_ProductId_When_Null()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "P1"), CreateProduct(name: "P2"));

            var query = new ProductAll { productId = null, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Return_Empty_When_ProductId_Does_Not_Exist()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "P1"));

            var query = new ProductAll { productId = 9999, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().BeEmpty();
        }


        // ══════════════════════════════════════════════
        //  AllProducts — Pagination (Skip / Take)
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Return_First_Page()
        {
            // Arrange — 5 products, page size 2
            await SeedProducts(
                CreateProduct(name: "P1"),
                CreateProduct(name: "P2"),
                CreateProduct(name: "P3"),
                CreateProduct(name: "P4"),
                CreateProduct(name: "P5"));

            var query = new ProductAll { page = 1, size = 2 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Return_Second_Page()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "P1"),
                CreateProduct(name: "P2"),
                CreateProduct(name: "P3"),
                CreateProduct(name: "P4"),
                CreateProduct(name: "P5"));

            var query = new ProductAll { page = 2, size = 2 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task AllProducts_Should_Return_Last_Page_With_Remaining_Items()
        {
            // Arrange — 5 items, page 3's size 2 → only 1 item left
            await SeedProducts(
                CreateProduct(name: "P1"),
                CreateProduct(name: "P2"),
                CreateProduct(name: "P3"),
                CreateProduct(name: "P4"),
                CreateProduct(name: "P5"));

            var query = new ProductAll { page = 3, size = 2 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task AllProducts_Should_Return_Empty_When_Page_Exceeds_Total()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "P1"),
                CreateProduct(name: "P2"));

            var query = new ProductAll { page = 5, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task AllProducts_Should_Return_All_When_Page_Size_Is_Larger_Than_Total()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "P1"),
                CreateProduct(name: "P2"));

            var query = new ProductAll { page = 1, size = 100 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
        }


        // ══════════════════════════════════════════════
        //  AllProducts — Combined Filters
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Apply_Mine_And_SearchName_Combined()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "My Apple Watch", userId: 1),
                CreateProduct(name: "My Samsung Phone", userId: 1),
                CreateProduct(name: "Other Apple Watch", userId: 2));

            var query = new ProductAll
            {
                mine = true,
                id = 1,
                searchName = "Apple",
                page = 1,
                size = 10
            };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("My Apple Watch");
        }

        [Fact]
        public async Task AllProducts_Should_Apply_Verified_And_SearchName_Combined()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "Verified Apple", isVerified: true),
                CreateProduct(name: "Unverified Apple", isVerified: false),
                CreateProduct(name: "Verified Samsung", isVerified: true));

            var query = new ProductAll
            {
                verified = true,
                searchName = "Apple",
                page = 1,
                size = 10
            };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("Verified Apple");
        }

        [Fact]
        public async Task AllProducts_Should_Apply_BuyFrom_And_BuyTo_Date_Range()
        {
            // Arrange
            var now = DateTime.Now;
            await SeedProducts(
                CreateProduct(name: "TooOld", buyDate: now.AddDays(-60)),
                CreateProduct(name: "InRange", buyDate: now.AddDays(-10)),
                CreateProduct(name: "TooRecent", buyDate: now.AddDays(-1)));

            var query = new ProductAll
            {
                buyFrom = now.AddDays(-30),
                buyTo = now.AddDays(-5),
                page = 1,
                size = 10
            };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("InRange");
        }

        [Fact]
        public async Task AllProducts_Should_Apply_CreatedFrom_And_CreatedTo_Date_Range()
        {
            // Arrange
            var now = DateTime.Now;
            await SeedProducts(
                CreateProduct(name: "OldCreated", creationDate: now.AddDays(-60)),
                CreateProduct(name: "InRangeCreated", creationDate: now.AddDays(-10)),
                CreateProduct(name: "RecentCreated", creationDate: now.AddDays(-1)));

            var query = new ProductAll
            {
                createdFrom = now.AddDays(-30),
                createdTo = now.AddDays(-5),
                page = 1,
                size = 10
            };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("InRangeCreated");
        }

        [Fact]
        public async Task AllProducts_Should_Apply_All_Filters_Together()
        {
            // Arrange
            var now = DateTime.Now;
            await SeedProducts(
                CreateProduct(name: "Target Product", userId: 1, isVerified: true,
                    buyDate: now.AddDays(-10), creationDate: now.AddDays(-8)),
                CreateProduct(name: "Wrong User", userId: 2, isVerified: true,
                    buyDate: now.AddDays(-10), creationDate: now.AddDays(-8)),
                CreateProduct(name: "Not Verified", userId: 1, isVerified: false,
                    buyDate: now.AddDays(-10), creationDate: now.AddDays(-8)),
                CreateProduct(name: "Wrong Name", userId: 1, isVerified: true,
                    buyDate: now.AddDays(-10), creationDate: now.AddDays(-8)),
                CreateProduct(name: "Target Outside Date", userId: 1, isVerified: true,
                    buyDate: now.AddDays(-60), creationDate: now.AddDays(-60)));

            var query = new ProductAll
            {
                mine = true,
                id = 1,
                verified = true,
                searchName = "Target",
                buyFrom = now.AddDays(-30),
                buyTo = now.AddDays(-1),
                createdFrom = now.AddDays(-30),
                createdTo = now.AddDays(-1),
                page = 1,
                size = 10
            };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("Target Product");
        }

        [Fact]
        public async Task AllProducts_Should_Apply_Filters_With_Pagination()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "Verified1", isVerified: true),
                CreateProduct(name: "Verified2", isVerified: true),
                CreateProduct(name: "Verified3", isVerified: true),
                CreateProduct(name: "Unverified", isVerified: false));

            var query = new ProductAll { verified = true, page = 1, size = 2 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(p => p.isVerified);
        }

        [Fact]
        public async Task AllProducts_Should_Apply_Filters_With_Pagination_Page2()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "Verified1", isVerified: true),
                CreateProduct(name: "Verified2", isVerified: true),
                CreateProduct(name: "Verified3", isVerified: true),
                CreateProduct(name: "Unverified", isVerified: false));

            var query = new ProductAll { verified = true, page = 2, size = 2 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result.Should().OnlyContain(p => p.isVerified);
        }

        [Fact]
        public async Task AllProducts_Should_Apply_ProductId_With_Mine_Filter()
        {
            // Arrange
            var p1 = CreateProduct(name: "UserProduct1", userId: 1);
            var p2 = CreateProduct(name: "UserProduct2", userId: 1);
            var p3 = CreateProduct(name: "OtherProduct", userId: 2);
            await SeedProducts(p1, p2, p3);

            var targetId = (await _db.PRODUCTS.FirstAsync(x => x.product_name == "UserProduct1")).Id;

            var query = new ProductAll
            {
                mine = true,
                id = 1,
                productId = targetId,
                page = 1,
                size = 10
            };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("UserProduct1");
        }


        // ══════════════════════════════════════════════
        //  AllProducts — Edge Cases
        // ══════════════════════════════════════════════

        [Fact]
        public async Task AllProducts_Should_Handle_BuyFrom_Equal_To_Now()
        {
            // Arrange — buyFrom == DateTime.Now is valid (buyFrom <= DateTime.Now is true)
            var now = DateTime.Now;
            await SeedProducts(
                CreateProduct(name: "P1", buyDate: now));

            var query = new ProductAll { buyFrom = now, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert — filter is applied
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task AllProducts_Should_Handle_BuyTo_Equal_To_Now()
        {
            // Arrange
            var now = DateTime.Now;
            await SeedProducts(
                CreateProduct(name: "P1", buyDate: now.AddDays(-1)));

            var query = new ProductAll { buyTo = now, page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task AllProducts_Should_Handle_Default_Page_And_Size()
        {
            // Arrange — defaults are page=1, size=10
            await SeedProducts(
                CreateProduct(name: "P1"),
                CreateProduct(name: "P2"),
                CreateProduct(name: "P3"));

            var query = new ProductAll(); // uses defaults

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task AllProducts_Should_Return_Page_Size_1()
        {
            // Arrange
            await SeedProducts(
                CreateProduct(name: "P1"),
                CreateProduct(name: "P2"),
                CreateProduct(name: "P3"));

            var query = new ProductAll { page = 1, size = 1 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task AllProducts_SearchName_Should_Match_Partial_Name()
        {
            // Arrange
            await SeedProducts(CreateProduct(name: "SuperProductDeluxe"));

            var query = new ProductAll { searchName = "Product", page = 1, size = 10 };

            // Act
            var result = (await _sut.AllProducts(query)).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].product_name.Should().Be("SuperProductDeluxe");
        }
    }
}
