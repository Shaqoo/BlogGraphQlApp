using BlogGraphQlApp.Core.Repositories;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Repositories.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<User> Users { get; }
        IRepository<Post> Posts { get; }
        IRepository<Reel> Reels { get; }
        IRepository<Reaction> Reactions { get; }
        IRepository<Reply> Replies { get; }
        IRepository<Notification> Notifications { get; }
        IRepository<UserInteraction> UserInteractions { get; }
        IRepository<UserFollow> UserFollows { get; }
        IRepository<Conversation> Conversations { get; }
        IRepository<Message> Messages { get; }
        IRepository<PostMention> PostMentions { get; }
        IRepository<Hashtag> HashTags { get; }
        IRepository<PostHashtag> PostHashtags { get; }
        IRepository<ModerationResult> ModerationResults { get; }
        IRepository<AiUsage> AiUsages { get; }
        Task<int> CompleteAsync();
    }
}