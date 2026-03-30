using Xunit;
using Moq;
using MassTransit;
using Microsoft.Extensions.Logging;
using AUCTION.Consumers;
using AUCTION.Services.Interfaces;
using AUCTION.Data.Repositories.Interfaces;
using Messaging.Contracts;

public class ProductUnverifiedConsumerTests
{
    private readonly Mock<IAuctionRepository> _repoMock;
    private readonly Mock<IAuctionService> _serviceMock;
    private readonly Mock<ILogger<ProductUnverifiedConsumer>> _loggerMock;
    private readonly ProductUnverifiedConsumer _consumer;

    public ProductUnverifiedConsumerTests()
    {
        _repoMock = new Mock<IAuctionRepository>();
        _serviceMock = new Mock<IAuctionService>();
        _loggerMock = new Mock<ILogger<ProductUnverifiedConsumer>>();

        _consumer = new ProductUnverifiedConsumer(
            _repoMock.Object,
            _serviceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Consume_Should_Call_Service_When_ProductId_Is_Valid()
    {
        // Arrange
        var message = new ProductUnverified
        (
            ProductId: 10,
            AdminId :5
        );

        var contextMock = new Mock<ConsumeContext<ProductUnverified>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _serviceMock.Verify(
            x => x.ProductUnverifyHandling(10, 5),
            Times.Once
        );
    }

    [Fact]
    public async Task Consume_Should_Not_Call_Service_When_ProductId_Is_Zero()
    {
        // Arrange
        var message = new ProductUnverified
        (
            ProductId : 0,
            AdminId : 5
        );

        var contextMock = new Mock<ConsumeContext<ProductUnverified>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _serviceMock.Verify(
            x => x.ProductUnverifyHandling(It.IsAny<int>(), It.IsAny<int>()),
            Times.Never
        );
    }
}

