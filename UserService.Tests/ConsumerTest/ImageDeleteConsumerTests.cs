using Moq;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using USER.Messaging.Consumer;
using USER.Repository;
using USER.CloudinaryService;
using Messaging.Contracts;

public class ImageDeleteConsumerTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IUserRepository> _repoMock;
    private readonly Mock<IClodinaryService> _cloudinaryMock;

    private readonly ImageDeleteConsumer _consumer;

    public ImageDeleteConsumerTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _repoMock = new Mock<IUserRepository>();
        _cloudinaryMock = new ();

        _scopeFactoryMock
            .Setup(x => x.CreateScope())
            .Returns(_scopeMock.Object);

        _scopeMock
            .Setup(x => x.ServiceProvider)
            .Returns(_serviceProviderMock.Object);

        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IUserRepository)))
            .Returns(_repoMock.Object);

        _serviceProviderMock
            .Setup(x => x.GetService(typeof(IClodinaryService)))
            .Returns(_cloudinaryMock.Object);

        _consumer = new ImageDeleteConsumer(_scopeFactoryMock.Object);
    }

    [Fact]
    public async Task Consume_Should_Call_DeleteFile_When_PublicId_Is_Valid()
    {
        // Arrange
        var message = new productDeleteImage("test-public-id");

        var contextMock = new Mock<ConsumeContext<productDeleteImage>>();
        contextMock.Setup(x => x.Message).Returns(message);

        _cloudinaryMock
            .Setup(x => x.deleteFile("test-public-id"))
            .Returns(Task.CompletedTask);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _cloudinaryMock.Verify(x => x.deleteFile("test-public-id"), Times.Once);
    }

    [Fact]
    public async Task Consume_Should_Not_Call_DeleteFile_When_PublicId_Is_Null()
    {
        // Arrange
        var message = new productDeleteImage(null);

        var contextMock = new Mock<ConsumeContext<productDeleteImage>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _cloudinaryMock.Verify(x => x.deleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Consume_Should_Not_Call_DeleteFile_When_PublicId_Is_Empty()
    {
        // Arrange
        var message = new productDeleteImage("");

        var contextMock = new Mock<ConsumeContext<productDeleteImage>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        _cloudinaryMock.Verify(x => x.deleteFile(It.IsAny<string>()), Times.Never);
    }
}