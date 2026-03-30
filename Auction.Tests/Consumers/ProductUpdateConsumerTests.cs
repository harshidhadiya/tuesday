using Xunit;
using Moq;
using MassTransit;
using Microsoft.Extensions.Logging;
using AUCTION.Consumers;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Data.Entities;
using Messaging.Contracts;
using System.Threading.Tasks;

public class ProductUpdateConsumerTests
{
    private readonly Mock<IAuctionRepository> _repoMock = new();
    private readonly Mock<ILogger<ProductUpdateConsumer>> _loggerMock = new();

    private readonly ProductUpdateConsumer _consumer;

    public ProductUpdateConsumerTests()
    {
        _consumer = new ProductUpdateConsumer(
            _repoMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Consume_Should_Update_Name_And_Description_When_Provided()
    {
        // Arrange
        var message = new ProductUpdateForVerification
        (
            ProductId : 1,
            name : "Updated Name",
            descripiton : "Updated Desc"
        );

        var auction = new Auction
        {
            ProductId = 1,
            ProductName = "Old Name",
            Description = "Old Desc"
        };

        _repoMock.Setup(x => x.GetbyProductId(1)).ReturnsAsync(auction);

        var contextMock = new Mock<ConsumeContext<ProductUpdateForVerification>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal("Updated Name", auction.ProductName);
        Assert.Equal("Updated Desc", auction.Description);

        _repoMock.Verify(x => x.UpdateAsync(auction), Times.Once);
        _repoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Consume_Should_Update_Only_Name_When_Description_Is_Null()
    {
        // Arrange
        var message = new ProductUpdateForVerification
        (
            ProductId : 1,
            name : "Updated Name",
            descripiton : null
        );

        var auction = new Auction
        {
            ProductId = 1,
            ProductName = "Old Name",
            Description = "Old Desc"
        };

        _repoMock.Setup(x => x.GetbyProductId(1)).ReturnsAsync(auction);

        var contextMock = new Mock<ConsumeContext<ProductUpdateForVerification>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        Assert.Equal("Updated Name", auction.ProductName);
        Assert.Equal("Old Desc", auction.Description);

        _repoMock.Verify(x => x.UpdateAsync(auction), Times.Once);
        _repoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Consume_Should_Do_Nothing_When_Auction_Not_Found()
    {
        // Arrange
        var message = new ProductUpdateForVerification
       ( 
            ProductId : 99,
            name : "Name",
            descripiton : "Desc"
       );

        _repoMock.Setup(x => x.GetbyProductId(99))
                 .ReturnsAsync((Auction?)null);

        var contextMock = new Mock<ConsumeContext<ProductUpdateForVerification>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _repoMock.Verify(x => x.UpdateAsync(It.IsAny<Auction>()), Times.Never);
        _repoMock.Verify(x => x.SaveChangesAsync(), Times.Never);
    }
}
