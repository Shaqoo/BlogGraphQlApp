using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Config
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            // Other mappings would go here...

            // Notification Mapping
            CreateMap<Notification, NotificationDto>()
                // This tells ProjectTo how to calculate IsRead in the SQL query
                .ForMember(dest => dest.IsRead, opt => opt.MapFrom(src => src.ReadAt != null));

            // Ensure other DTO mappings are also present
            CreateMap<User, UserDto>();
        }
    }
}
