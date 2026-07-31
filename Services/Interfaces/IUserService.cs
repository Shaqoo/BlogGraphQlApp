﻿﻿﻿using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.Core.Interfaces
{
    public interface IUserService
    {
        Task<ApiResponse<UserDto>> CreateUserAsync(CreateUserDto createUserDto);
        Task<ApiResponse<UserDto?>> GetUserByIdAsync(Guid id);
        Task<ApiResponse<UserDto?>> GetUserByUsernameAsync(string username);
        Task<ApiResponse<bool>> CheckIfEmailExists(string email);
        Task<ApiResponse<bool>> CheckIfUsernameExists(string username);
        Task<ApiResponse<IEnumerable<UserDto>>> GetAllUsersAsync();
        Task<ApiResponse<bool>> DeleteUserAsync(Guid id);
        Task<ApiResponse<UserDto>> UpdateUserBioAsync(Guid id, UpdateUserBioDto dto);
        Task<ApiResponse<UserDto>> UpdateUserUsernameAsync(Guid id, UpdateUserUsernameDto dto);
        Task<ApiResponse<UserDto>> UpdateUserProfilePictureAsync(Guid id, UpdateUserProfilePictureDto dto);
        Task<ApiResponse<UserDto>> UpdateUserCoverPhotoAsync(Guid id, UpdateUserCoverUrlDto dto);
        Task<ApiResponse<UserDto>> UpdateUserBackgroundIdentifierAsync(Guid id, UpdateUserBackgroundIdentifierDto dto); 
        Task<ApiResponse<IQueryable<UserDto>>> SearchUsersAsync(string searchTerm);
    }
}