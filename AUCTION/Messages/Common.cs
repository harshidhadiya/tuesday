using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;

namespace AUCTION.Messages
{
    public record AuctionUpdated(AuctionUpdateConsumerDto auction);
}