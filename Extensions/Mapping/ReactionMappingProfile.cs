using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class ReactionMappingProfile : Profile
    {
        public ReactionMappingProfile()
        {
            CreateMap<Reaction, ReactionDto>()
                .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User));

        }
    }
}
