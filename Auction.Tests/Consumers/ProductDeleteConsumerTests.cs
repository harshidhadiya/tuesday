using Xunit;
using Moq;
using MassTransit;
using Microsoft.Extensions.Logging;
using AUCTION.Consumers;
using AUCTION.Services.Interfaces;
using AUCTION.Data.Repositories.Interfaces;
using Messaging.Contracts;
using System.Threading.Tasks;

public class ProductDeleteConsumerTests
{
    private readonly Mock<IAuctionRepository> _repoMock = new();
    private readonly Mock<IAuctionService> _serviceMock = new();
    private readonly Mock<ILogger<ProductDeleteConsumer>> _loggerMock = new();

    private readonly ProductDeleteConsumer _consumer;

    public ProductDeleteConsumerTests()
    {
        _consumer = new ProductDeleteConsumer(
            _repoMock.Object,
            _serviceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Consume_Should_Call_Service_When_ProductId_Is_Valid()
    {
        // Arrange
        var message = new ProductDeleted
        (
            ProductId : 10,
            DeletedByUserId : 5
        );

        var contextMock = new Mock<ConsumeContext<ProductDeleted>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _serviceMock.Verify(x =>
            x.forceFullyclosed(10, 5),
            Times.Once
        );
    }

    [Fact]
    public async Task Consume_Should_Not_Call_Service_When_ProductId_Is_Zero()
    {
        // Arrange
        var message = new ProductDeleted
        (
            ProductId : 0,
            DeletedByUserId : 5
        );

        var contextMock = new Mock<ConsumeContext<ProductDeleted>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _serviceMock.Verify(x =>
            x.forceFullyclosed(It.IsAny<int>(), It.IsAny<int>()),
            Times.Never
        );
    }
}

