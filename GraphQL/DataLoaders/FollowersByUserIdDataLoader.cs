

using BlogGraphQlApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class FollowersByUserIdDataLoader(IDbContextFactory<AppDbContext> dbContextFactory,IBatchScheduler batchScheduler, DataLoaderOptions options) : BatchDataLoader<Guid, long>(batchScheduler, options)
    {
        protected override async Task<IReadOnlyDictionary<Guid, long>> LoadBatchAsync(
             IReadOnlyList<Guid> keys,
             CancellationToken cancellationToken)
        {
            var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var result = await db.UserFollows
                .Where(f => keys.Contains(f.FollowingId))
                .GroupBy(f => f.FollowingId)
                .Select(g => new { UserId = g.Key, FollowerCount = g.LongCount() })
                .ToListAsync(cancellationToken);

            // Ensure every key is in the dictionary, default to 0 if missing
            var dictionary = keys.ToDictionary(
                id => id,
                id => result.FirstOrDefault(r => r.UserId == id)?.FollowerCount ?? 0
            );

            return dictionary;
        }

    }
}
