using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class UserMappingProfile : Profile
    {
        public UserMappingProfile()
        {
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.PostsCount, opt => opt.Ignore())
                .ForMember(dest => dest.ReelsCount, opt => opt.Ignore());
            
            CreateMap<CreateUserDto, User>();
            CreateMap<UpdateUserBioDto, User>();
            CreateMap<UpdateUserUsernameDto, User>();
            CreateMap<UpdateUserProfilePictureDto, User>();
            CreateMap<UpdateUserCoverUrlDto, User>();
            CreateMap<UpdateUserBackgroundIdentifierDto, User>();
        }
    }
}