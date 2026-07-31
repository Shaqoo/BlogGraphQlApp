using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class MessagingMappingProfile : Profile
    {
        public MessagingMappingProfile()
        {
            CreateMap<Message, MessageDto>()
                 .ForMember(dest => dest.Sender, opt => opt.MapFrom(src => src.Sender))
                .ForMember(dest => dest.Reactions, opt => opt.MapFrom(src => src.Reactions));
            
        }
    }
}
