
using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using PRODUCT.Messaging.Consumers;
using PRODUCT.Model;
using PRODUCT.Repository;

public class ProductAddAuctionConsumerTests
{
    private readonly Mock<Irepository> _repoMock = new();
    private readonly Mock<ILogger<ProductAddAuctionConsumer>> _loggerMock = new();

    private ProductAddAuctionConsumer GetConsumer()
    {
        return new ProductAddAuctionConsumer(_repoMock.Object, _loggerMock.Object);
    }


    [Fact]
    public async Task Consume_Should_DoNothing_When_Product_NotFound()
    {
        var consumer = GetConsumer();

        var message = new ProductAddAuctionDate
        (
            productId: 1,
            StartDate : DateTime.UtcNow,
            EndDate :DateTime.UtcNow.AddHours(1)
        );

        var context = Mock.Of<ConsumeContext<ProductAddAuctionDate>>(x => x.Message == message);

        _repoMock.Setup(x => x.getByIdProduct(1))
                 .ReturnsAsync((ProductTable)null);

        await consumer.Consume(context);

        _repoMock.Verify(x => x.Update(It.IsAny<ProductTable>()), Times.Never);
    }


    [Fact]
    public async Task Consume_Should_Update_Product_When_ProductExists()
    {
        var consumer = GetConsumer();

        var product = new ProductTable
        {
            Id = 1
        };

        var start = DateTime.UtcNow;
        var end = start.AddHours(2);

        var message = new ProductAddAuctionDate
        (
            productId: 1,
            StartDate : start,
            EndDate :end
        );

        var context = Mock.Of<ConsumeContext<ProductAddAuctionDate>>(x => x.Message == message);

        _repoMock.Setup(x => x.getByIdProduct(1))
                 .ReturnsAsync(product);

        await consumer.Consume(context);

        // ✔ values updated
        product.AuctionStartTime.Should().Be(start);
        product.AuctionEndTime.Should().Be(end);

        // ✔ repo update called
        _repoMock.Verify(x => x.Update(product), Times.Once);
    }


}