using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Moq;
using VERIFY.Messaging.Consumers;
using VERIFY.Model;

namespace VERIFY.TESTS.Consumers
{
    public class ProductUpdateConsumerTests
    {
        private readonly VerifyDbContext ctx;

        public ProductUpdateConsumerTests()
        {
            var options = new DbContextOptionsBuilder<VerifyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            ctx = new VerifyDbContext(options);
        }

        private VerifyProductTable seedData(
            int productId = 1,
            string name = "OldName",
            string description = "OldDescription")
        {
            return new VerifyProductTable
            {
                Id = 1,
                ProductId = productId,
                ProductName = name,
                Product_description = description,
                isProductVerified = true,
                SellerId = 1
            };
        }

        [Fact]
        public async Task Consume_Should_Update_Name_And_Description()
        {
            // Arrange
            var consumer = new ProductUpdateConsumer(ctx);

            var data = seedData();
            ctx.VERIFY_PRODUCTS.Add(data);
            await ctx.SaveChangesAsync();

            var context = Mock.Of<ConsumeContext<ProductUpdateForVerification>>(x =>
                x.Message == new ProductUpdateForVerification(
                    1,
                    "NewName",
                    "NewDescription"));

            // Act
            await consumer.Consume(context);

            // Assert
            var updated = await ctx.VERIFY_PRODUCTS.FirstAsync();

            updated.ProductName.Should().Be("NewName");
            updated.Product_description.Should().Be("NewDescription");
        }

        [Fact]
        public async Task Consume_Should_Update_Only_Name_When_Description_Null()
        {
            var consumer = new ProductUpdateConsumer(ctx);

            var data = seedData();
            ctx.VERIFY_PRODUCTS.Add(data);
            await ctx.SaveChangesAsync();

            var context = Mock.Of<ConsumeContext<ProductUpdateForVerification>>(x =>
                x.Message == new ProductUpdateForVerification(
                    1,
                    "NewName",
                    null));

            await consumer.Consume(context);

            var updated = await ctx.VERIFY_PRODUCTS.FirstAsync();

            updated.ProductName.Should().Be("NewName");
            updated.Product_description.Should().Be("OldDescription");
        }

        [Fact]
        public async Task Consume_Should_Update_Only_Description_When_Name_Null()
        {
            var consumer = new ProductUpdateConsumer(ctx);

            var data = seedData();
            ctx.VERIFY_PRODUCTS.Add(data);
            await ctx.SaveChangesAsync();

            var context = Mock.Of<ConsumeContext<ProductUpdateForVerification>>(x =>
                x.Message == new ProductUpdateForVerification(
                    1,
                    null,
                    "NewDescription"));

            await consumer.Consume(context);

            var updated = await ctx.VERIFY_PRODUCTS.FirstAsync();

            updated.ProductName.Should().Be("OldName");
            updated.Product_description.Should().Be("NewDescription");
        }

        [Fact]
        public async Task Consume_Should_Do_Nothing_When_Record_Not_Found()
        {
            var consumer = new ProductUpdateConsumer(ctx);

            var context = Mock.Of<ConsumeContext<ProductUpdateForVerification>>(x =>
                x.Message == new ProductUpdateForVerification(
                    99,
                    "NewName",
                    "NewDescription"));

            await consumer.Consume(context);

            ctx.VERIFY_PRODUCTS.Count().Should().Be(0);
        }

        [Fact]
        public async Task Consume_Should_Not_Update_When_Both_Fields_Null()
        {
            var consumer = new ProductUpdateConsumer(ctx);

            var data = seedData();
            ctx.VERIFY_PRODUCTS.Add(data);
            await ctx.SaveChangesAsync();

            var context = Mock.Of<ConsumeContext<ProductUpdateForVerification>>(x =>
                x.Message == new ProductUpdateForVerification(
                    1,
                    null,
                    null));

            await consumer.Consume(context);

            var updated = await ctx.VERIFY_PRODUCTS.FirstAsync();

            updated.ProductName.Should().Be("OldName");
            updated.Product_description.Should().Be("OldDescription");
        }
    }
}