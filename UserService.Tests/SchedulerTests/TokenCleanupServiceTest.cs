using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using USER.Model;
using USER.Services;

namespace UserService_Tests.SchedulerTests
{
    public class TokenCleanupServiceTest
    {
       private readonly Mock<ILogger<TokenCleanupService>> _loggerMock;
       private readonly Mock<IServiceScope> _scope;
       private readonly Mock<IServiceProvider> _serviceProviderMock;

    public TokenCleanupServiceTest  ()
    {
        _loggerMock = new Mock<ILogger<TokenCleanupService>>();
        _scope=new();
        _serviceProviderMock=new();
    }

    private IServiceProvider BuildServiceProvider(string dbName)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MACUTIONDB>(options =>
            options.UseInMemoryDatabase(dbName));

        return services.BuildServiceProvider();
   
         var database = new MACUTIONDB(new DbContextOptionsBuilder<MACUTIONDB>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
      
    
    }

    [Fact]
    public async Task CleanExpiredTokensInBatches_Should_Delete_Expired_Tokens()
    {
        // Arrange
        var provider = BuildServiceProvider("TestDb1");

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();

            db.refreshTables.AddRange(
                new RefreshTable { expiryDate = DateTime.UtcNow.AddMinutes(-10) },
                new RefreshTable { expiryDate = DateTime.UtcNow.AddMinutes(-5) },
                new RefreshTable { expiryDate = DateTime.UtcNow.AddMinutes(10) } // valid
            );

            await db.SaveChangesAsync();
        }

        var service = new TokenCleanupService(provider, _loggerMock.Object);

        // Act
        await service.CleanExpiredTokensInBatches(CancellationToken.None);

        // Assert
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();
            var remaining = await db.refreshTables.ToListAsync();

            remaining.Should().HaveCount(1);
            remaining.First().expiryDate.Should().BeAfter(DateTime.UtcNow);
        }
    }

    [Fact]
    public async Task CleanExpiredTokensInBatches_Should_Not_Delete_When_No_Expired()
    {
        // Arrange
        var provider = BuildServiceProvider("TestDb2");

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();

            db.refreshTables.AddRange(
                new RefreshTable { expiryDate = DateTime.UtcNow.AddMinutes(10) },
                new RefreshTable { expiryDate = DateTime.UtcNow.AddMinutes(20) }
            );

            await db.SaveChangesAsync();
        }

        var service = new TokenCleanupService(provider, _loggerMock.Object);

        // Act
        await service.CleanExpiredTokensInBatches(CancellationToken.None);

        // Assert
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();
            var count = await db.refreshTables.CountAsync();

            count.Should().Be(2);
        }
    }

    [Fact]
    public async Task CleanExpiredTokensInBatches_Should_Handle_Batch_Deletion()
    {
        // Arrange
        var provider = BuildServiceProvider("TestDb3");

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();

            // Insert more than batch size (1000)
            for (int i = 0; i < 1500; i++)
            {
                db.refreshTables.Add(new RefreshTable
                {
                    expiryDate = DateTime.UtcNow.AddMinutes(-1)
                });
            }

            await db.SaveChangesAsync();
        }

        var service = new TokenCleanupService(provider, _loggerMock.Object);

        // Act
        await service.CleanExpiredTokensInBatches(CancellationToken.None);

        // Assert
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();
            var count = await db.refreshTables.CountAsync();

            count.Should().Be(0);
        }
    }

    [Fact]
    public async Task CleanExpiredTokensInBatches_Should_Log_When_Deleting()
    {
        // Arrange
        var provider = BuildServiceProvider("TestDb4");

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();

            db.refreshTables.Add(new RefreshTable
            {
                expiryDate = DateTime.UtcNow.AddMinutes(-1)
            });

            await db.SaveChangesAsync();
        }

        var service = new TokenCleanupService(provider, _loggerMock.Object);

        // Act
        await service.CleanExpiredTokensInBatches(CancellationToken.None);

        // Assert (Verify logging happened)
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeastOnce);
    }

    }
}