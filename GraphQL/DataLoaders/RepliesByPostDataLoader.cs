using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class RepliesByPostDataLoader(
     IBatchScheduler batchScheduler,
     DataLoaderOptions options,
     IDbContextFactory<AppDbContext> dbContextFactory)
     : GroupedDataLoader<Guid, Reply>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, Reply>> LoadGroupedBatchAsync(
            IReadOnlyList<Guid> keys,
            CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var replies = await dbContext.Replies
                .Where(r => r.PostId != null && keys.Contains(r.PostId.Value))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);

            var lookup = replies
                .GroupBy(r => r.PostId!.Value)
                .SelectMany(g => g.Take(3))
                .ToLookup(r => r.PostId!.Value);

            return lookup;
        }
    }

}