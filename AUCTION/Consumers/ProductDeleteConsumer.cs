using MassTransit;
using Messaging.Contracts;

namespace AUCTION.Consumers
{
    public class ProductDeleteConsumer:IConsumer<ProductDeleted>
{
    private readonly Data.Repositories.Interfaces.IAuctionRepository _auctionRepo;
    private readonly Services.Interfaces.IAuctionService _auctionService;
     private readonly ILogger<ProductDeleteConsumer> _logger;

    public ProductDeleteConsumer(
        Data.Repositories.Interfaces.IAuctionRepository auctionRepo,
        Services.Interfaces.IAuctionService auctionService,
        ILogger<ProductDeleteConsumer> logger)
    {
        _auctionRepo    = auctionRepo;
        _auctionService = auctionService;
        _logger         = logger;
    }
//   consumer is to used for the deleted Product To Stop The Live actions
     public async Task Consume(ConsumeContext<ProductDeleted> context)
    {
        var msg = context.Message;
        _logger.LogWarning(
            "Product {ProductId} deleted by user {UserId} — cancelling related auctions",
            msg.ProductId, msg.DeletedByUserId);
  
         if(context.Message.ProductId != 0)
          await  _auctionService.forceFullyclosed(context.Message.ProductId,context.Message.DeletedByUserId);
        return ;
    }
}
}