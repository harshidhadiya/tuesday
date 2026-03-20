using AutoMapper;
using RabbitMQ.Client;
using VERIFY.DTOs.Responses;
using VERIFY.Model;

namespace VERIFY.Mapper
{
    public class Mappin:Profile
    {
        public Mappin()
        {
            CreateMap<VerifyProductTable,FilterResponse>().ForMember(x=>x.Description,opt=>opt.MapFrom(x=>x.Product_description))
            .ForMember(x=>x.VerifyDescription,opt=>opt.MapFrom(x=>x.Description))
            .ForMember(x=>x.sellerId,opt=>opt.MapFrom(x=>x.SellerId))
            .ForMember(x=>x.IsVerified,opt=>opt.MapFrom(x=>x.isProductVerified));
        }
    }
}