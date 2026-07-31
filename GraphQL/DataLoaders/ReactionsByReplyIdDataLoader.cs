using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class ReactionsByReplyIdDataLoader(IDbContextFactory<AppDbContext> dbContextFactory,
        IBatchScheduler batchScheduler, DataLoaderOptions options) 
        : GroupedDataLoader<Guid, Reaction>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, Reaction>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var reactions = await dbContext.Reactions
                .Where(r => r.ReplyId != null && keys.Contains(r.ReplyId.Value))
                .ToListAsync(cancellationToken);

            return reactions.ToLookup(r => r.ReplyId!.Value);
        }
    }
}
