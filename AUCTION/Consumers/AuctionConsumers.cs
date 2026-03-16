using AUCTION.Data.Entities;
using MassTransit;
using Messaging.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AUCTION.Consumers;


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
public class ProductUnverifiedConsumer : IConsumer<ProductUnverifiedFromService>
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

    public async Task Consume(ConsumeContext<ProductUnverifiedFromService> context)
    {
        var msg = context.Message;
        _logger.LogWarning(
            "Product {ProductId} was un-verified  — checking for live auctions",
            msg.productId);

       if(context.Message.productId != 0)
          await  _auctionService.ProductDeleteHandling(context.Message.productId);
        return ;
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
  
         if(context.Message.ProductId != 0)
          await  _auctionService.ProductDeleteHandling(context.Message.ProductId);
        return ;
    }
}
