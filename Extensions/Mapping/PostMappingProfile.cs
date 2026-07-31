using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class PostMappingProfile : Profile
    {
        public PostMappingProfile()
        {
            CreateMap<Post, PostDto>();
        }
    }
}