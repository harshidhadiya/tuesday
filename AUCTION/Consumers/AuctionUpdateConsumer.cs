using AUCTION.Data.Dto.Response;
using AUCTION.Data.Entities;
using AUCTION.Hubs;
using AUCTION.Messages;
using MassTransit;

namespace AUCTION.Consumers
{
    public class AuctionUpdateConsumer(IAuctionHubService hub,ILogger<AuctionUpdateConsumer> logger) : IConsumer<AuctionUpdated>
    {
        public async Task Consume(ConsumeContext<AuctionUpdated> context)
        {

                logger.LogInformation("in AuctioUpdateConsumer Data Received SuccessFully");
            var auction=context.Message.auction;
            var users=auction.users;
            foreach (var item in users)
            {
                logger.LogInformation(auction.ProductName + "containing id"+ auction.ProductId +" has send data to the receiver" );
                await hub.SendAddObject(item,new{productName=auction.ProductName,productId=auction.ProductId,TimeRemainingSeconds=auction.EndDate - TimeHelper.Now(),
                 StartingPrice=auction.StartingPrice,
                 StartDate=auction.StartDate,
                 MinBidIncrement=auction.MinBidIncrement,
                 EndDate=auction.EndDate,
                 Id=auction.Id,
                 status=auction.Status,
                 productDescription=auction.productDescription
                });
            }
          
        }
    }
}