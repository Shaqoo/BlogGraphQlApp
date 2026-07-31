using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class UserByIdDataLoader(
        IBatchScheduler batchScheduler,
        IDbContextFactory<AppDbContext> dbContextFactory,
        DataLoaderOptions? options = null)
        : BatchDataLoader<Guid, User>(batchScheduler, options)
    {
        protected override async Task<IReadOnlyDictionary<Guid, User>> LoadBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var users = await dbContext.Users
                .Where(u => keys.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);
            return users;
        }
    }
}