using BlogGraphQlApp.Core.Repositories;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore.Storage;

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
        IRepository<UserWebPushSubscription> WebPushSubscriptions { get; }
        IRepository<ActiveVideoCall> ActiveVideoCalls { get; }
        IRepository<ChatGroup> ChatGroups { get; }
        IRepository<ChatGroupMember> ChatGroupMembers { get; }
        IRepository<GroupMessage> GroupMessages { get; }
        IRepository<GroupVideoCall> GroupVideoCalls { get; }
        IRepository<GroupVideoCallParticipant> GroupVideoCallParticipants { get; }
        IRepository<CallHistory> CallHistories { get; }
        IRepository<GroupCallParticipantHistory> GroupCallParticipantHistories { get; }
        IRepository<GroupMessageMention> GroupMessageMentions { get; }
        IRepository<GroupMessageRead> GroupMessageReads { get; }
        IRepository<GroupJoinRequest> GroupJoinRequests { get; }
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task<int> CompleteAsync(CancellationToken cancellationToken = default);
    }
}