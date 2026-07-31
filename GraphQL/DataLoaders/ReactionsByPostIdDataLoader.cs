using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class ReactionsByPostIdDataLoader(IBatchScheduler batchScheduler,IDbContextFactory<AppDbContext> dbContextFactory
     ,DataLoaderOptions options) : GroupedDataLoader<Guid, Reaction>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, Reaction>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var reactions = await dbContext.Reactions
            .Where(a => a.PostId != null && keys.Contains(a.PostId.Value))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

            var lookup = reactions
            .GroupBy(a => a.PostId!.Value)
            .SelectMany(a => a.Take(3))
            .ToLookup(a => a.PostId!.Value);

            return lookup;
        }
    }
}