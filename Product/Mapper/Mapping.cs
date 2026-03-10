using AutoMapper;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Model;
using PRODUCT.Data.Dto.Response;

namespace PRODUCT.Mapper
{
    public class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<ProductCreate, ProductTable>().ForMember(x => x.product_name, opt => opt.MapFrom(x => x.name)).
            ForMember(X => X.Buy_Date, opt => opt.MapFrom(x => x.date)).
            ForMember(x => x.product_description, opt =>
            {
                opt.Condition(x => x.description != null);
                opt.MapFrom(x => x.description);
            }
            ).ForMember(x => x.isVerified, opt => opt.MapFrom(x => false)).ForMember(x => x.Id, opt => opt.Ignore())
            .ForMember(x => x.AuctionStartTime, opt => opt.Ignore())
            .ForMember(x => x.AuctionEndTime, opt => opt.Ignore());



            CreateMap<ProductTable, ProductDto>().ForMember(X => X.product_buy_date, opt => opt.MapFrom(x => x.Buy_Date)).ForMember(X => X.Name, opt => opt.MapFrom(x => x.product_name))
            .ForMember(x => x.verified, opt => opt.MapFrom(x => x.isVerified))
            .ForMember(x => x.description, opt => opt.MapFrom(x => x.product_description))
            .ForMember(x => x.id, opt => opt.MapFrom(x => x.Id));
        }
    }
}