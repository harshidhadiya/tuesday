using AUCTION.Data.Entities;
using AUCTION.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AUCTION.Hubs;

[Authorize]
public class AuctionHub : Hub
{
    private readonly IRedisService _redis;
    private readonly ILogger<AuctionHub> _logger;

    public AuctionHub(IRedisService redis, ILogger<AuctionHub> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    // Client calls this when they open an auction page
    public async Task JoinAuction(string auctionId)
    {

        if (!int.TryParse(auctionId, out var id)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"auction_{auctionId}");
        await _redis.IncrementViewerCountAsync(id);
        var count = await _redis.GetViewerCountAsync(id);
        await Clients.Group($"auction_{auctionId}").SendAsync("ViewerCountUpdated", count);

        _logger.LogInformation("User {User} joined auction room {AuctionId}", Context.UserIdentifier, auctionId);
    }

    // Client calls this when they leave the auction page
    public async Task LeaveAuction(string auctionId)
    {
        if (!int.TryParse(auctionId, out var id)) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction_{auctionId}");
        await _redis.DecrementViewerCountAsync(id);

        var count = await _redis.GetViewerCountAsync(id);
        await Clients.Group($"auction_{auctionId}").SendAsync("ViewerCountUpdated", count);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

// ── Hub service — used by other services to push messages into rooms ──────────

public interface IAuctionHubService
{
    Task BroadcastBidPlaced(int auctionId, object data);
    Task BroadcastAuctionStarted(int auctionId);
    Task BroadcastAuctionClosed(int auctionId, object data);
    Task BroadcastEndingSoon(int auctionId, int minutesRemaining);
    Task BroadcastTimerTick(int auctionId, double secondsRemaining);
    Task AuctionMessage(int auctionId, string message);
    Task BroadcastProductDeleted(int auctionId);
    Task BroadcastProductUnverified(int auctionId);
}

public class AuctionHubService : IAuctionHubService
{
    private readonly IHubContext<AuctionHub> _hub;
    public AuctionHubService(IHubContext<AuctionHub> hub) => _hub = hub;

    private IClientProxy Room(int auctionId)
        => _hub.Clients.Group($"auction_{auctionId}");

    public Task BroadcastBidPlaced(int auctionId, object data)
        => Room(auctionId).SendAsync("BidPlaced", data);

    public Task BroadcastAuctionStarted(int auctionId)
        => Room(auctionId).SendAsync("AuctionStarted", new { auctionId });

    public Task BroadcastAuctionClosed(int auctionId, object data)
        => Room(auctionId).SendAsync("AuctionClosed", data);

    public Task BroadcastEndingSoon(int auctionId, int minutesRemaining)
        => Room(auctionId).SendAsync("AuctionEndingSoon", new { auctionId, minutesRemaining });
    public Task AuctionMessage(int auctionId,string message)
    => Room(auctionId).SendAsync("AuctionMessage", new { message });
    
    

    public Task BroadcastTimerTick(int auctionId, double secondsRemaining)
        => Room(auctionId).SendAsync("TimerTick", new { auctionId, secondsRemaining });

    public Task BroadcastProductDeleted(int auctionId)
        => Room(auctionId).SendAsync("AuctionAborted", new { auctionId, reason = "Product deleted by owner" });

    public Task BroadcastProductUnverified(int auctionId)
        => Room(auctionId).SendAsync("AuctionUnverified", new { auctionId, reason = "Product un-verified during live auction" });
}
