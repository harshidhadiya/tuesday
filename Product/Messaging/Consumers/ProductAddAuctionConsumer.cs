using MassTransit;
using Messaging.Contracts;
using PRODUCT.Repository;

namespace PRODUCT.Messaging.Consumers
{
    public class ProductAddAuctionConsumer(Irepository _repo,ILogger<ProductAddAuctionConsumer> logger) : IConsumer<ProductAddAuctionDate>
    {
        public async Task Consume(ConsumeContext<ProductAddAuctionDate> context)
        {
          var product=await _repo.getByIdProduct(context.Message.productId);
          if (product==null)
          {
            return ;
          }
          product.AuctionStartTime=context.Message.StartDate;
          product.AuctionEndTime=context.Message.EndDate;
          logger.LogInformation("auction created success full and this are the date of the auctiosn okay"+context.Message.StartDate+"this is the end date"+context.Message.EndDate);
          await _repo.Update(product);
        }
    }
}