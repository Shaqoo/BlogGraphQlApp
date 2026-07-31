using BlogGraphQlApp.Data;
using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class NotificationByUserIdDataLoader : GroupedDataLoader<Guid, Notification>
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        public NotificationByUserIdDataLoader(IBatchScheduler batchScheduler, IDbContextFactory<AppDbContext> dbContextFactory, DataLoaderOptions? options = null)
            : base(batchScheduler, options)
        {
            _dbContextFactory = dbContextFactory;
        }

        protected override async Task<ILookup<Guid, Notification>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var notifications = await dbContext.Notifications
              .Where(n => keys.Contains(n.UserId))
              .OrderByDescending(n => n.CreatedAt)
              .ToListAsync(cancellationToken);

            var topPerUser = notifications
                .GroupBy(n => n.UserId)
                .SelectMany(g => g.Take(10))
                .ToLookup(n => n.UserId);

            return topPerUser;
        }
    }
}