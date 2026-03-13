using AutoMapper;
using PRODUCT.Data.Dto.Request;
using PRODUCT.Model;
using PRODUCT.Data.Dto.Response;
using AutoMapper.Internal;

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
            .ForMember(x => x.AuctionEndTime, opt => opt.Ignore()).ForMember(x => x.images,opt=>opt.Ignore());


            CreateMap<ProductTable, ProductDto>()
            .ForMember(dest => dest.product_buy_date, opt => opt.MapFrom(src => src.Buy_Date))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.product_name))
            .ForMember(dest => dest.verified, opt => opt.MapFrom(src => src.isVerified))
            .ForMember(dest => dest.description, opt => opt.MapFrom(src => src.product_description))
            .ForMember(dest => dest.id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.images, opt => opt.MapFrom(src =>
                 src.images != null && src.images.Any()
                 ? src.images.Select(i => new imageData
                 {
                    id = i.Id,
                    imageUrl = i.Image_URL
                 }).ToList()
                   : null
               ));
        }
    }
}