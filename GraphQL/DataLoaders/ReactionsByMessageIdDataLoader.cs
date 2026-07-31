using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class ReactionsByMessageIdDataLoader(IBatchScheduler batchScheduler, IDbContextFactory<AppDbContext> dbContextFactory
     , DataLoaderOptions options) : GroupedDataLoader<Guid, Reaction>(batchScheduler,options)
    {
        protected override async Task<ILookup<Guid, Reaction>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var reactions = await dbContext.Reactions
                //.Include(r => r.User)
                //.Select(a => new Reaction
                //{
                //    Id = a.Id,
                //    CreatedAt = a.CreatedAt,
                //    UpdatedAt = a.UpdatedAt,
                //    Emoji = a.Emoji,
                //    MessageId = a.MessageId,
                //    PostId = a.PostId,
                //    ReelId = a.ReelId,
                //    UserId = a.UserId,
                //    User = new User
                //    {
                //        Id = a.User!.Id,
                //        FullName = a.User.FullName,
                //    }
                //})
                .Where(r => keys.Contains(r.MessageId!.Value))
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(cancellationToken);
            
            var lookup = reactions.ToLookup(r => r.MessageId!.Value);
            return lookup;
        }
    }
}
