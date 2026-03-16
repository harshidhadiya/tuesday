using AUCTION.Consumers;
using AUCTION.Data;
using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Hubs;
using AUCTION.Services;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Messaging.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AUCTION.Tests.Integration;

// ── WebApplicationFactory — swaps out infrastructure for tests ────────────────
public class AuctionWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace PostgreSQL with InMemory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuctionDbContext>));
            if (descriptor != null) services.Remove(descriptor);
            services.AddDbContext<AuctionDbContext>(opt =>
                opt.UseInMemoryDatabase("integration_" + Guid.NewGuid()));

            // Replace Redis with no-op
            services.AddSingleton<IRedisService, NoOpRedisService>();

            // Replace SignalR hub service with no-op
            services.AddScoped<IAuctionHubService, NoOpHubService>();

            // Replace MassTransit with in-memory test harness
            services.AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<ProductVerifiedConsumer>();
                x.AddConsumer<ProductUnverifiedConsumer>();
                x.AddConsumer<ProductDeletedConsumer>();
            });
        });
    }
}

// ── No-op stubs ───────────────────────────────────────────────────────────────
public class NoOpRedisService : IRedisService
{
    public Task SetHighestBidAsync(int id, HighestBidCacheDto bid) => Task.CompletedTask;
    public Task<HighestBidCacheDto?> GetHighestBidAsync(int id)    => Task.FromResult<HighestBidCacheDto?>(null);
    public Task<bool> SetBidLockAsync(int a, int u, TimeSpan e)    => Task.FromResult(true);
    public Task ReleaseBidLockAsync(int a, int u)                  => Task.CompletedTask;
    public Task IncrementViewerCountAsync(int id)                  => Task.CompletedTask;
    public Task DecrementViewerCountAsync(int id)                  => Task.CompletedTask;
    public Task<long> GetViewerCountAsync(int id)                  => Task.FromResult(0L);
    public Task DeleteAuctionCacheAsync(int id)                    => Task.CompletedTask;
}

public class NoOpHubService : IAuctionHubService
{
    public Task BroadcastBidPlaced(int id, object d)       => Task.CompletedTask;
    public Task BroadcastAuctionStarted(int id)            => Task.CompletedTask;
    public Task BroadcastAuctionClosed(int id, object d)   => Task.CompletedTask;
    public Task BroadcastEndingSoon(int id, int m)         => Task.CompletedTask;
    public Task BroadcastTimerTick(int id, double s)       => Task.CompletedTask;
}

// ── Controller integration tests ──────────────────────────────────────────────
public class AuctionControllerIntegrationTests : IClassFixture<AuctionWebAppFactory>
{
    private readonly HttpClient           _client;
    private readonly AuctionWebAppFactory _factory;

    public AuctionControllerIntegrationTests(AuctionWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── GET /api/auctions (public) ────────────────────────────────────────────

    [Fact]
    public async Task GetAll_NoAuth_Returns200()
    {
        var response = await _client.GetAsync("/api/auctions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_FilterByStatus_Returns200()
    {
        await SeedAsync(AuctionStatus.Live);
        var response = await _client.GetAsync("/api/auctions?Status=Live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /api/auctions/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingAuction_Returns200()
    {
        var id       = await SeedAsync(AuctionStatus.Live);
        var response = await _client.GetAsync($"/api/auctions/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var response = await _client.GetAsync("/api/auctions/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/auctions ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAuction_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsync("/api/auctions", Json(new
        {
            productId       = 1,
            startingPrice   = 100,
            minBidIncrement = 10,
            startDate       = DateTime.UtcNow.AddHours(1),
            endDate         = DateTime.UtcNow.AddDays(1)
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAuction_InvalidDates_Returns400()
    {
        SetAuth(SeedData.UserId1, isVerified: true);
        var response = await _client.PostAsync("/api/auctions", Json(new
        {
            productId       = 1,
            startingPrice   = 100,
            minBidIncrement = 10,
            startDate       = DateTime.UtcNow.AddHours(-2),  // past!
            endDate         = DateTime.UtcNow.AddDays(1)
        }));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/auctions/{id}/bids ──────────────────────────────────────────

    [Fact]
    public async Task PlaceBid_OnLiveAuction_Returns201()
    {
        var id = await SeedAsync(AuctionStatus.Live);
        SetAuth(SeedData.UserId2);   // different user from creator

        var response = await _client.PostAsync($"/api/auctions/{id}/bids",
            Json(new { amount = 200m }));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PlaceBid_NoAuth_Returns401()
    {
        var id = await SeedAsync(AuctionStatus.Live);
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsync($"/api/auctions/{id}/bids",
            Json(new { amount = 200m }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PlaceBid_OnUpcomingAuction_Returns400()
    {
        var id = await SeedAsync(AuctionStatus.Upcoming);
        SetAuth(SeedData.UserId2);

        var response = await _client.PostAsync($"/api/auctions/{id}/bids",
            Json(new { amount = 200m }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/auctions/{id}/bids (public) ─────────────────────────────────

    [Fact]
    public async Task GetBidHistory_NoAuth_Returns200()
    {
        var id       = await SeedAsync(AuctionStatus.Live);
        var response = await _client.GetAsync($"/api/auctions/{id}/bids");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHighestBid_NoAuth_Returns200()
    {
        var id       = await SeedAsync(AuctionStatus.Live);
        var response = await _client.GetAsync($"/api/auctions/{id}/bids/highest");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Watchlist ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WatchAuction_AuthenticatedUser_Returns200()
    {
        var id = await SeedAsync(AuctionStatus.Live);
        SetAuth(SeedData.UserId2);

        var response = await _client.PostAsync($"/api/auctions/{id}/watch", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WatchAuction_Duplicate_Returns409()
    {
        var id = await SeedAsync(AuctionStatus.Live);
        SetAuth(SeedData.UserId2);

        await _client.PostAsync($"/api/auctions/{id}/watch", null);  // first
        var second = await _client.PostAsync($"/api/auctions/{id}/watch", null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetWatched_Returns200()
    {
        SetAuth(SeedData.UserId2);
        var response = await _client.GetAsync("/api/auctions/watched");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── PATCH /api/auctions/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateAuction_ByOwner_Returns200()
    {
        var id = await SeedAsync(AuctionStatus.Upcoming);
        SetAuth(SeedData.UserId1);   // UserId1 is the creator

        var response = await _client.PatchAsync($"/api/auctions/{id}",
            Json(new { startingPrice = 350m }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateAuction_ByNonOwner_Returns403()
    {
        var id = await SeedAsync(AuctionStatus.Upcoming);
        SetAuth(SeedData.UserId2);   // NOT the creator

        var response = await _client.PatchAsync($"/api/auctions/{id}",
            Json(new { startingPrice = 350m }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/auctions/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task CancelAuction_ByOwner_Returns200()
    {
        var id = await SeedAsync(AuctionStatus.Upcoming);
        SetAuth(SeedData.UserId1);

        var response = await _client.DeleteAsync($"/api/auctions/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelAuction_LiveAuction_Returns400()
    {
        var id = await SeedAsync(AuctionStatus.Live);
        SetAuth(SeedData.UserId1);

        var response = await _client.DeleteAsync($"/api/auctions/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Admin force-close ─────────────────────────────────────────────────────

    [Fact]
    public async Task ForceClose_AsAdmin_Returns200()
    {
        var id = await SeedAsync(AuctionStatus.Live);
        SetAuth(SeedData.AdminId, role: "Admin");

        var response = await _client.PatchAsync($"/api/admin/auctions/{id}/force-close", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForceClose_AsNonAdmin_Returns403()
    {
        var id = await SeedAsync(AuctionStatus.Live);
        SetAuth(SeedData.UserId1, role: "User");

        var response = await _client.PatchAsync($"/api/admin/auctions/{id}/force-close", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Response shape validation ─────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ResponseShape_HasSuccessAndData()
    {
        await SeedAsync(AuctionStatus.Live);
        var response = await _client.GetAsync("/api/auctions");
        var body     = await response.Content.ReadAsStringAsync();
        var doc      = JsonDocument.Parse(body).RootElement;

        doc.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.GetProperty("data").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetById_ResponseShape_HasCorrectFields()
    {
        var id       = await SeedAsync(AuctionStatus.Live);
        var response = await _client.GetAsync($"/api/auctions/{id}");
        var body     = await response.Content.ReadAsStringAsync();
        var doc      = JsonDocument.Parse(body).RootElement;

        doc.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = doc.GetProperty("data");
        data.GetProperty("id").GetInt32().Should().Be(id);
        data.TryGetProperty("status", out _).Should().BeTrue();
        data.TryGetProperty("currentHighestBid", out _).Should().BeTrue();
        data.TryGetProperty("totalBids", out _).Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int> SeedAsync(AuctionStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();

        var auction = status switch
        {
            AuctionStatus.Live  => SeedData.LiveAuction(),
            AuctionStatus.Ended => SeedData.EndedAuction(),
            _                   => SeedData.UpcomingAuction()
        };

        ctx.Auctions.Add(auction);
        await ctx.SaveChangesAsync();
        return auction.Id;
    }

    private void SetAuth(int userId, string role = "User", bool isVerified = true)
    {
        // In a real setup, generate a proper signed JWT here using the same
        // Jwt:Key configured in appsettings. The test factory's JWT middleware
        // will validate it. For now this sets a placeholder header.
        // See: https://docs.microsoft.com/aspnet/core/test/integration-tests
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",
                $"test-{userId}-{role}-{isVerified}");
    }

    private static StringContent Json(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");
}

// ── MassTransit consumer tests (using in-memory test harness) ─────────────────
public class ConsumerTests : IAsyncLifetime
{
    private readonly ITestHarness    _harness;
    private readonly ServiceProvider _provider;

    public ConsumerTests()
    {
        _provider = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AuctionDbContext>(opt =>
                opt.UseInMemoryDatabase("consumer_test_" + Guid.NewGuid()))
            .AddScoped<IAuctionRepository, Data.Repositories.AuctionRepository>()
            .AddScoped<IBidRepository,     Data.Repositories.BidRepository>()
            .AddScoped<IWatchlistRepository, Data.Repositories.WatchlistRepository>()
            .AddScoped<IRedisService, NoOpRedisService>()
            .AddScoped<IAuctionHubService, NoOpHubService>()
            .AddScoped<Services.Interfaces.IAuctionService, Services.AuctionService>()
            .AddScoped<Services.Interfaces.IBidService,     Services.BidService>()
            .AddScoped<Services.Interfaces.IWatchlistService, Services.WatchlistService>()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<ProductVerifiedConsumer>();
                x.AddConsumer<ProductUnverifiedConsumer>();
                x.AddConsumer<ProductDeletedConsumer>();
            })
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
    }

    public async Task InitializeAsync() => await _harness.Start();
    public async Task DisposeAsync()    => await _harness.Stop();

    [Fact]
    public async Task ProductVerified_MessagePublished_ConsumerReceivesIt()
    {
        await _harness.Bus.Publish(new ProductVerified(ProductId: 42));

        // Give consumer time to process
        (await _harness.Consumed.Any<ProductVerified>()).Should().BeTrue();

        var consumerHarness = _harness.GetConsumerHarness<ProductVerifiedConsumer>();
        (await consumerHarness.Consumed.Any<ProductVerified>()).Should().BeTrue();
    }

    [Fact]
    public async Task ProductDeleted_MessagePublished_ConsumerReceivesIt()
    {
        await _harness.Bus.Publish(new ProductDeleted(ProductId: 10, DeletedByUserId: 1));

        (await _harness.Consumed.Any<ProductDeleted>()).Should().BeTrue();

        var consumerHarness = _harness.GetConsumerHarness<ProductDeletedConsumer>();
        (await consumerHarness.Consumed.Any<ProductDeleted>()).Should().BeTrue();
    }

    [Fact]
    public async Task ProductUnverified_MessagePublished_ConsumerReceivesIt()
    {
        await _harness.Bus.Publish(new ProductUnverified(ProductId: 10, AdminId: 999));

        (await _harness.Consumed.Any<ProductUnverified>()).Should().BeTrue();
    }
}
