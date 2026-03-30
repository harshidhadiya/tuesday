using FluentAssertions;
using MassTransit;
using Messaging.Contracts;
using ADMIN.Messaging.Consumers;
using ADMIN.Repositories;
using ADMIN.Model;
using Moq;

public class AdminUpdateConsumerTests
{
    private readonly Mock<IRequestRepository> _repoMock = new();

    private adminUpdateConsumer GetConsumer()
    {
        return new adminUpdateConsumer(_repoMock.Object);
    }
    
    [Fact]
    public async Task Consume_Should_DoNothing_When_User_NotFound()
    {
        var consumer = GetConsumer();

        var message = new AdminUpdate(1, "NewName", "email@test.com");

        var context = Mock.Of<ConsumeContext<AdminUpdate>>(x => x.Message == message);
         

        await consumer.Consume(context);

        _repoMock.Verify(x => x.UpdateRequestAsync(It.IsAny<RequestTable>()), Times.Never);
    }


    [Fact]
    public async Task Consume_Should_Update_Name_And_Email_When_Provided()
    {
        var consumer = GetConsumer();

        var user = new RequestTable
        {
            RequestUserId = 1,
            Name = "Old",
            Email = "old@test.com"
        };

        var message = new AdminUpdate(1, "NewName", "new@test.com");

        var context = Mock.Of<ConsumeContext<AdminUpdate>>(x => x.Message == message);

        _repoMock.Setup(x => x.GetRequestByUserIdAsync(1))
                 .ReturnsAsync(user);

        await consumer.Consume(context);

        user.Name.Should().Be("NewName");
        user.Email.Should().Be("new@test.com");

        _repoMock.Verify(x => x.UpdateRequestAsync(user), Times.Once);
    }


    [Fact]
    public async Task Consume_Should_Update_Only_Name_When_Email_Empty()
    {
        var consumer = GetConsumer();

        var user = new RequestTable
        {
            RequestUserId = 1,
            Name = "Old",
            Email = "old@test.com"
        };

        var message = new AdminUpdate(1, "NewName", null);

        var context = Mock.Of<ConsumeContext<AdminUpdate>>(x => x.Message == message);

        _repoMock.Setup(x => x.GetRequestByUserIdAsync(1))
                 .ReturnsAsync(user);

        await consumer.Consume(context);

        user.Name.Should().Be("NewName");
        user.Email.Should().Be("old@test.com");

        _repoMock.Verify(x => x.UpdateRequestAsync(user), Times.Once);
    }
}