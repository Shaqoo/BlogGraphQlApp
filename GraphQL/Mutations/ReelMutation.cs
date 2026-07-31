using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class ReelMutation
    {
        public record CreateReelInput(string Title, IFile Video);
        public record UpdateReelInput(Guid Id, string Title);

        [Authorize]
        public async Task<ApiResponse<ReelDto>> CreateReelAsync(
            CreateReelInput input,
            [Service] IReelService reelService,
            [Service] IAuthService authService)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            var createDto = new CreateReelDto
            {
                Title = input.Title,
                Video = input.Video,
                UserId = currentUser.Data!.Id
            };
            return await reelService.CreateReelAsync(createDto);
        }

        [Authorize]
        public async Task<ApiResponse<ReelDto>> UpdateReelAsync(UpdateReelInput input, [Service] IReelService reelService)
        {
            return await reelService.UpdateReelAsync(input.Id, new UpdateReelDto { Title = input.Title });
        }

        [Authorize]
        public async Task<ApiResponse<bool>> DeleteReelAsync(Guid id, [Service] IReelService reelService)
            => await reelService.DeleteReelAsync(id);
    }
}