using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Messaging.Contracts;
using PRODUCT.Messaging.Consumers;
using PRODUCT.Model;
using Moq;

public class ProductUnverifiedConsumerTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<IPublishEndpoint> _publishMock = new();
    private readonly Mock<ILogger<ProductUnverifiedConsumer>> _loggerMock = new();

    private MACUTIONDB GetDbContext()
    {
        var options = new DbContextOptionsBuilder<MACUTIONDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MACUTIONDB(options);
    }

    private ProductUnverifiedConsumer GetConsumer(MACUTIONDB db)
    {
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(MACUTIONDB)))
            .Returns(db);

        _scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);

        return new ProductUnverifiedConsumer(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            _publishMock.Object
        );
    }
    
    [Fact]
    public async Task Consume_Should_DoNothing_When_ProductId_Invalid()
    {
        var db = GetDbContext();
        var consumer = GetConsumer(db);

        var message = new ProductUnverified(0, 1);

        var context = Mock.Of<ConsumeContext<ProductUnverified>>(x => x.Message == message);

        await consumer.Consume(context);

        _publishMock.Verify(x => x.Publish(It.IsAny<ProductUnverifiedFromService>(), default), Times.Never);
    }
    
    [Fact]
    public async Task Consume_Should_Throw_When_Product_NotFound()
    {
        var db = GetDbContext();
        var consumer = GetConsumer(db);

        var message = new ProductUnverified(1, 1);

        var context = Mock.Of<ConsumeContext<ProductUnverified>>(x => x.Message == message);

        Func<Task> act = async () => await consumer.Consume(context);

        await act.Should().ThrowAsync<NullReferenceException>();
    }
    
    [Fact]
    public async Task Consume_Should_Unverify_Product_And_Publish_Event()
    {
        var db = GetDbContext();

        var product = new ProductTable
        {
            Id = 1,
            product_name = "Test Product",   
            user_id = 10,
            AuctionStartTime = DateTime.UtcNow,
            AuctionEndTime = DateTime.UtcNow.AddHours(1),
            isVerified = true
        };

        db.PRODUCTS.Add(product);
        await db.SaveChangesAsync();

        var consumer = GetConsumer(db);



        var message = new ProductUnverified(1, 99);

        var context = Mock.Of<ConsumeContext<ProductUnverified>>(x => x.Message == message);

        await consumer.Consume(context);

        var updated = await db.PRODUCTS.FirstAsync();

        updated.AuctionStartTime.Should().BeNull();
        updated.AuctionEndTime.Should().BeNull();
        updated.isVerified.Should().BeFalse();

        _publishMock.Verify(x =>
            x.Publish(
                It.Is<ProductUnverifiedFromService>(p => p.ProductId == 1),
                default),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("marked unverified")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Once
        );
    }
}