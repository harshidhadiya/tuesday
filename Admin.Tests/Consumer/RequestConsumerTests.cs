using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ADMIN.Messaging.Consumers;
using ADMIN.Model;
using Moq;

public class RequestConsumerTests
{
    private readonly Mock<ILogger<RequestConsumer>> _loggerMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _providerMock = new();

    private RequestConsumer GetConsumer(MACUTIONDB db)
    {
        _scopeMock.Setup(x => x.ServiceProvider).Returns(_providerMock.Object);
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);

        _providerMock.Setup(x => x.GetService(typeof(MACUTIONDB)))
                     .Returns(db);

        return new RequestConsumer(_loggerMock.Object, _scopeFactoryMock.Object);
    }

    private MACUTIONDB GetDbContext()
    {
        var options = new DbContextOptionsBuilder<MACUTIONDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MACUTIONDB(options);
    }

    [Fact]
    public async Task Consume_Should_Return_When_Invalid_UserId()
    {
        var db = GetDbContext();
        var consumer = GetConsumer(db);

        var message = new AdminRegistrationRequested(0, "Name", "email@test.com");

        var context = Mock.Of<ConsumeContext<AdminRegistrationRequested>>(x => x.Message == message);

        await consumer.Consume(context);

        db.REQUESTS.Count().Should().Be(0);
    }

    [Fact]
    public async Task Consume_Should_Not_Create_When_Already_Exists()
    {
        var db = GetDbContext();

        db.REQUESTS.Add(new RequestTable
        {
            RequestUserId = 1,
            Name = "Old",
            Email = "old@test.com"
        });
        await db.SaveChangesAsync();

        var consumer = GetConsumer(db);

        var message = new AdminRegistrationRequested(1, "New", "new@test.com");

        var context = Mock.Of<ConsumeContext<AdminRegistrationRequested>>(x => x.Message == message);

        await consumer.Consume(context);

        db.REQUESTS.Count().Should().Be(1);
    }

    [Fact]
    public async Task Consume_Should_Create_New_Request_When_Not_Exists()
    {
        var db = GetDbContext();
        var consumer = GetConsumer(db);

        var message = new AdminRegistrationRequested(1, "Name", "email@test.com");

        var context = Mock.Of<ConsumeContext<AdminRegistrationRequested>>(x => x.Message == message);

        await consumer.Consume(context);

        db.REQUESTS.Count().Should().Be(1);

        var saved = db.REQUESTS.First();

        saved.Name.Should().Be("Name");
        saved.Email.Should().Be("email@test.com");
        saved.VerifiedByAdmin.Should().BeFalse();
    }
}