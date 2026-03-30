using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VERIFY.Messaging.Consumers;
using VERIFY.Model;
using Xunit;

namespace Verify.Tests.Consumers
{
    public class CreateVerifyObjConsumerTests
    {
        private readonly Mock<IServiceScopeFactory> _scopeFactory;
        private readonly Mock<IServiceScope> _scope;
        private readonly Mock<IServiceProvider> _serviceProvider;
        private readonly Mock<ILogger<createVerifyObjConsumer>> _logger;
        private readonly DbContextOptions<VerifyDbContext> _dbOptions;

        public CreateVerifyObjConsumerTests()
        {
            _scopeFactory = new Mock<IServiceScopeFactory>();
            _scope = new Mock<IServiceScope>();
            _serviceProvider = new Mock<IServiceProvider>();
            _logger = new Mock<ILogger<createVerifyObjConsumer>>();

            _dbOptions = new DbContextOptionsBuilder<VerifyDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _scopeFactory.Setup(s => s.CreateScope()).Returns(_scope.Object);
            _scope.Setup(s => s.ServiceProvider).Returns(_serviceProvider.Object);
        }

        [Fact]
        public async Task Consume_Should_LogWarning_When_ProductId_Invalid()
        {
            var consumer = new createVerifyObjConsumer(_scopeFactory.Object, _logger.Object);
            var context = new Mock<ConsumeContext<ProductCreatedForVerification>>();
            context.Setup(c => c.Message).Returns(new ProductCreatedForVerification(0, 1, "test", "desc"));

            await consumer.Consume(context.Object);

            _scopeFactory.Verify(s => s.CreateScope(), Times.Never);
        }

        [Fact]
        public async Task Consume_Should_Add_VerifyProductTable_When_Not_Exists()
        {
            using var dbContext = new VerifyDbContext(_dbOptions);
            _serviceProvider.Setup(s => s.GetService(typeof(VerifyDbContext))).Returns(dbContext);

            var consumer = new createVerifyObjConsumer(_scopeFactory.Object, _logger.Object);
            var context = new Mock<ConsumeContext<ProductCreatedForVerification>>();
            context.Setup(c => c.Message).Returns(new ProductCreatedForVerification(1, 1, "test", "desc"));

            await consumer.Consume(context.Object);

            dbContext.VERIFY_PRODUCTS.Should().ContainSingle();
            var added = dbContext.VERIFY_PRODUCTS.First();
            added.ProductId.Should().Be(1);
            added.isProductVerified.Should().BeFalse();
        }

        [Fact]
        public async Task Consume_Should_Return_If_Exists()
        {
            using var dbContext = new VerifyDbContext(_dbOptions);
            dbContext.VERIFY_PRODUCTS.Add(new VerifyProductTable { ProductId = 1, SellerId = 2, Product_description = "desc" });
            dbContext.SaveChanges();

            _serviceProvider.Setup(s => s.GetService(typeof(VerifyDbContext))).Returns(dbContext);

            var consumer = new createVerifyObjConsumer(_scopeFactory.Object, _logger.Object);
            var context = new Mock<ConsumeContext<ProductCreatedForVerification>>();
            context.Setup(c => c.Message).Returns(new ProductCreatedForVerification(1, 1, "test", "desc"));

            await consumer.Consume(context.Object);

            dbContext.VERIFY_PRODUCTS.Should().ContainSingle();
        }
    }
}
