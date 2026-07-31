using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class NotificationMappingProfile : Profile
    {
        public NotificationMappingProfile()
        {
            CreateMap<Notification, NotificationDto>()
                .ForMember(a => a.IsRead ,opt => opt.MapFrom(a => a.ReadAt != null));
        }
    }
}