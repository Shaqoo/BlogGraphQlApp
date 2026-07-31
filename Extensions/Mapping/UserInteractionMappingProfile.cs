using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class UserInteractionProfile : Profile
    {
        public UserInteractionProfile()
        {
            CreateMap<CreateUserInteractionDto, UserInteraction>();
            CreateMap<UserInteraction,UserInteractionDto>();
        }
    }

}
