using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Services.Interfaces;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class ReplyQueries
    {
        public async Task<ApiResponse<ReplyDto?>> GetReplyByIdAsync(Guid id, [Service] IReplyService replyService)
            => await replyService.GetReplyByIdAsync(id);

        [UsePaging]
        public async Task<IQueryable<ReplyDto>> GetTopLevelRepliesAsync([Service]IReplyService replyService,
            Guid postId)
        {
            return await replyService.GetTopLevelRepliesAsync(postId);
        }

        [UsePaging]
        public async Task<IQueryable<ReplyDto>> GetNestedRepliesAsync([Service] IReplyService replyService, 
            Guid parentReplyId)
        {
            return await replyService.GetNestedRepliesAsync(parentReplyId);
        }
    }
}