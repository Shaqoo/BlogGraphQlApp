using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.GraphQL.Events;
using BlogGraphQlApp.GraphQL.Subscriptions;
using BlogGraphQlApp.Models;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class ReactionMutation
    {
        public record CreateReactionInput(string Reaction, Guid? PostId, Guid? ReelId, Guid? MessageId, Guid? replyId);

        [Authorize]
        public async Task<ApiResponse<DTOs.ReactionDto>> CreateReactionAsync(
            CreateReactionInput input,
            [Service] ITopicEventSender sender,
            [Service] IReactionService reactionService,
            [Service] IAuthService authService)
        {
            var currentUser = await authService.GetCurrentUserAsync();
            var createDto = new CreateReactionDto
            {
                Emoji = input.Reaction,
                PostId = input.PostId,
                ReelId = input.ReelId,
                MessageId = input.MessageId,
                UserId = currentUser.Data!.Id,
                ReplyId = input.replyId
            };
            var response = await reactionService.CreateReactionAsync(createDto);
            var payload = new ReactionPayload
            {
                Reaction = input.Reaction,
                PostId = input.PostId,
                ReelId = input.ReelId,
                MessageId = input.MessageId,
                UserId = currentUser.Data!.Id,
                FullName = currentUser.Data!.FullName,
            };
            if (input.PostId != null)
                await sender.SendAsync($"{nameof(ReactionSubscription.OnPostReactionAdded)}_{input.PostId}", payload);

            if (input.ReelId != null)
                await sender.SendAsync($"{nameof(ReactionSubscription.OnReelReactionAdded)}_{input.ReelId}", payload);

            if (input.MessageId != null)
                await sender.SendAsync($"{nameof(ReactionSubscription.OnMessageReactionAdded)}_{input.MessageId}", payload);
            return response;

        }

        [Authorize]
        public async Task<ApiResponse<bool>> DeleteReactionAsync(Guid id, [Service] IReactionService reactionService)
        {
            return await reactionService.DeleteReactionAsync(id);
        }
    }
}