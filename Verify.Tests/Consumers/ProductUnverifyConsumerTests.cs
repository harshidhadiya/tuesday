using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VERIFY.Messaging.Consumers;
using VERIFY.Model;

namespace VERIFY.TESTS.Consumers
{
    public class ProductUnverifyConsumerTests
    {
        private readonly DbContextOptions<VerifyDbContext> options;
        private readonly Mock<IServiceScope> scope;
        private readonly Mock<IServiceScopeFactory> factory;
        private readonly Mock<IServiceProvider> provider;
        private readonly Mock<ILogger<ProductUnverifyConsumer>> logger;
        private readonly Mock<IPublishEndpoint> publishEndpoint;
        private VerifyDbContext ctx;

        public ProductUnverifyConsumerTests()
        {
            options = new DbContextOptionsBuilder<VerifyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            scope = new();
            factory = new();
            provider = new();
            logger = new();
            publishEndpoint = new();

            factory.Setup(x => x.CreateScope()).Returns(scope.Object);
            scope.Setup(x => x.ServiceProvider).Returns(provider.Object);

            ctx = new VerifyDbContext(options);

            provider.Setup(x => x.GetService(typeof(VerifyDbContext)))
                    .Returns(ctx);
        }

        private VerifyProductTable seedData(
            int ProductId = 1,
            int? VerifierId = 1,
            bool isVerified = true)
        {
            return new VerifyProductTable
            {
                Id = 1,
                ProductId = ProductId,
                VerifierId = VerifierId,
                isProductVerified = isVerified,
                Description = "Old",
                ProductName = "Test",
                Product_description = "Desc",
                SellerId = 1
            };
        }

        // ✅ SUCCESS CASE
        [Fact]
        public async Task Consume_Should_Unverify_Product_And_Publish_Event()
        {
            // Arrange
            var consumer = new ProductUnverifyConsumer(factory.Object, logger.Object, publishEndpoint.Object);

            var data = seedData(ProductId: 1, VerifierId: 10);
            ctx.VERIFY_PRODUCTS.Add(data);
            await ctx.SaveChangesAsync();

            var context = Mock.Of<ConsumeContext<ProductUnverifyRequested>>(x =>
                x.Message == new ProductUnverifyRequested(1, 10, "reason"));

            // Act
            await consumer.Consume(context);

            // Assert
            var updated = await ctx.VERIFY_PRODUCTS.FirstAsync();

            updated.isProductVerified.Should().BeFalse();
            updated.Description.Should().Be("reason");

            publishEndpoint.Verify(x => x.Publish(
                It.Is<ProductUnverified>(m =>
                    m.ProductId == 1 &&
                    m.AdminId == 10),
                It.IsAny<CancellationToken>()),
                Times.Once);

            logger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unverified by admin")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ✅ INVALID PRODUCT ID
        [Fact]
        public async Task Consume_Should_Return_When_ProductId_Invalid()
        {
            var consumer = new ProductUnverifyConsumer(factory.Object, logger.Object, publishEndpoint.Object);

            var context = Mock.Of<ConsumeContext<ProductUnverifyRequested>>(x =>
                x.Message == new ProductUnverifyRequested(0, 1, "test"));

            await consumer.Consume(context);

            factory.Verify(x => x.CreateScope(), Times.Never);

            logger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid ProductUnverifyRequested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ✅ RECORD NOT FOUND
        [Fact]
        public async Task Consume_Should_Return_When_Record_Not_Found()
        {
            var consumer = new ProductUnverifyConsumer(factory.Object, logger.Object, publishEndpoint.Object);

            var context = Mock.Of<ConsumeContext<ProductUnverifyRequested>>(x =>
                x.Message == new ProductUnverifyRequested(1, 1, "test"));

            await consumer.Consume(context);

            publishEndpoint.Verify(x => x.Publish(It.IsAny<ProductUnverified>(), It.IsAny<CancellationToken>()),
                Times.Never);

            logger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        // ✅ ADMIN ID NOT MATCH
        [Fact]
        public async Task Consume_Should_Return_When_Admin_Not_Matching()
        {
            var consumer = new ProductUnverifyConsumer(factory.Object, logger.Object, publishEndpoint.Object);

            var data = seedData(ProductId: 1, VerifierId: 99);
            ctx.VERIFY_PRODUCTS.Add(data);
            await ctx.SaveChangesAsync();

            var context = Mock.Of<ConsumeContext<ProductUnverifyRequested>>(x =>
                x.Message == new ProductUnverifyRequested(1, 10, "test"));

            await consumer.Consume(context);

            var record = await ctx.VERIFY_PRODUCTS.FirstAsync();

            record.isProductVerified.Should().BeTrue(); // unchanged

            publishEndpoint.Verify(x => x.Publish(It.IsAny<ProductUnverified>(), It.IsAny<CancellationToken>()),
                Times.Never);

            logger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
    }
}