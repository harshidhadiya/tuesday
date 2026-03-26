using System.Collections.Concurrent;
using AUCTION.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.AspNetCore.SignalR;

namespace AUCTION.Hubs;

[Authorize]
public class AuctionHub : Hub
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _connectionViewers = new();
    private readonly IRedisService _redis;
    private readonly ILogger<AuctionHub> _logger;

    public AuctionHub(IRedisService redis, ILogger<AuctionHub> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    // Client calls this when they open an auction page (counts as a viewer)
    public async Task JoinAuction(string auctionId)
    {
        if (!int.TryParse(auctionId, out var id)) return;

        // 1. Add to group to receive all events
        await Groups.AddToGroupAsync(Context.ConnectionId, $"auction_{auctionId}");

        // 2. Increment global viewer count only if this connection isn't already a viewer for this ID
        var connectionsViewed = _connectionViewers.GetOrAdd(Context.ConnectionId, _ => new ConcurrentDictionary<int, byte>());
        if (connectionsViewed.TryAdd(id, 1))
        {
            await _redis.IncrementViewerCountAsync(id);
            var count = await _redis.GetViewerCountAsync(id);
            await Clients.Group($"auction_{auctionId}").SendAsync("ViewerCountUpdated", count);
            _logger.LogInformation("Connection {ConnectionId} (User {User}) JOINED as viewer for auction {AuctionId}. New count: {Count}", 
                Context.ConnectionId, Context.UserIdentifier, auctionId, count);
        }
    }

    public async Task ListenToAuction(string auctionId)
    {
        if (!int.TryParse(auctionId, out var id)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"auctiondetail_{auctionId}");
        _logger.LogInformation("Connection {ConnectionId} (User {User}) is now LISTENING to auction {AuctionId}", 
            Context.ConnectionId, Context.UserIdentifier, auctionId);
    }

    // Client calls this when they leave the detail page but might still have dashboard open (Listen mode)
    public async Task StopViewing(string auctionId)
    {
        if (!int.TryParse(auctionId, out var id)) return;
        if (_connectionViewers.TryGetValue(Context.ConnectionId, out var viewed) && viewed.TryRemove(id, out _))
        {
            await _redis.DecrementViewerCountAsync(id);
            var count = await _redis.GetViewerCountAsync(id);
            await Clients.Group($"auction_{auctionId}").SendAsync("ViewerCountUpdated", count);
            _logger.LogInformation("Connection {ConnectionId} STOPPED VIEWING auction {AuctionId}. Still listening in group. New count: {Count}", 
                Context.ConnectionId, auctionId, count);
        }
    }

    // Client calls this when they leave the auction detail page
    public async Task LeaveAuction(string auctionId)
    {
        if (!int.TryParse(auctionId, out var id)) return;

        // 1. Remove from group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"auction_{auctionId}");

        // 2. Decrement global count if they were marked as a viewer
        if (_connectionViewers.TryGetValue(Context.ConnectionId, out var viewed) && viewed.TryRemove(id, out _))
        {
            await _redis.DecrementViewerCountAsync(id);
            var count = await _redis.GetViewerCountAsync(id);
            await Clients.Group($"auction_{auctionId}").SendAsync("ViewerCountUpdated", count);
            _logger.LogInformation("Connection {ConnectionId} LEFT viewer status for auction {AuctionId}. New count: {Count}", 
                Context.ConnectionId, auctionId, count);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Automatically decrement counts for any auctions this connection was "Joined" to
        if (_connectionViewers.TryRemove(Context.ConnectionId, out var viewedAuctions))
        {
            foreach (var id in viewedAuctions.Keys)
            {
                await _redis.DecrementViewerCountAsync(id);
                var count = await _redis.GetViewerCountAsync(id);
                await Clients.Group($"auction_{id}").SendAsync("ViewerCountUpdated", count);
                _logger.LogInformation("Connection {ConnectionId} DISCONNECTED. Automatically decremented count for auction {AuctionId}. New count: {Count}", 
                    Context.ConnectionId, id, count);
            }
        }

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
    Task BroadcastAuctionUpdated(int auctionId, object data);
    Task SendAddObject(int userid,object data);
}

public class AuctionHubService : IAuctionHubService
{
    private readonly IHubContext<AuctionHub> _hub;
    public AuctionHubService(IHubContext<AuctionHub> hub) => _hub = hub;

    private IClientProxy Room(int auctionId)
        => _hub.Clients.Group($"auction_{auctionId}");
    private IClientProxy user(int userid)
    =>_hub.Clients.User(userid.ToString());
        
    private IClientProxy Room1(int auctionId)=>_hub.Clients.Group($"auctiondetail_{auctionId}");

    public Task BroadcastBidPlaced(int auctionId, object data)
        => Room(auctionId).SendAsync("BidPlaced", data);

    public Task BroadcastAuctionStarted(int auctionId)
        => Room1(auctionId).SendAsync("AuctionStarted", new { auctionId });

    public Task BroadcastAuctionClosed(int auctionId, object data)
        => Room(auctionId).SendAsync("AuctionClosed", data);

    public Task BroadcastEndingSoon(int auctionId, int minutesRemaining)
        => Room(auctionId).SendAsync("AuctionEndingSoon", new { auctionId, minutesRemaining });
    public Task AuctionMessage(int auctionId, string message)
    => Room(auctionId).SendAsync("AuctionMessage", new { message });

    public Task BroadcastAuctionUpdated(int auctionId, object data)
        => Room(auctionId).SendAsync("AuctionUpdated", data);

    public Task BroadcastTimerTick(int auctionId, double secondsRemaining)
        => Room(auctionId).SendAsync("TimerTick", new { auctionId, secondsRemaining });

    public Task BroadcastProductDeleted(int auctionId)
        => Room(auctionId).SendAsync("AuctionAborted", new { auctionId, reason = "Product deleted by owner" });

    public Task BroadcastProductUnverified(int auctionId)
        => Room(auctionId).SendAsync("AuctionUnverified", new { auctionId, reason = "Product un-verified during live auction" });

    public Task SendAddObject(int userid, object data)
    =>user(userid).SendAsync("GetWatchListDetail",data);
}



public interface IUserHubService
{
    Task BroadCastCreatMessage(int userId, string message);
}

public class UserHubService(IHubContext<AuctionHub> _hub) : IUserHubService
{


    public IClientProxy client(string userId) => _hub.Clients.Client(userId);
    public Task BroadCastCreatMessage(int userId, string message) => client(userId.ToString()).SendAsync("GeneralAuction", "Message");
}
