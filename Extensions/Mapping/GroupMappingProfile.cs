using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class GroupMappingProfile : Profile
    {
        public GroupMappingProfile()
        {
            CreateMap<GroupMessage, GroupMessageDto>()
                .ForMember(d => d.SenderName, o => o.MapFrom(s => s.Sender != null ? s.Sender.FullName : string.Empty))
                .ForMember(d => d.SenderAvatar, o => o.MapFrom(s => s.Sender != null ? s.Sender.ProfilePictureUrl : null))
                .ForMember(d => d.Mentions, o => o.MapFrom(s => s.Mentions))
                .ForMember(d => d.Reactions, o => o.MapFrom(s => s.Reactions));

            CreateMap<GroupMessageMention, GroupMentionDto>()
                .ForMember(d => d.Username, o => o.MapFrom(s => s.User != null ? s.User.Username : string.Empty))
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty));

            CreateMap<GroupJoinRequest, GroupJoinRequestDto>()
                .ForMember(d => d.Username, o => o.MapFrom(s => s.User != null ? s.User.Username : string.Empty))
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.Avatar, o => o.MapFrom(s => s.User != null ? s.User.ProfilePictureUrl : null));

            CreateMap<GroupVideoCallParticipant, GroupCallParticipantDto>()
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.Avatar, o => o.MapFrom(s => s.User != null ? s.User.ProfilePictureUrl : null));
        }
    }
}
