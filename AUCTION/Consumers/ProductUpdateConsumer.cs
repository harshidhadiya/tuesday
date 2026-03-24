using AUCTION.Data.Repositories.Interfaces;
using MassTransit;
using Messaging.Contracts;

namespace AUCTION.Consumers
{
    public class ProductUpdateConsumer(IAuctionRepository repo,ILogger<ProductUpdateConsumer> logger) : IConsumer<ProductUpdateForVerification>
    {
        public async Task Consume(ConsumeContext<ProductUpdateForVerification> context)
        {
        logger.LogInformation("entered here"+context.Message.ProductId);
            var auction = await repo.GetbyProductId(context.Message.ProductId);
            if (auction == null)
                return;
logger.LogInformation("entered heresdfsefsfs"+context.Message.descripiton);
         
            if (context.Message.name != null)
                auction.ProductName = context.Message.name;
            if (context.Message.descripiton != null)
                auction.Description = context.Message.descripiton;
            await repo.UpdateAsync(auction);
            await repo.SaveChangesAsync();

        }
    }
}
