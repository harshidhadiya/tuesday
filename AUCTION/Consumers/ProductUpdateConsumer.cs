using AUCTION.Data.Repositories.Interfaces;
using MassTransit;
using Messaging.Contracts;

namespace AUCTION.Consumers
{
    public class ProductUpdateConsumer(IAuctionRepository repo) : IConsumer<ProductUpdateForVerification>
    {
        public async Task Consume(ConsumeContext<ProductUpdateForVerification> context)
        {
            var auction = await repo.GetbyProductId(context.Message.ProductId);
            if (auction == null)
                return;

            var product = context.Message;
            if (product.name != null)
                auction.ProductName = product.name;
            if (product.descripiton != null)
                auction.Description = product.descripiton;
            await repo.UpdateAsync(auction);

        }
    }
}