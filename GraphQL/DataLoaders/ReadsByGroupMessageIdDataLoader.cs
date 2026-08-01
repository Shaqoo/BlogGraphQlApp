using BlogGraphQlApp.Data;
using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class ReadsByGroupMessageIdDataLoader(IBatchScheduler batchScheduler, IDbContextFactory<AppDbContext> dbContextFactory, DataLoaderOptions options)
        : GroupedDataLoader<Guid, GroupMessageRead>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, GroupMessageRead>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var reads = await dbContext.GroupMessageReads
                .Where(r => keys.Contains(r.MessageId))
                .Include(r => r.User)
                .ToListAsync(cancellationToken);
            return reads.ToLookup(r => r.MessageId);
        }
    }
}
