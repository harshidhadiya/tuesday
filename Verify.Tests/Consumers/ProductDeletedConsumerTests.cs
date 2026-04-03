using System.Net.NetworkInformation;
using Castle.Core.Logging;
using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Moq;
using VERIFY.Messaging.Consumers;
using VERIFY.Model;

namespace VERIFY.TESTS.Consumers
{
    public class ProductDeletedConsumerTests
    {

        private readonly DbContextOptions<VerifyDbContext> options;
        private readonly Mock<IServiceScope> scope;
        private readonly Mock<IServiceScopeFactory> factory;
        private readonly Mock<IServiceProvider> provider;
        private readonly Mock<ILogger<ProductDeletedConsumer>> logger;
        private  VerifyDbContext ctx;

        public ProductDeletedConsumerTests()
        {
            options = new DbContextOptionsBuilder<VerifyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            scope = new();
            factory = new();
            provider = new();
            factory.Setup(x => x.CreateScope()).Returns(scope.Object);
            scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
            this.ctx = new VerifyDbContext(options);
            provider.Setup(x => x.GetService(typeof(VerifyDbContext))).Returns(ctx);
            logger = new();

        }
        private VerifyProductTable seedData(string Description = "verified product", int Id = 1, bool isProductVerified = false, int ProductId = 1, string ProductName = "product", string Product_description = "very good condition"
        , int SellerId = 1, DateTime? VerifiedTime = null, int? VerifierId = null)
        {
            return new VerifyProductTable
            {
                Description = Description,
                Id = Id,
                isProductVerified = isProductVerified
            ,
                ProductId = ProductId,
                ProductName = ProductName,
                Product_description = Product_description,
                SellerId = SellerId
            ,
                VerifiedTime = VerifiedTime,
                VerifierId = VerifierId
            };
        }

        //    private ProductDeletedConsumer GetConsumer(int ProductId=0,int DeltedBYid=0)
        //     {
        //       var consumer=new ProductDeletedConsumer(factory.Object,logger.Object);
        //       var consumeContext=Mock.Of<ConsumeContext<ProductDeleted>>(X=>X.Message==new ProductDeleted(0,0));
        //         return 
        //     }


        [Fact]
        public async Task Consume_remove_deleterProduct_related_item()
        {
            int userid = 1;
            // Given
            var consumer = new ProductDeletedConsumer(factory.Object, logger.Object);
            var data = seedData(SellerId: userid);
            var consumeContext = Mock.Of<ConsumeContext<ProductDeleted>>(X => X.Message == new ProductDeleted(data.ProductId, data.SellerId));
            ctx.VERIFY_PRODUCTS.Add(data);
            // When
            await ctx.SaveChangesAsync();
            await consumer.Consume(consumeContext);
            // Then

            ctx.VERIFY_PRODUCTS.Count().Should().Be(0);
               logger.Verify(x => x.Log(
                          LogLevel.Information,
                          It.IsAny<EventId>(),
                          It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Removed verification record for product")),
                          It.IsAny<Exception>(),
                          It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                        ),Times.Once);
             
        }
        [Fact]
        public async Task Consume_Return_When_ProductId_Is_Less_Or_zero()
        {
            // Given
            var consumer = new ProductDeletedConsumer(factory.Object, logger.Object);
            var consumeContext = Mock.Of<ConsumeContext<ProductDeleted>>(X => X.Message == new ProductDeleted(0, 0));
            // When
            await consumer.Consume(consumeContext);
            // Then
            factory.Verify(x => x.CreateScope(), Times.Never);
             

        }
        [Fact]
        public async Task Consume_Return_when_related_ProductId_Record_Nor_Found()
        {
            // Given
            var consumer = new ProductDeletedConsumer(factory.Object, logger.Object);
            var consumeContext = Mock.Of<ConsumeContext<ProductDeleted>>(X => X.Message == new ProductDeleted(1, 2));
            // When 
            await consumer.Consume(consumeContext);
            // Then
            logger.Verify(x => x.Log(
                          LogLevel.Error,
                          It.IsAny<EventId>(),
                          It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("current productId related record Not Found")),
                          It.IsAny<Exception>(),
                          It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                        ),Times.Once);

        }
        [Fact]
        public async Task Consume_Return_when_related_SellerId_is_not_match_with_DeletedByUserId()
        {
            // Given
            var consumer = new ProductDeletedConsumer(factory.Object, logger.Object);
              var data = seedData(SellerId: 3);
            var consumeContext = Mock.Of<ConsumeContext<ProductDeleted>>(X => X.Message == new ProductDeleted(data.ProductId, 2));
            ctx.VERIFY_PRODUCTS.Add(data);
            ctx.SaveChanges();
            // When 
            await consumer.Consume(consumeContext);
            // Then
            logger.Verify(x => x.Log(
                          LogLevel.Error,
                          It.IsAny<EventId>(),
                          It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("current productId related seller id is not match with ProductDeletedid")),
                          It.IsAny<Exception>(),
                          It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                        ),Times.Once);
        }
    }
}