using BlogGraphQlApp.Core.Repositories;
using BlogGraphQlApp.Data;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Infrastructure.Repositories;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BlogGraphQlApp.Infrastructure
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly AppDbContext _context;

        public IRepository<User> Users { get; }
        public IRepository<RefreshToken> RefreshTokens { get; }
        public IRepository<Post> Posts { get; }
        public IRepository<Reel> Reels { get; }
        public IRepository<Reaction> Reactions { get; }
        public IRepository<Reply> Replies { get; }
        public IRepository<Notification> Notifications { get; }
        public IRepository<UserInteraction> UserInteractions { get; }
        public IRepository<UserFollow> UserFollows { get; }
        public IRepository<Conversation> Conversations { get; }
        public IRepository<Message> Messages { get; }
        public IRepository<PostMention> PostMentions { get; }
        public IRepository<Hashtag> HashTags { get; }
        public IRepository<PostHashtag> PostHashtags { get; }

        public IRepository<ModerationResult> ModerationResults { get; }

        public IRepository<AiUsage> AiUsages { get; }

        public IRepository<UserWebPushSubscription> WebPushSubscriptions { get; }

        public IRepository<ActiveVideoCall> ActiveVideoCalls { get; }

        public IRepository<ChatGroup> ChatGroups { get; }

        public IRepository<ChatGroupMember> ChatGroupMembers { get; }

        public IRepository<GroupMessage> GroupMessages { get; }

        public IRepository<GroupVideoCall> GroupVideoCalls { get; }

        public IRepository<GroupVideoCallParticipant> GroupVideoCallParticipants { get; }

        public IRepository<CallHistory> CallHistories { get; }

        public IRepository<GroupCallParticipantHistory> GroupCallParticipantHistories { get; }

        public IRepository<GroupMessageMention> GroupMessageMentions { get; }

        public IRepository<GroupMessageRead> GroupMessageReads { get; }

        public IRepository<GroupJoinRequest> GroupJoinRequests { get; }

        public UnitOfWork(IDbContextFactory<AppDbContext> factory)
        {
            _context = factory.CreateDbContext();
            Users = new Repository<User>(_context);
            RefreshTokens = new Repository<RefreshToken>(_context);
            Posts = new Repository<Post>(_context);
            Reels = new Repository<Reel>(_context);
            Reactions = new Repository<Reaction>(_context);
            Replies = new Repository<Reply>(_context);
            Notifications = new Repository<Notification>(_context);
            UserInteractions = new Repository<UserInteraction>(_context);
            UserFollows = new Repository<UserFollow>(_context);
            Conversations = new Repository<Conversation>(_context);
            Messages = new Repository<Message>(_context);
            PostMentions = new Repository<PostMention>(_context);
            HashTags = new Repository<Hashtag>(_context);
            PostHashtags = new Repository<PostHashtag>(_context);
            AiUsages = new Repository<AiUsage>(_context);
            ModerationResults = new Repository<ModerationResult>(_context);
            WebPushSubscriptions = new Repository<UserWebPushSubscription>(_context);
            ActiveVideoCalls = new Repository<ActiveVideoCall>(_context);
            ChatGroups = new Repository<ChatGroup>(_context);
            ChatGroupMembers = new Repository<ChatGroupMember>(_context);
            GroupMessages = new Repository<GroupMessage>(_context);
            GroupVideoCalls = new Repository<GroupVideoCall>(_context);
            GroupVideoCallParticipants = new Repository<GroupVideoCallParticipant>(_context);
            CallHistories = new Repository<CallHistory>(_context);
            GroupCallParticipantHistories = new Repository<GroupCallParticipantHistory>(_context);
            GroupMessageMentions = new Repository<GroupMessageMention>(_context);
            GroupMessageReads = new Repository<GroupMessageRead>(_context);
            GroupJoinRequests = new Repository<GroupJoinRequest>(_context);
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => await _context.Database.BeginTransactionAsync(cancellationToken);

        public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await operation();
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        public async Task<int> CompleteAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}