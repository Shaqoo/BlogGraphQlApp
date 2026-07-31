using System.Security.Claims;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using HotChocolate.AspNetCore;
using FluentValidation;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class UserMutation
    {
        [Authorize]
        public async Task<ApiResponse<UserDto>> UpdateMyBioAsync(
            UpdateUserBioDto input,
            [Service] IUserService userService,
            [Service] IValidator<UpdateUserBioDto> validator,
            ClaimsPrincipal claimsPrincipal)
        {
            var validationResult = await validator.ValidateAsync(input);
            if (!validationResult.IsValid)
            {
                return ApiResponse<UserDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }
            var userId = GetUserId(claimsPrincipal);
            return await userService.UpdateUserBioAsync(userId, input);
        }

        [Authorize]
        public async Task<ApiResponse<UserDto>> UpdateMyUsernameAsync(
            UpdateUserUsernameDto input,
            [Service] IUserService userService,
            [Service] IValidator<UpdateUserUsernameDto> validator,
            ClaimsPrincipal claimsPrincipal)
        {
            var validationResult = await validator.ValidateAsync(input);
            if (!validationResult.IsValid)
            {
                return ApiResponse<UserDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }
            var userId = GetUserId(claimsPrincipal);
            return await userService.UpdateUserUsernameAsync(userId, input);
        }

        [Authorize]
        public async Task<ApiResponse<UserDto>> UpdateMyProfilePictureAsync(
            IFile profilePicture,
            [Service] IUserService userService,
            [Service] IValidator<UpdateUserProfilePictureDto> validator,
            ClaimsPrincipal claimsPrincipal)
        {
            var input = new UpdateUserProfilePictureDto { ProfilePicture = profilePicture };
            var validationResult = await validator.ValidateAsync(input);
            if (!validationResult.IsValid)
            {
                return ApiResponse<UserDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }
            var userId = GetUserId(claimsPrincipal);
            return await userService.UpdateUserProfilePictureAsync(userId, input);
        }

        [Authorize]
        public async Task<ApiResponse<UserDto>> UpdateMyCoverPhotoAsync(
            UpdateUserCoverUrlDto input,
            [Service] IUserService userService,
            [Service] IValidator<UpdateUserCoverUrlDto> validator,
            ClaimsPrincipal claimsPrincipal)
        {
            var validationResult = await validator.ValidateAsync(input);
            if (!validationResult.IsValid)
            {
                return ApiResponse<UserDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }
            var userId = GetUserId(claimsPrincipal);
            return await userService.UpdateUserCoverPhotoAsync(userId, input);
        }

        [Authorize]
        public async Task<ApiResponse<UserDto>> UpdateMyBackgroundIdentifierAsync(
            UpdateUserBackgroundIdentifierDto input,
            [Service] IUserService userService,
            [Service] IValidator<UpdateUserBackgroundIdentifierDto> validator,
            ClaimsPrincipal claimsPrincipal)
        {
            var validationResult = await validator.ValidateAsync(input);
            if (!validationResult.IsValid)
            {
                return ApiResponse<UserDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());
            }
            var userId = GetUserId(claimsPrincipal);
            return await userService.UpdateUserBackgroundIdentifierAsync(userId, input);
        }

        private static Guid GetUserId(ClaimsPrincipal claimsPrincipal)
        {
            var userIdString = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            return !Guid.TryParse(userIdString, out var userId)
                ? throw new GraphQLRequestException("User not authenticated.")
                : userId;
        }
    }
}