using BlogGraphQlApp.Data;
using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class GroupMessageByIdDataLoader(IBatchScheduler batchScheduler, IDbContextFactory<AppDbContext> dbContextFactory, DataLoaderOptions options)
        : GroupedDataLoader<Guid, GroupMessage>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, GroupMessage>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var messages = await dbContext.GroupMessages
                .Where(m => keys.Contains(m.Id))
                .Include(m => m.Sender)
                .ToListAsync(cancellationToken);
            return messages.ToLookup(m => m.Id);
        }
    }
}
