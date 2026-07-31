using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class ReplyMappingProfile : Profile
    {
        public ReplyMappingProfile()
        {
            CreateMap<Reply, ReplyDto>()
                .ForMember(a => a.User, opt => opt.MapFrom(a => a.User));

            CreateMap<CreateReplyDto, Reply>();
        }
    }
}
