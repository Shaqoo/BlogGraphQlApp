using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class NestedRepliesByReplyIdDataLoader(
        IBatchScheduler batchScheduler,
        IDbContextFactory<AppDbContext> dbContextFactory,
        DataLoaderOptions? options = null)
        : GroupedDataLoader<Guid, Reply>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, Reply>> LoadGroupedBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var replies = await dbContext.Replies
                .Where(r => r.ParentReplyId != null && keys.Contains(r.ParentReplyId.Value))
                .ToListAsync(cancellationToken);

            return replies.ToLookup(r => r.ParentReplyId!.Value);
        }
    }
}