using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class ReelMappingProfile : Profile
    {
        public ReelMappingProfile()
        {
            CreateMap<Reel, ReelDto>();
        }
    }
}