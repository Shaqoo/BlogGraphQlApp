using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class ReactionQueries
    {
        [UsePaging]
        public Task<IQueryable<ReactionDto>> GetReactionsByReplyIdAsync(
            Guid replyId,
            [Service] IReactionService reactionService)
        {
            return reactionService.GetReactionsByReplyIdAsync(replyId);
        }

        [UsePaging]
        public Task<IQueryable<ReactionDto>> GetReactionsByPostIdAsync(
            Guid postId,
            [Service] IReactionService reactionService)
        {
            return reactionService.GetReactionsByPostIdAsync(postId);
        }
    }

}
