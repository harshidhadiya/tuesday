using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using AUCTION.Services.Interfaces;
using MassTransit;
using Messaging.Contracts;

namespace AUCTION.BackgroundJobs;

/// <summary>
/// Runs every 30 seconds.
/// • Starts upcoming auctions whose StartDate has arrived.
/// • Closes live auctions whose EndDate has passed.
/// • Fires AuctionEndingSoon for auctions expiring within 5 minutes.
/// • Broadcasts TimerTick via SignalR for all live auctions.
/// </summary>
public class AuctionSchedulerJob : BackgroundService
{
    private readonly IServiceScopeFactory      _scopeFactory;
    private readonly ILogger<AuctionSchedulerJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public AuctionSchedulerJob(
        IServiceScopeFactory scopeFactory,
        ILogger<AuctionSchedulerJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auction Scheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try   { await TickAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "Error in auction scheduler tick"); }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task TickAsync()
    {
        using var scope          = _scopeFactory.CreateScope();
        var auctionService       = scope.ServiceProvider.GetRequiredService<IAuctionService>();
        var auctionRepo          = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
        var publish              = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var hub                  = scope.ServiceProvider.GetRequiredService<IAuctionHubService>();

        var toStart = await auctionRepo.GetUpcomingAuctionsDueToStartAsync();
        foreach (var auction in toStart)
        {
            _logger.LogInformation("Starting auction {AuctionId}", auction.Id);
            await auctionService.StartAuctionAsync(auction.Id);
        }

        var toClose = await auctionRepo.GetLiveAuctionsDueToCloseAsync();
        foreach (var auction in toClose)
        {
            _logger.LogInformation("Closing auction {AuctionId}", auction.Id);
            await auctionService.CloseAuctionAsync(auction.Id);
        }

        var endingSoon = await auctionRepo.GetLiveAuctionsEndingSoonAsync(5);
           
        foreach (var auction in endingSoon)
        {

            var minutesLeft = (int)Math.Ceiling((auction.EndDate - TimeHelper.Now()).TotalMinutes);

            await publish.Publish(new AuctionEndingSoon(
                auction.Id, auction.EndDate, minutesLeft));

            await hub.BroadcastEndingSoon(auction.Id, minutesLeft);
           
            await hub.BroadcastTimerTick(auction.Id, (auction.EndDate - TimeHelper.Now()).TotalSeconds);
            _logger.LogInformation(
                "Auction {AuctionId} ending in {Minutes} minutes", auction.Id, minutesLeft);
        }
            _logger.LogInformation("this is the time {time}",TimeHelper.Now());

        var (liveAuctions, _) = await auctionRepo.GetAllAsync(
            new Data.Dto.Request.AuctionFilterRequest
            {
                Status   = Data.Entities.AuctionStatus.Live,
                PageSize = 200
            });

        foreach (var auction in liveAuctions)
        {
           
            var remaining = auction.EndDate - TimeHelper.Now();
            if (remaining > TimeSpan.Zero )
                await hub.BroadcastTimerTick(auction.Id, remaining.TotalSeconds);
        }
    }
}
