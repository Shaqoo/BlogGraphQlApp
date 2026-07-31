using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Services.Interfaces;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class ReplyMutation
    {
        public record CreateReplyInput(string Content, Guid? PostId, Guid? ReelId, Guid? ParentReplyId);
        public record UpdateReplyInput(Guid Id, string Content);

        [Authorize]
        public async Task<ApiResponse<ReplyDto>> CreateReplyAsync(
            CreateReplyInput input,
            [Service] IReplyService replyService,
            [Service] IAuthService authService)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            var createDto = new CreateReplyDto
            {
                Content = input.Content,
                PostId = input.PostId,
                ReelId = input.ReelId,
                ParentReplyId = input.ParentReplyId,
                UserId = currentUser.Data!.Id
            };
            return await replyService.CreateReplyAsync(createDto);
        }

        [Authorize]
        public async Task<ApiResponse<ReplyDto>> UpdateReplyAsync(UpdateReplyInput input, [Service] IReplyService replyService)
            => await replyService.UpdateReplyAsync(input.Id, new UpdateReplyDto { Content = input.Content });

        [Authorize]
        public async Task<ApiResponse<bool>> DeleteReplyAsync(Guid id, [Service] IReplyService replyService)
            => await replyService.DeleteReplyAsync(id);
    }
}