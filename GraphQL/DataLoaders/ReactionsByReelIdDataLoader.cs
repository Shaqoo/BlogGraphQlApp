using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class ReactionsByReelIdDataLoader(
        IBatchScheduler batchScheduler,
        IDbContextFactory<AppDbContext> dbContextFactory,
        DataLoaderOptions? options = null)
        : GroupedDataLoader<Guid, Reaction>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, Reaction>> LoadGroupedBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var reactions = await dbContext.Reactions
                .Where(r => r.ReelId != null && keys.Contains(r.ReelId.Value))
                .ToListAsync(cancellationToken);

            return reactions.ToLookup(r => r.ReelId!.Value);
        }
    }
}