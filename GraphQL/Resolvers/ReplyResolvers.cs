using AutoMapper;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.DataLoaders;

namespace BlogGraphQlApp.GraphQL.Resolvers
{
    public class ReplyResolvers
    {
#pragma warning disable CC0091
        public async Task<IEnumerable<ReplyDto>> GetNestedRepliesAsync(
#pragma warning restore CC0091
            [Parent] ReplyDto reply,
            NestedRepliesByReplyIdDataLoader dataLoader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var replies = await dataLoader.LoadAsync(reply.Id, cancellationToken);
            return mapper.Map<IEnumerable<ReplyDto>>(replies);
        }

        public async Task<IEnumerable<ReactionDto>> GetReactionsAsync(
            [Parent] ReplyDto reply,
            ReactionsByReplyIdDataLoader dataLoader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var reactions = await dataLoader.LoadAsync(reply.Id, cancellationToken);
            return mapper.Map<IEnumerable<ReactionDto>>(reactions);
        }

        public async Task<bool> HasReactedToReplyAsync(
            [Parent] ReplyDto reply,
            [Service] IReactionService reactionService)
        {
            var hasReacted = await reactionService.HasUserReactedToReplyAsync(reply.Id);
            return hasReacted.Data;
        }

        public async Task<string> GetUserReactionAsync(
            [Parent] ReplyDto reply,
            [Service] IReactionService reactionService)
        {
            var hasReacted = await reactionService.GetUserReactionToReplyAsync(reply.Id);
            return hasReacted.Data ?? string.Empty;
        }
    }
}