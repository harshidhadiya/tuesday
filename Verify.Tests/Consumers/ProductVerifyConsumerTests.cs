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
    public class ProductVerifyConsumerTests
    {
        private readonly DbContextOptions<VerifyDbContext> options;
        private readonly Mock<IServiceScope> scope;
        private readonly Mock<IServiceScopeFactory> factory;
        private readonly Mock<IServiceProvider> provider;
        private readonly Mock<ILogger<ProductVerifyConsumer>> logger;
        private readonly Mock<IPublishEndpoint> publishEndpoint;
        private VerifyDbContext ctx;

        public ProductVerifyConsumerTests()
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
            int productId = 1,
            bool isVerified = false)
        {
            return new VerifyProductTable
            {
                Id = 1,
                ProductId = productId,
                ProductName = "Test",
                Product_description = "Old",
                isProductVerified = isVerified,
                SellerId = 1
            };
        }

        [Fact]
        public async Task Consume_Should_Verify_Product_And_Publish_Event()
        {
            // Arrange
            var consumer = new ProductVerifyConsumer(factory.Object, logger.Object, publishEndpoint.Object);

            var data = seedData(productId: 1);
            ctx.VERIFY_PRODUCTS.Add(data);
            await ctx.SaveChangesAsync();

            var context = Mock.Of<ConsumeContext<ProductVerifyRequested>>(x =>
                x.Message == new ProductVerifyRequested(1, 99, "approved"));

            // Act
            await consumer.Consume(context);

            // Assert
            var updated = await ctx.VERIFY_PRODUCTS.FirstAsync();

            updated.isProductVerified.Should().BeTrue();
            updated.Description.Should().Be("approved");
            updated.VerifierId.Should().Be(99);
            updated.VerifiedTime.Should().NotBeNull();

            publishEndpoint.Verify(x => x.Publish(
                It.Is<ProductVerified>(m => m.ProductId == 1),
                It.IsAny<CancellationToken>()),
                Times.Once);

            logger.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("verified successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Return_When_ProductId_Invalid()
        {
            var consumer = new ProductVerifyConsumer(factory.Object, logger.Object, publishEndpoint.Object);

            var context = Mock.Of<ConsumeContext<ProductVerifyRequested>>(x =>
                x.Message == new ProductVerifyRequested(0, 1, "test"));

            await consumer.Consume(context);

            factory.Verify(x => x.CreateScope(), Times.Never);

            logger.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid ProductVerifyRequested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consume_Should_Return_When_Record_Not_Found()
        {
            var consumer = new ProductVerifyConsumer(factory.Object, logger.Object, publishEndpoint.Object);

            var context = Mock.Of<ConsumeContext<ProductVerifyRequested>>(x =>
                x.Message == new ProductVerifyRequested(1, 1, "test"));

            await consumer.Consume(context);

            publishEndpoint.Verify(x => x.Publish(It.IsAny<ProductVerified>(), It.IsAny<CancellationToken>()),
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