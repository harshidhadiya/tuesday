using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using USER.Messaging.Consumer;
using USER.Model;

namespace UserService_Tests.ConsumerTest;

public class refreshTokenConsumerTest
{

    private readonly Mock<ILogger<refreshTokenConsumer>> logger;
    private MACUTIONDB db;
    public refreshTokenConsumerTest()
    {
        logger = new Mock<ILogger<refreshTokenConsumer>>();
    }
    public DbContextOptionsBuilder<MACUTIONDB> GetInMemoryDbOptions()
    {
        return new DbContextOptionsBuilder<MACUTIONDB>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());
    }
    [Fact]
    public async Task Consume_successfully()
    {
        // Given
        var options = GetInMemoryDbOptions().Options;
        using (var context = new MACUTIONDB(options))
        {
            var consumer = new refreshTokenConsumer(context, logger.Object);
            var message = new RefreshTokenGenerate
           (
                userId: 1,
                refreshToken: "new_refresh_token",
                expiryDate: DateTime.UtcNow.AddDays(7),
                name: "harshid",
                role: "ADMIN"
           );
           var consumeContextMock= Mock.Of<ConsumeContext<RefreshTokenGenerate>>(C=>C.Message==message);
           
            // When
            await consumer.Consume(consumeContextMock);
            // Then
            var count= await context.refreshTables.CountAsync();
            count.Should().Be(1);
            logger.Verify(l => l.Log(
                It.Is<LogLevel>(logLevel => logLevel == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("RefreshToken Saved Successfully")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);
        }
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Not_Consume_when_some_message_field_Empty(int opt)
    {
        // Given
        var options = GetInMemoryDbOptions().Options;
        using (var context = new MACUTIONDB(options))
        {
            var consumer = new refreshTokenConsumer(context, logger.Object);
            var message = new RefreshTokenGenerate
           (
                userId:opt==1?0:opt,
                refreshToken: opt==2?"":"new_refresh_token",
                expiryDate: DateTime.UtcNow.AddDays(7),
                name: opt==3?"": "harshid",
                role: opt==4?"":"ADMIN"
           );
           var consumeContextMock= Mock.Of<ConsumeContext<RefreshTokenGenerate>>(C=>C.Message==message);
           
            // When
            await consumer.Consume(consumeContextMock);
            // Then
            var count= await context.refreshTables.CountAsync();
            count.Should().Be(0);
            logger.Verify(l => l.Log(
                It.Is<LogLevel>(logLevel => logLevel == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("sorry but some data")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);
        }
    }
}