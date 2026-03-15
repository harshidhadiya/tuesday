using AutoMapper;
using USER.Model;
using USER.Data.Dto;
using Microsoft.AspNetCore.Identity;
using Name;
using USER.Data.Dto.Response;
using Microsoft.AspNetCore.Components.Web;

namespace USER.MAPPER
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            Tokenget token = new Tokenget();
            var hash = new PasswordHasher<object>();

            CreateMap<UserCreateDto, UserTable>()
                .ForMember(dest => dest.HashPassword, opt => opt.MapFrom(src => hash.HashPassword(new object(), src.Password)))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()).ReverseMap().ForMember(x=>x.file,opt=>opt.Ignore());
            CreateMap<UserTable,UserDetail>().ForMember(x=>x.imageUrl,opt=>opt.MapFrom(x=>x.ProfilePicture)).ForMember(x=>x.token,opt=>opt.MapFrom(data=>token.getToken(data.Name,data.Role.ToUpperInvariant(),data.Id.ToString())));
            CreateMap<UserTable, UserCreateDto>();
            CreateMap<UserTable, SignupResponceDto>().ForMember(x => x.token, opt => opt.MapFrom(x => token.getToken(x.Name, x.Role, x.Id.ToString()))).ForMember(x => x.requestobj, opt => opt.Ignore());

        }
    }
}
