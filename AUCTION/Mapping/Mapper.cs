using AUCTION.Data.Dto.Request;
using AUCTION.Data.Entities;
using AutoMapper;
using Messaging.Contracts;

namespace AUCTION.Mapping
{
    public class Mapper : Profile
    {
        public Mapper()
        {
            CreateMap<AuctionCreatedFromVerifyService, Auction>().ForMember(x => x.Status, opt => opt.MapFrom(x => AuctionStatus.Upcoming))
            .ForMember(x => x.CreatedByUserId, opt => opt.MapFrom(x => x.userId)).ForMember(x => x.CreatedByVerifyId, opt => opt.MapFrom(y => y.verifierId));
            CreateMap<Auction, AuctionUpdateConsumerDto>()
                .ForMember(dest => dest.users,
                    opt => opt.MapFrom(src => src.Watchlists.Select(w => w.UserId).ToList())).ForMember(x=>x.productDescription,opt=>opt.MapFrom(y=>y.Description));
        }
    }
}