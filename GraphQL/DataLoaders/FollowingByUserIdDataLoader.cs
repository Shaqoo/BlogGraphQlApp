

using BlogGraphQlApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class FollowingByUserIdDataLoader(IDbContextFactory<AppDbContext> dbContextFactory, IBatchScheduler batchScheduler, DataLoaderOptions options) : BatchDataLoader<Guid, long>(batchScheduler, options)
    {
        protected override async Task<IReadOnlyDictionary<Guid, long>> LoadBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            var db =  await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await db.UserFollows
                .Where(f => keys.Contains(f.FollowerId))
                .GroupBy(f => f.FollowerId)
                .Select(g => new { UserId = g.Key, FollowingCount = g.LongCount() })
                .ToListAsync(cancellationToken);

            var dictionary = keys.ToDictionary(
                id => id,
                id => result.FirstOrDefault(r => r.UserId == id)?.FollowingCount ?? 0
            );

            return dictionary;
        }
    }
}
