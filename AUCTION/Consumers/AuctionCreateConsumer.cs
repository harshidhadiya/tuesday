using AUCTION.Data;
using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;
using AUCTION.Data.Repositories.Interfaces;
using AUCTION.Hubs;
using AutoMapper;
using MassTransit;
using Messaging.Contracts;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;

namespace AUCTION.Consumers
{
    public class AuctionCreateConsumer : IConsumer<AuctionCreatedFromVerifyService>
{
    private readonly IAuctionRepository _repo;
    private readonly IMapper mapper;
    private readonly IUserHubService userHub;
    private readonly IPublishEndpoint publish;
    private readonly ILogger<AuctionCreateConsumer> logger;
    private readonly AuctionDbContext ctx;

    public AuctionCreateConsumer(
        IAuctionRepository repo,
        IMapper mapper,
        IUserHubService userHub,
        IPublishEndpoint publish,
        ILogger<AuctionCreateConsumer> logger,
        AuctionDbContext ctx)
    {
        this._repo = repo;
        this.mapper = mapper;
        this.userHub = userHub;
        this.publish = publish;
        this.logger = logger;
        this.ctx = ctx;
    }
       
        public async Task Consume(ConsumeContext<AuctionCreatedFromVerifyService> context)
        {
            var exist=await _repo.GetbyProductId(context.Message.ProductId);
            logger.LogInformation("entered here");
            if(exist!=null ){

                // if the product going to unverify by the admin then for this handling we are doin this
                if(exist.CreatedByUserId!=context.Message.userId)
                return; 
                exist.ProductId=context.Message.ProductId;
                exist.ProductName=context.Message.ProductName;
                exist.EndDate=context.Message.EndDate;
                exist.StartDate=context.Message.StartDate;
                exist.MinBidIncrement=context.Message.MinBidIncrement;
                exist.Description=context.Message.Description;
                exist.CreatedByVerifyId=context.Message.verifierId;
                exist.CreatedByUserId=context.Message.userId;
                exist.ReservePrice=context.Message.ReservePrice;
                exist.StartingPrice=context.Message.StartingPrice;
                exist.Status=AuctionStatus.Upcoming;
                ctx.Bids.RemoveRange(exist.Bids);
                exist.Bids.Clear();
                await _repo.UpdateAsync(exist);
                await _repo.SaveChangesAsync();
                await publish.Publish(new ProductAddAuctionDate(productId:context.Message.ProductId,StartDate:context.Message.StartDate,EndDate:context.Message.EndDate));
                return ;
            }
            var data=mapper.Map<Auction>(context.Message);
            await _repo.AddAsync(data);
            await _repo.SaveChangesAsync();
            logger.LogInformation("entered here2");
            await userHub.BroadCastCreatMessage(context.Message.userId,"UserCreated SuccessFully");
            await publish.Publish(new ProductAddAuctionDate(productId:context.Message.ProductId,StartDate:context.Message.StartDate,EndDate:context.Message.EndDate));

        }
    }
}
