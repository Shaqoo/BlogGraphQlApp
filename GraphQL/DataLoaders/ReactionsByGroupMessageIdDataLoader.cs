using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class ReactionsByGroupMessageIdDataLoader(IBatchScheduler batchScheduler, IDbContextFactory<AppDbContext> dbContextFactory, DataLoaderOptions options)
        : GroupedDataLoader<Guid, Reaction>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, Reaction>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var reactions = await dbContext.Reactions
                .Where(r => keys.Contains(r.GroupMessageId!.Value))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
            return reactions.ToLookup(r => r.GroupMessageId!.Value);
        }
    }
}
