using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Services.Interfaces;
using MassTransit;
using Messaging.Contracts;

namespace AUCTION.Consumers;


public class ProductVerifiedConsumer(IAuctionRepository _repo) : IConsumer<ProductVerified>
{
    public async Task Consume(ConsumeContext<ProductVerified> context)
    {
        var auction=await _repo.GetbyProductId(context.Message.ProductId);
        if(auction==null)
        return ;
        auction.Status=Data.Entities.AuctionStatus.Verified;
        await _repo.UpdateAsync(auction);
        await _repo.SaveChangesAsync();
    }
}


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
            "Product {ProductId} was un-verified  — checking for live auctions",
            msg.ProductId);

       if(context.Message.ProductId != 0 )
          await  _auctionService.ProductUnverifyHandling(context.Message.ProductId,context.Message.AdminId);
        return ;
    }

   
}

