using BlogGraphQlApp.Data;
using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class MentionsByGroupMessageIdDataLoader(IBatchScheduler batchScheduler, IDbContextFactory<AppDbContext> dbContextFactory, DataLoaderOptions options)
        : GroupedDataLoader<Guid, GroupMessageMention>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, GroupMessageMention>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var mentions = await dbContext.GroupMessageMentions
                .Where(m => keys.Contains(m.MessageId))
                .Include(m => m.User)
                .ToListAsync(cancellationToken);
            return mentions.ToLookup(m => m.MessageId);
        }
    }
}
