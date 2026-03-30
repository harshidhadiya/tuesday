using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Messaging.Contracts;
using PRODUCT.Messaging.Consumers;
using PRODUCT.Model;
using Moq;

public class ProductVerifiedConsumerTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<ILogger<ProductVerifiedConsumer>> _loggerMock = new();

    private MACUTIONDB GetDbContext()
    {
        var options = new DbContextOptionsBuilder<MACUTIONDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MACUTIONDB(options);
    }

    private ProductVerifiedConsumer GetConsumer(MACUTIONDB db)
    {
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(MACUTIONDB)))
            .Returns(db);

        _scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);

        return new ProductVerifiedConsumer(
            _scopeFactoryMock.Object,
            _loggerMock.Object
        );
    }


    [Fact]
    public async Task Consume_Should_DoNothing_When_ProductId_Invalid()
    {
        var db = GetDbContext();
        var consumer = GetConsumer(db);

        var message = new ProductVerified(0);

        var context = Mock.Of<ConsumeContext<ProductVerified>>(x => x.Message == message);

        await consumer.Consume(context);

        // verify no product created/changed
        db.PRODUCTS.Count().Should().Be(0);
    }

    [Fact]
    public async Task Consume_Should_Verify_Product_When_Exists()
    {
        var db = GetDbContext();

        var product = new ProductTable
        {
            Id = 1,
            product_name = "Test Product",   
            user_id = 10,                 
            isVerified = false
        };

        db.PRODUCTS.Add(product);
        await db.SaveChangesAsync();

        var consumer = GetConsumer(db);

        var message = new ProductVerified(1);

        var context = Mock.Of<ConsumeContext<ProductVerified>>(x => x.Message == message);

        await consumer.Consume(context);

        var updated = await db.PRODUCTS.FirstAsync();
        updated.isVerified.Should().BeTrue();        
    }

}