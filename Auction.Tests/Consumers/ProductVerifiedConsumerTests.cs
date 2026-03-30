using Xunit;
using Moq;
using MassTransit;
using AUCTION.Consumers;
using AUCTION.Data.Repositories.Interfaces;
using Messaging.Contracts;
using AUCTION.Data.Entities;

public class ProductVerifiedConsumerTests
{
    private readonly Mock<IAuctionRepository> _repoMock;
    private readonly ProductVerifiedConsumer _consumer;

    public ProductVerifiedConsumerTests()
    {
        _repoMock = new Mock<IAuctionRepository>();
        _consumer = new ProductVerifiedConsumer(_repoMock.Object);
    }

    [Fact]
    public async Task Consume_Should_Update_Auction_When_Exists()
    {
        // Arrange
        var message = new ProductVerified  (ProductId : 10 );

        var auction = new Auction{
        
            Id = 1,
            ProductId = 10,
            Status = AuctionStatus.Upcoming};

        _repoMock
            .Setup(x => x.GetbyProductId(10))
            .ReturnsAsync(auction);

        var contextMock = new Mock<ConsumeContext<ProductVerified>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal(AuctionStatus.Verified, auction.Status);

        _repoMock.Verify(x => x.UpdateAsync(auction), Times.Once);
        _repoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Consume_Should_Do_Nothing_When_Auction_Not_Found()
    {
        // Arrange
        var message = new ProductVerified(  ProductId : 99 );

        _repoMock
            .Setup(x => x.GetbyProductId(99))
            .ReturnsAsync((Auction?)null);

        var contextMock = new Mock<ConsumeContext<ProductVerified>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _repoMock.Verify(x => x.UpdateAsync(It.IsAny<Auction>()), Times.Never);
        _repoMock.Verify(x => x.SaveChangesAsync(), Times.Never);
    }
}




