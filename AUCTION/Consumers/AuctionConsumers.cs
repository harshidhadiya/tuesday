using MassTransit;
using Messaging.Contracts;

namespace AUCTION.Consumers;

/// <summary>
/// Listens for ProductVerified from your VerifyService.
/// You can use this to maintain a local cache of verified product IDs,
/// or simply use it for logging/alerting.
/// The auction creation endpoint already requires isVerified claim from JWT,
/// but this consumer gives you a server-side event trail.
/// </summary>
public class ProductVerifiedConsumer : IConsumer<ProductVerified>
{
    private readonly ILogger<ProductVerifiedConsumer> _logger;

    public ProductVerifiedConsumer(ILogger<ProductVerifiedConsumer> logger)
        => _logger = logger;

    public Task Consume(ConsumeContext<ProductVerified> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Product {ProductId} has been verified — auctions can now be created for it",
            msg.ProductId);

        // Extension point: store a local set of verified product IDs in Redis
        // so CreateAuction can validate ProductId without calling VerifyService over HTTP.
        // e.g. await _redis.SetAsync($"verified_product:{msg.ProductId}", "1");

        return Task.CompletedTask;
    }
}

/// <summary>
/// Listens for ProductUnverified from your VerifyService.
/// When a product is un-verified, we should close any live auctions for it.
/// </summary>
public class ProductUnverifiedConsumer : IConsumer<ProductUnverified>
{
    private readonly Data.Repositories.Interfaces.IAuctionRepository _auctionRepo;
    private readonly Services.Interfaces.IAuctionService             _auctionService;
    private readonly ILogger<ProductUnverifiedConsumer>              _logger;

    public ProductUnverifiedConsumer(
        Data.Repositories.Interfaces.IAuctionRepository auctionRepo,
        Services.Interfaces.IAuctionService auctionService,
        ILogger<ProductUnverifiedConsumer> logger)
    {
        _auctionRepo    = auctionRepo;
        _auctionService = auctionService;
        _logger         = logger;
    }

    public async Task Consume(ConsumeContext<ProductUnverified> context)
    {
        var msg = context.Message;
        _logger.LogWarning(
            "Product {ProductId} was un-verified by admin {AdminId} — checking for live auctions",
            msg.ProductId, msg.AdminId);

        // Find any live auctions for this product and force-close them
        var filter = new Data.Dto.Request.AuctionFilterRequest
        {
            Status   = Data.Entities.AuctionStatus.Live,
            PageSize = 100
        };

        var (auctions, _) = await _auctionRepo.GetAllAsync(filter);
        var affected       = auctions.Where(a => a.ProductId == msg.ProductId).ToList();

        foreach (var auction in affected)
        {
            _logger.LogWarning(
                "Force-closing auction {AuctionId} because product {ProductId} was un-verified",
                auction.Id, msg.ProductId);

            await _auctionService.CloseAuctionAsync(auction.Id);
        }
    }
}

/// <summary>
/// Listens for ProductDeleted from your ProductService.
/// Cancels all upcoming auctions and closes any live ones for the deleted product.
/// </summary>
public class ProductDeletedConsumer : IConsumer<ProductDeleted>
{
    private readonly Data.Repositories.Interfaces.IAuctionRepository _auctionRepo;
    private readonly Services.Interfaces.IAuctionService             _auctionService;
    private readonly ILogger<ProductDeletedConsumer>                 _logger;

    public ProductDeletedConsumer(
        Data.Repositories.Interfaces.IAuctionRepository auctionRepo,
        Services.Interfaces.IAuctionService auctionService,
        ILogger<ProductDeletedConsumer> logger)
    {
        _auctionRepo    = auctionRepo;
        _auctionService = auctionService;
        _logger         = logger;
    }

    public async Task Consume(ConsumeContext<ProductDeleted> context)
    {
        var msg = context.Message;
        _logger.LogWarning(
            "Product {ProductId} deleted by user {UserId} — cancelling related auctions",
            msg.ProductId, msg.DeletedByUserId);

        // Close live auctions
        var liveFilter = new Data.Dto.Request.AuctionFilterRequest
        {
            Status = Data.Entities.AuctionStatus.Live, PageSize = 100
        };
        var (liveAuctions, _) = await _auctionRepo.GetAllAsync(liveFilter);
        foreach (var a in liveAuctions.Where(a => a.ProductId == msg.ProductId))
            await _auctionService.CloseAuctionAsync(a.Id);

        // Cancel upcoming auctions (admin userId = DeletedByUserId bypass)
        var upcomingFilter = new Data.Dto.Request.AuctionFilterRequest
        {
            Status = Data.Entities.AuctionStatus.Upcoming, PageSize = 100
        };
        var (upcomingAuctions, _) = await _auctionRepo.GetAllAsync(upcomingFilter);
        foreach (var a in upcomingAuctions.Where(a => a.ProductId == msg.ProductId))
        {
            a.Status    = Data.Entities.AuctionStatus.Cancelled;
            a.UpdatedAt = DateTime.UtcNow;
            await _auctionRepo.UpdateAsync(a);
        }
        await _auctionRepo.SaveChangesAsync();
    }
}
