# Group Chat Upgrade — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade `ChatGroup`, `GroupMessage`, and `GroupVideoCall` into a feature-complete, production-grade group chat (media, mentions, replies, reactions, read receipts, pinned messages, system messages, invites/join requests, muting, typing, presence, unread counts, search, expanded calls) consistent with the existing 1:1 messaging architecture.

**Architecture:** Follows the existing pattern — entities + EF configs + one migration, scoped services over `IUnitOfWork` returning `ApiResponse<T>`, HotChocolate GraphQL (`[ExtendObjectType]` + `[Authorize]`), UploadThing via `IFileStorage`, DataLoaders for N+1, `PaginatedResult<T>` for paging. A new `GroupMessageService` owns all message behavior; `GroupService`/`GroupCallService` are extended; `NotificationService` gains `CreateAsync`; permissions stay centralized in `GroupPermissions` + a thin `GroupPermissionService`.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (Pomelo MySql), HotChocolate GraphQL + `AddInMemorySubscriptions`, AutoMapper, FluentValidation, UploadThing (`IFileStorage`), Daily.co.

## Global Constraints

- **Do NOT modify WebPush code.** `IWebPushService`/`WebPushService` stay untouched; call notifications keep using `SendToUsersAsync` as-is.
- Namespaces/patterns must match the codebase: entities in `BlogGraphQlApp.Entities` (group) / `BlogGraphQlApp.Models` (Message, Reaction, User, Conversation); enums in `BlogGraphQlApp.Enums`; configs in `BlogGraphQlApp.Data.Configurations`; DTOs in `BlogGraphQlApp.DTOs`; services return `BlogGraphQlApp.Common.ApiResponse<T>`.
- `MessageType` enum values already stored as ints (Text=1, Audio=2, Image=3, Document=4) — only **append** new values (`Video=5`, `System=6`), never reorder.
- Every mutation/query/subscription gets `[Authorize]`; every group operation validates membership; role rules live only in `GroupPermissions`.
- `MessageType.System` messages cannot be edited, deleted, reacted to, or pinned (enforced in `GroupMessageService`).
- One EF Core migration for the whole feature.
- No test project exists in this repo (verified). Verification = `dotnet build` (0 errors) + migration apply + live GraphQL smoke tests, consistent with prior phases. Do not introduce a test framework.
- Build command: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj` (run from repo root). Migrations: `"/mnt/c/Program Files/dotnet/dotnet.exe" ef migrations add <Name> --project BlogGraphQlApp.csproj`.
- Commit only the files listed in each task. Commit messages use the repo style (`feat:`, `refactor:`, `docs:`).
- Subscription topics are shared contract strings (must match between publisher and subscriber):
  - Message sent: `{groupId}_GroupMessage` (existing)
  - Message edited: `{groupId}_GroupMessageEdited`; deleted: `{groupId}_GroupMessageDeleted`; pinned/unpinned: `{groupId}_GroupMessagePinned`
  - Reaction added/removed: `{groupId}_GroupMessageReactionAdded` / `{groupId}_GroupMessageReactionRemoved`
  - Member joined/left: `{groupId}_GroupMemberJoined` / `{groupId}_GroupMemberLeft`
  - Group updated: `{groupId}_GroupUpdated`
  - Typing: `{groupId}_GroupTyping`
  - Call: `{groupId}_GroupCallStarted`, `{groupId}_GroupCallEnded` (existing), `{callId}_GroupCallParticipantJoined`, `{callId}_GroupCallParticipantLeft`, `{callId}_GroupCallParticipantUpdated`
  - Notification: `{userId}_User_NotificationReceived` (existing, in `NotificationSubscription`)

---

## File Structure

**New files**
- `Entities/GroupMessageMention.cs`, `Entities/GroupMessageRead.cs`, `Entities/GroupJoinRequest.cs`
- `Configurations/EfConfigs/GroupMessageMentionConfiguration.cs`, `GroupMessageReadConfiguration.cs`, `GroupJoinRequestConfiguration.cs`
- `Enums/MessageStatus.cs`, `Enums/CallMediaType.cs`, `Enums/NotificationLevel.cs`, `Enums/JoinRequestStatus.cs`
- `Dtos/GroupMentionDto.cs`, `Dtos/GroupCallParticipantDto.cs`, `Dtos/GroupJoinRequestDto.cs`, `Dtos/GroupMessageSearchInput.cs`
- `Services/Groups/MentionParser.cs`, `Services/Groups/GroupPermissionService.cs`, `Services/Groups/IGroupMessageService.cs`, `Services/Groups/GroupMessageService.cs`
- `GraphQL/DataLoaders/ReactionsByGroupMessageIdDataLoader.cs`, `MentionsByGroupMessageIdDataLoader.cs`, `GroupMessageByIdDataLoader.cs`, `ReadsByGroupMessageIdDataLoader.cs`
- `GraphQL/Resolvers/GroupMessageResolvers.cs`
- `GraphQL/Types/GroupMentionTypeGql.cs`, `GroupCallParticipantTypeGql.cs`, `GroupJoinRequestTypeGql.cs`
- `GraphQL/Queries/GroupMessageQueries.cs`, `GraphQL/Queries/GroupCallQueries.cs`
- `GraphQL/Mutations/GroupMessageMutations.cs`
- `GraphQL/Events/GroupTypingEvent.cs`
- `Validators/SendGroupMessageValidator.cs`, `Validators/SearchGroupMessagesValidator.cs`
- `Extensions/Mapping/GroupMappingProfile.cs`
- `Migrations/2026xxxx_GroupChatUpgrade.*` (generated)

**Modified files**
- `Entities/ChatGroup.cs`, `Entities/GroupMessage.cs`, `Entities/ChatGroupMember.cs`, `Entities/GroupVideoCall.cs`, `Entities/GroupVideoCallParticipant.cs`, `Entities/Reaction.cs`, `Entities/Notification.cs`
- `Enums/MessageType.cs`, `Enums/NotificationType.cs`
- `Configurations/EfConfigs/ChatGroupConfiguration.cs`, `GroupMessageConfiguration.cs`, `ChatGroupMemberConfiguration.cs`, `GroupVideoCallConfiguration.cs`, `ReactionConfiguration.cs`
- `Context/AppDbContext.cs`
- `Repositories/Interfaces/IUnitOfWork.cs`, `Repositories/Implementations/UnitOfWork.cs`
- `Dtos/GroupDto.cs`, `Dtos/GroupMessageDto.cs`, `Dtos/GroupMemberDto.cs`, `Dtos/GroupCallDto.cs`, `Dtos/NotificationDto.cs`, `Dtos/CreateReactionDto.cs`
- `Services/Interfaces/INotificationService.cs`, `Services/Implementations/NotificationService.cs`
- `Services/Interfaces/IReactionService.cs`, `Services/Implementations/ReactionService.cs`
- `Services/Groups/IGroupService.cs`, `Services/Groups/GroupService.cs`, `Services/Groups/IGroupCallService.cs`, `Services/Groups/GroupCallService.cs`, `Services/Groups/GroupPermissions.cs`
- `GraphQL/Types/GroupTypeGql.cs`, `GroupMessageTypeGql.cs`, `NotificationTypeGql.cs`
- `GraphQL/Queries/GroupQueries.cs`, `GraphQL/Mutations/GroupMutations.cs`, `GraphQL/Mutations/GroupCallMutations.cs`, `GraphQL/Subscriptions/CallSubscription.cs`
- `GraphQL/Events/ReactionPayload.cs`
- `Program.cs`
- `AGENTS.md`, `GRAPHQL_SCHEMA.md`, `REALTIME_FEATURES.md`

---

## Phase 1 — Data layer

### Task 1: Enums

**Files:**
- Modify: `Enums/MessageType.cs`, `Enums/NotificationType.cs`
- Create: `Enums/MessageStatus.cs`, `Enums/CallMediaType.cs`, `Enums/NotificationLevel.cs`, `Enums/JoinRequestStatus.cs`

- [ ] **Step 1: Append values to existing enums**

`Enums/MessageType.cs` — append `Video` and `System` after `Document` (do NOT touch `Text = 1, Audio, Image, Document`):
```csharp
namespace BlogGraphQlApp.Enums
{
    public enum MessageType
    {
        Text = 1,
        Audio,
        Image,
        Document,
        Video,
        System
    }
}
```

`Enums/NotificationType.cs` — append:
```csharp
    public enum NotificationType
    {
        NewReply,
        NewReaction,
        MediaSaved,
        MentionsSaved,
        NewFollower,
        InvalidMentions,
        GroupMemberAdded,
        GroupMention,
        GroupReply,
        GroupReaction,
        GroupCallStarted,
        GroupCallMissed,
        GroupUpdated,
        GroupRoleChanged,
        GroupInvite,
    }
```

- [ ] **Step 2: Create the four new enums**

`Enums/MessageStatus.cs`:
```csharp
namespace BlogGraphQlApp.Enums
{
    public enum MessageStatus
    {
        Sending,
        Sent,
        Delivered,
        Read,
        Failed
    }
}
```

`Enums/CallMediaType.cs`:
```csharp
namespace BlogGraphQlApp.Enums
{
    public enum CallMediaType
    {
        Voice,
        Video
    }
}
```

`Enums/NotificationLevel.cs`:
```csharp
namespace BlogGraphQlApp.Enums
{
    public enum NotificationLevel
    {
        All,
        MentionsOnly,
        Muted
    }
}
```

`Enums/JoinRequestStatus.cs`:
```csharp
namespace BlogGraphQlApp.Enums
{
    public enum JoinRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }
}
```

- [ ] **Step 3: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add Enums/MessageType.cs Enums/NotificationType.cs Enums/MessageStatus.cs Enums/CallMediaType.cs Enums/NotificationLevel.cs Enums/JoinRequestStatus.cs
git commit -m "feat: add enums for group chat upgrade (message status, media type, notification level, join request)"
```

### Task 2: Entities

**Files:**
- Modify: `Entities/ChatGroup.cs`, `Entities/GroupMessage.cs`, `Entities/ChatGroupMember.cs`, `Entities/GroupVideoCall.cs`, `Entities/GroupVideoCallParticipant.cs`, `Entities/Reaction.cs`, `Entities/Notification.cs`
- Create: `Entities/GroupMessageMention.cs`, `Entities/GroupMessageRead.cs`, `Entities/GroupJoinRequest.cs`

- [ ] **Step 1: Replace `Entities/GroupMessage.cs`**

```csharp
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupMessage : BaseEntity
    {
        public Guid GroupId { get; set; }
        public ChatGroup Group { get; set; } = null!;
        public Guid SenderId { get; set; }
        public User Sender { get; set; } = null!;
        public MessageType MessageType { get; set; } = MessageType.Text;
        public string? Content { get; set; }
        public string? FileUrl { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public GroupMessage? ReplyToMessage { get; set; }
        public DateTime? EditedAt { get; set; }
        public Guid? EditedBy { get; set; }
        public bool Deleted { get; set; }
        public bool IsPinned { get; set; }
        public DateTime? PinnedAt { get; set; }
        public Guid? PinnedBy { get; set; }
        public MessageStatus Status { get; set; } = MessageStatus.Sent;
        public string? Metadata { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public ICollection<GroupMessageMention> Mentions { get; set; } = [];
        public ICollection<GroupMessageRead> Reads { get; set; } = [];
        public ICollection<Reaction> Reactions { get; set; } = [];
    }
}
```

- [ ] **Step 2: Replace `Entities/ChatGroup.cs`**

```csharp
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class ChatGroup : BaseEntity
    {
        public required string Name { get; set; }
        public Guid CreatedBy { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsPrivate { get; set; }
        public string? InviteCode { get; set; }
        public Guid? LastMessageId { get; set; }
        public GroupMessage? LastMessage { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool Archived { get; set; }
        public int? MaxMembers { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public ICollection<ChatGroupMember> Members { get; set; } = [];
        public ICollection<GroupMessage> Messages { get; set; } = [];
        public ICollection<GroupVideoCall> VideoCalls { get; set; } = [];
        public ICollection<GroupJoinRequest> JoinRequests { get; set; } = [];
    }
}
```

- [ ] **Step 3: Extend `Entities/ChatGroupMember.cs`** (add after `JoinedAt`):

```csharp
        public bool Muted { get; set; }
        public DateTime? MutedUntil { get; set; }
        public NotificationLevel NotificationLevel { get; set; } = NotificationLevel.All;
        public DateTime? LastReadAt { get; set; }
```

- [ ] **Step 4: Extend `Entities/GroupVideoCall.cs`** (add after `Status`):

```csharp
        public CallMediaType MediaType { get; set; } = CallMediaType.Video;
```

- [ ] **Step 5: Extend `Entities/GroupVideoCallParticipant.cs`** (add after `LeftAt`):

```csharp
        public bool IsMuted { get; set; }
        public bool CameraEnabled { get; set; }
        public bool ScreenSharing { get; set; }
        public bool HandRaised { get; set; }
```

- [ ] **Step 6: Extend `Entities/Reaction.cs`** (add `using BlogGraphQlApp.Entities;` at top and these members after `ReplyId`):

```csharp
        public Guid? GroupMessageId { get; set; }
        public GroupMessage? GroupMessage { get; set; }
```

- [ ] **Step 7: Extend `Entities/Notification.cs`** (add after `ReadAt`):

```csharp
        public Guid? RelatedEntityId { get; set; }
        public int RelatedEntityType { get; set; }
        public string? Metadata { get; set; }
```

- [ ] **Step 8: Create the three new entities**

`Entities/GroupMessageMention.cs`:
```csharp
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupMessageMention : BaseEntity
    {
        public Guid MessageId { get; set; }
        public GroupMessage Message { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string MentionText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

`Entities/GroupMessageRead.cs`:
```csharp
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupMessageRead : BaseEntity
    {
        public Guid MessageId { get; set; }
        public GroupMessage Message { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
```

`Entities/GroupJoinRequest.cs`:
```csharp
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class GroupJoinRequest : BaseEntity
    {
        public Guid GroupId { get; set; }
        public ChatGroup Group { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public Guid? ResolvedBy { get; set; }
    }
}
```

- [ ] **Step 9: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add Entities/
git commit -m "feat: extend group entities for full group chat (messages, mentions, reads, join requests, calls)"
```

### Task 3: EF configurations

**Files:**
- Modify: `Configurations/EfConfigs/ChatGroupConfiguration.cs`, `GroupMessageConfiguration.cs`, `ChatGroupMemberConfiguration.cs`, `GroupVideoCallConfiguration.cs`, `ReactionConfiguration.cs`
- Create: `Configurations/EfConfigs/GroupMessageMentionConfiguration.cs`, `GroupMessageReadConfiguration.cs`, `GroupJoinRequestConfiguration.cs`

- [ ] **Step 1: Replace `ChatGroupConfiguration.cs`**

```csharp
using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class ChatGroupConfiguration : IEntityTypeConfiguration<ChatGroup>
    {
        public void Configure(EntityTypeBuilder<ChatGroup> builder)
        {
            builder.HasKey(g => g.Id);

            builder.Property(g => g.Name).IsRequired().HasMaxLength(120);
            builder.Property(g => g.Description).HasMaxLength(500);
            builder.Property(g => g.InviteCode).HasMaxLength(32);
            builder.Property(g => g.IsPrivate).HasDefaultValue(false);
            builder.Property(g => g.Archived).HasDefaultValue(false);
            builder.Property(g => g.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            builder.Property(g => g.RowVersion).IsRowVersion();

            builder.HasIndex(g => g.InviteCode).IsUnique();

            builder.HasOne(g => g.CreatedByUser).WithMany().HasForeignKey(g => g.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(g => g.LastMessage).WithOne().HasForeignKey<ChatGroup>(g => g.LastMessageId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

- [ ] **Step 2: Replace `GroupMessageConfiguration.cs`**

```csharp
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class GroupMessageConfiguration : IEntityTypeConfiguration<GroupMessage>
    {
        public void Configure(EntityTypeBuilder<GroupMessage> builder)
        {
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Content).HasMaxLength(2000);
            builder.Property(m => m.FileUrl).HasMaxLength(2048);
            builder.Property(m => m.Metadata).HasColumnType("json");
            builder.Property(m => m.MessageType).HasDefaultValue(MessageType.Text);
            builder.Property(m => m.Status).HasDefaultValue(MessageStatus.Sent);
            builder.Property(m => m.IsPinned).HasDefaultValue(false);
            builder.Property(m => m.RowVersion).IsRowVersion();

            builder.HasIndex(m => new { m.GroupId, m.CreatedAt });
            builder.HasIndex(m => new { m.GroupId, m.SenderId });
            builder.HasIndex(m => new { m.GroupId, m.IsPinned });
            builder.HasIndex(m => new { m.GroupId, m.MessageType });
            builder.HasIndex(m => m.SenderId);
            builder.HasIndex(m => m.ReplyToMessageId);

            builder.HasOne(m => m.Group).WithMany(g => g.Messages).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(m => m.ReplyToMessage).WithMany().HasForeignKey(m => m.ReplyToMessageId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

- [ ] **Step 3: Extend `ChatGroupMemberConfiguration.cs`** (append inside `Configure`):

```csharp
            builder.Property(m => m.Muted).HasDefaultValue(false);
            builder.Property(m => m.NotificationLevel).HasDefaultValue(NotificationLevel.All);
```

- [ ] **Step 4: Extend `GroupVideoCallConfiguration.cs`** (append inside `Configure`):

```csharp
            builder.Property(c => c.MediaType).HasDefaultValue(CallMediaType.Video);
```

- [ ] **Step 5: Extend `ReactionConfiguration.cs`** (append inside `Configure`):

```csharp
            builder.HasOne(r => r.GroupMessage).WithMany(gm => gm.Reactions).HasForeignKey(r => r.GroupMessageId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(r => r.GroupMessageId);
            builder.HasIndex(r => new { r.GroupMessageId, r.UserId }).IsUnique();
```

- [ ] **Step 6: Create the three new configurations**

`Configurations/EfConfigs/GroupMessageMentionConfiguration.cs`:
```csharp
using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class GroupMessageMentionConfiguration : IEntityTypeConfiguration<GroupMessageMention>
    {
        public void Configure(EntityTypeBuilder<GroupMessageMention> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.MentionText).HasMaxLength(64);
            builder.HasIndex(e => new { e.MessageId, e.UserId }).IsUnique();
            builder.HasIndex(e => e.UserId);
            builder.HasOne(e => e.Message).WithMany(m => m.Mentions).HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

`Configurations/EfConfigs/GroupMessageReadConfiguration.cs`:
```csharp
using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class GroupMessageReadConfiguration : IEntityTypeConfiguration<GroupMessageRead>
    {
        public void Configure(EntityTypeBuilder<GroupMessageRead> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.MessageId, e.UserId }).IsUnique();
            builder.HasIndex(e => new { e.UserId, e.ReadAt });
            builder.HasOne(e => e.Message).WithMany(m => m.Reads).HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

`Configurations/EfConfigs/GroupJoinRequestConfiguration.cs`:
```csharp
using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class GroupJoinRequestConfiguration : IEntityTypeConfiguration<GroupJoinRequest>
    {
        public void Configure(EntityTypeBuilder<GroupJoinRequest> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
            builder.HasIndex(e => new { e.GroupId, e.Status });
            builder.HasOne(e => e.Group).WithMany(g => g.JoinRequests).HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 7: Register DbSets in `Context/AppDbContext.cs`** (append inside the class, after `GroupCallParticipantHistories`):

```csharp
        public DbSet<GroupMessageMention> GroupMessageMentions => Set<GroupMessageMention>();
        public DbSet<GroupMessageRead> GroupMessageReads => Set<GroupMessageRead>();
        public DbSet<GroupJoinRequest> GroupJoinRequests => Set<GroupJoinRequest>();
```

(Configurations are auto-discovered via `ApplyConfigurationsFromAssembly` — no other change needed.)

- [ ] **Step 8: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add Configurations/EfConfigs/ Context/AppDbContext.cs
git commit -m "feat: add EF configurations and DbSets for group chat upgrade"
```

### Task 4: Unit of Work — new repositories + transactions

**Files:**
- Modify: `Repositories/Interfaces/IUnitOfWork.cs`, `Repositories/Implementations/UnitOfWork.cs`

- [ ] **Step 1: Extend `IUnitOfWork.cs`** — add `using Microsoft.EntityFrameworkCore.Storage;` and after `GroupCallParticipantHistories`:

```csharp
        IRepository<GroupMessageMention> GroupMessageMentions { get; }
        IRepository<GroupMessageRead> GroupMessageReads { get; }
        IRepository<GroupJoinRequest> GroupJoinRequests { get; }
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Extend `UnitOfWork.cs`** — add `using Microsoft.EntityFrameworkCore.Storage;`, the three repository properties + assignments, and the transaction method:

```csharp
        public IRepository<GroupMessageMention> GroupMessageMentions { get; }
        public IRepository<GroupMessageRead> GroupMessageReads { get; }
        public IRepository<GroupJoinRequest> GroupJoinRequests { get; }
```

Assign in the constructor (after `GroupCallParticipantHistories = ...;`):
```csharp
            GroupMessageMentions = new Repository<GroupMessageMention>(_context);
            GroupMessageReads = new Repository<GroupMessageRead>(_context);
            GroupJoinRequests = new Repository<GroupJoinRequest>(_context);
```

Add method:
```csharp
        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => await _context.Database.BeginTransactionAsync(cancellationToken);
```

- [ ] **Step 3: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add Repositories/
git commit -m "feat: add group message/join-request repositories and transaction helper to unit of work"
```

### Task 5: DTOs + AutoMapper profile

**Files:**
- Modify: `Dtos/GroupDto.cs`, `Dtos/GroupMessageDto.cs`, `Dtos/GroupMemberDto.cs`, `Dtos/GroupCallDto.cs`, `Dtos/NotificationDto.cs`, `Dtos/CreateReactionDto.cs`
- Create: `Dtos/GroupMentionDto.cs`, `Dtos/GroupCallParticipantDto.cs`, `Dtos/GroupJoinRequestDto.cs`, `Dtos/GroupMessageSearchInput.cs`, `Extensions/Mapping/GroupMappingProfile.cs`

- [ ] **Step 1: Replace `Dtos/GroupDto.cs`**

```csharp
namespace BlogGraphQlApp.DTOs
{
    public class GroupDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsPrivate { get; set; }
        public string? InviteCode { get; set; }
        public Guid? LastMessageId { get; set; }
        public GroupMessageDto? LastMessage { get; set; }
        public UserDto? LastSender { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool Archived { get; set; }
        public int? MaxMembers { get; set; }
        public Guid CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int MemberCount { get; set; }
        public int UnreadCount { get; set; }
    }
}
```

- [ ] **Step 2: Replace `Dtos/GroupMessageDto.cs`**

```csharp
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class GroupMessageDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderAvatar { get; set; }
        public MessageType MessageType { get; set; }
        public string? Content { get; set; }
        public string? FileUrl { get; set; }
        public Guid? ReplyToMessageId { get; set; }
        public GroupMessageDto? ReplyToMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public Guid? EditedBy { get; set; }
        public bool Deleted { get; set; }
        public bool IsPinned { get; set; }
        public DateTime? PinnedAt { get; set; }
        public Guid? PinnedBy { get; set; }
        public MessageStatus Status { get; set; }
        public string? Metadata { get; set; }
        public int DeliveredCount { get; set; }
        public int ReadCount { get; set; }
        public int UnreadCount { get; set; }
        public IEnumerable<GroupMentionDto> Mentions { get; set; } = [];
        public IEnumerable<ReactionDto> Reactions { get; set; } = [];
    }
}
```

- [ ] **Step 3: Create `Dtos/GroupMentionDto.cs`**

```csharp
namespace BlogGraphQlApp.DTOs
{
    public class GroupMentionDto
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string MentionText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
```

- [ ] **Step 4: Create `Dtos/GroupCallParticipantDto.cs`**

```csharp
namespace BlogGraphQlApp.DTOs
{
    public class GroupCallParticipantDto
    {
        public Guid Id { get; set; }
        public Guid CallId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public DateTime? JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }
        public bool IsMuted { get; set; }
        public bool CameraEnabled { get; set; }
        public bool ScreenSharing { get; set; }
        public bool HandRaised { get; set; }
    }
}
```

- [ ] **Step 5: Create `Dtos/GroupJoinRequestDto.cs`**

```csharp
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class GroupJoinRequestDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public JoinRequestStatus Status { get; set; }
        public DateTime RequestedAt { get; set; }
    }
}
```

- [ ] **Step 6: Create `Dtos/GroupMessageSearchInput.cs`**

```csharp
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.DTOs
{
    public class GroupMessageSearchInput
    {
        public string? Text { get; set; }
        public Guid? SenderId { get; set; }
        public Guid? MentionedUserId { get; set; }
        public bool? Pinned { get; set; }
        public MessageType? MediaType { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool? HasReactions { get; set; }
        public bool? RepliesOnly { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
```

- [ ] **Step 7: Extend DTOs**

`Dtos/GroupMemberDto.cs` — add:
```csharp
        public bool Online { get; set; }
        public DateTime? LastSeen { get; set; }
```

`Dtos/GroupCallDto.cs` — add `using BlogGraphQlApp.Enums;` already present; add:
```csharp
        public CallMediaType MediaType { get; set; }
```

`Dtos/NotificationDto.cs` — add:
```csharp
        public Guid? RelatedEntityId { get; set; }
        public int RelatedEntityType { get; set; }
        public string? Metadata { get; set; }
```

`Dtos/CreateReactionDto.cs` — add:
```csharp
        public Guid? GroupMessageId { get; set; }
```

- [ ] **Step 8: Create `Extensions/Mapping/GroupMappingProfile.cs`**

```csharp
using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;

namespace BlogGraphQlApp.Extensions.Mapping
{
    public class GroupMappingProfile : Profile
    {
        public GroupMappingProfile()
        {
            CreateMap<GroupMessage, GroupMessageDto>()
                .ForMember(d => d.SenderName, o => o.MapFrom(s => s.Sender != null ? s.Sender.FullName : string.Empty))
                .ForMember(d => d.SenderAvatar, o => o.MapFrom(s => s.Sender != null ? s.Sender.ProfilePictureUrl : null))
                .ForMember(d => d.Mentions, o => o.MapFrom(s => s.Mentions))
                .ForMember(d => d.Reactions, o => o.MapFrom(s => s.Reactions));

            CreateMap<GroupMessageMention, GroupMentionDto>()
                .ForMember(d => d.Username, o => o.MapFrom(s => s.User != null ? s.User.Username : string.Empty))
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty));

            CreateMap<GroupJoinRequest, GroupJoinRequestDto>()
                .ForMember(d => d.Username, o => o.MapFrom(s => s.User != null ? s.User.Username : string.Empty))
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.Avatar, o => o.MapFrom(s => s.User != null ? s.User.ProfilePictureUrl : null));

            CreateMap<GroupVideoCallParticipant, GroupCallParticipantDto>()
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : string.Empty))
                .ForMember(d => d.Avatar, o => o.MapFrom(s => s.User != null ? s.User.ProfilePictureUrl : null));
        }
    }
}
```

- [ ] **Step 9: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add Dtos/ Extensions/Mapping/GroupMappingProfile.cs
git commit -m "feat: add DTOs and mapping profile for group chat upgrade"
```

### Task 6: Migration

**Files:**
- Create: `Migrations/*GroupChatUpgrade.*`

- [ ] **Step 1: Build, then generate the migration**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Then: `"/mnt/c/Program Files/dotnet/dotnet.exe" ef migrations add GroupChatUpgrade --project BlogGraphQlApp.csproj`
Expected: migration files created.

- [ ] **Step 2: Inspect the generated migration**

Open the generated `Migrations/<timestamp>_GroupChatUpgrade.cs` and verify:
- Every new NOT NULL column on an existing table has an explicit default (EF generates these because of the `HasDefaultValue`/`HasDefaultValueSql` configs): `ChatGroupMembers` → `Muted` (false), `NotificationLevel` (0); `GroupMessages` → `MessageType` (1), `Status` (1), `IsPinned` (false); `ChatGroups` → `IsPrivate` (false), `Archived` (false), `UpdatedAt` (CURRENT_TIMESTAMP(6)); `GroupVideoCalls` → `MediaType` (1). If any new NOT NULL column on an existing table lacks a default, add `.HasDefaultValue(...)` to its configuration and re-run the migration generation.
- `GroupMessages.Text` column is dropped or renamed to `Content` — confirm the `Content` column exists. (The DTO/service layer is updated in later tasks; data migration of `Text` values is optional — if `Content` is a new nullable column, copy old values via the migration `migrationBuilder.Sql("UPDATE GroupMessages SET Content = Text WHERE Text IS NOT NULL")` before dropping `Text`.)
- New tables `GroupMessageMentions`, `GroupMessageReads`, `GroupJoinRequests` with the configured indexes.
- `RowVersion` columns exist on `ChatGroups` and `GroupMessages` (MySQL `timestamp(6) ROWVERSION` / EF `IsRowVersion`).

- [ ] **Step 3: Commit**

```bash
git add Migrations/
git commit -m "feat: add GroupChatUpgrade EF Core migration"
```

(The migration will be applied automatically on app startup via `ApplyMigrationAsync`.)

### Task 7: ReactionService group-message support

**Files:**
- Modify: `Services/Interfaces/IReactionService.cs`, `Services/Implementations/ReactionService.cs`

- [ ] **Step 1: Extend `IReactionService.cs`** — add after `GetReactionsByPostIdAsync`:

```csharp
        Task<IQueryable<ReactionDto>> GetReactionsByGroupMessageIdAsync(Guid groupMessageId);
```

- [ ] **Step 2: Extend `ReactionService.cs`**

In `CreateReactionAsync`, extend the existence check query to also match `GroupMessageId` (change the `exists` query condition to add `&& r.GroupMessageId == createReactionDto.GroupMessageId`), and add `GroupMessageId = createReactionDto.GroupMessageId,` to the new `Reaction` initializer.

Add the new method (mirror `GetReactionsByPostIdAsync`):
```csharp
        public Task<IQueryable<ReactionDto>> GetReactionsByGroupMessageIdAsync(Guid groupMessageId)
        {
            _logger.LogInformation("Building IQueryable for reactions by GroupMessageId: {GroupMessageId}", groupMessageId);

            var query = _mapper.ProjectTo<ReactionDto>(
                _unitOfWork.Reactions
                    .Find(r => r.GroupMessageId == groupMessageId)
            );

            return Task.FromResult(query);
        }
```

- [ ] **Step 3: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add Services/Interfaces/IReactionService.cs Services/Implementations/ReactionService.cs
git commit -m "feat: support group message reactions in reaction service"
```

---

## Phase 2 — Services

### Task 8: MentionParser + GroupPermissions + GroupPermissionService

**Files:**
- Modify: `Services/Groups/GroupPermissions.cs`
- Create: `Services/Groups/MentionParser.cs`, `Services/Groups/GroupPermissionService.cs`

- [ ] **Step 1: Create `Services/Groups/MentionParser.cs`**

```csharp
using System.Text.RegularExpressions;

namespace BlogGraphQlApp.Services.Groups
{
    public static class MentionParser
    {
        private static readonly Regex MentionRegex = new(@"@([A-Za-z0-9_.-]{1,32})", RegexOptions.Compiled);

        public static IReadOnlyList<string> Parse(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return [];

            return MentionRegex.Matches(content)
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
```

- [ ] **Step 2: Replace `Services/Groups/GroupPermissions.cs`**

Add the new rules and change `CanAddMember` so only Owner/Admin can add members (per the spec permission matrix):
```csharp
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.Services.Groups
{
    /// <summary>
    /// Pure role-based permission rules for group actions. Kept static so the rules
    /// can be unit tested without any infrastructure. Single source of truth.
    /// </summary>
    public static class GroupPermissions
    {
        public static bool CanUpdateGroup(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanChangeImage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanChangeDescription(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanDeleteGroup(GroupMemberRole role) => role == GroupMemberRole.Owner;
        public static bool CanAddMember(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanRemoveMember(GroupMemberRole actorRole, GroupMemberRole targetRole) =>
            actorRole == GroupMemberRole.Owner || (actorRole == GroupMemberRole.Admin && targetRole == GroupMemberRole.Member);
        public static bool CanPromoteAdmin(GroupMemberRole role) => role == GroupMemberRole.Owner;
        public static bool CanDemoteAdmin(GroupMemberRole role) => role == GroupMemberRole.Owner;
        public static bool CanTransferOwnership(GroupMemberRole role) => role == GroupMemberRole.Owner;
        public static bool CanManageInvite(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanManageJoinRequests(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanPinMessage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin;
        public static bool CanStartCall(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin or GroupMemberRole.Member;
        public static bool CanSendMessage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin or GroupMemberRole.Member;
        public static bool CanEditMessage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin or GroupMemberRole.Member;
        public static bool CanDeleteMessage(GroupMemberRole role) => role is GroupMemberRole.Owner or GroupMemberRole.Admin or GroupMemberRole.Member;
    }
}
```

- [ ] **Step 3: Create `Services/Groups/GroupPermissionService.cs`**

```csharp
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Groups
{
    /// <summary>
    /// Shared membership/permission helper used by every group service so checks
    /// are never scattered. Rule functions live in <see cref="GroupPermissions"/>.
    /// </summary>
    public class GroupPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public GroupPermissionService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<ChatGroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken ct = default) =>
            await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId && m.UserId == userId)
                .FirstOrDefaultAsync(ct);

        public async Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default) =>
            await GetMembershipAsync(groupId, userId, ct) is not null;

        public async Task<bool> CanAsync(Guid groupId, Guid userId, Func<GroupMemberRole, bool> rule, CancellationToken ct = default)
        {
            var membership = await GetMembershipAsync(groupId, userId, ct);
            return membership is not null && rule(membership.Role);
        }
    }
}
```

- [ ] **Step 4: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add Services/Groups/
git commit -m "feat: add mention parser, centralized permission service and extended role rules"
```

### Task 9: NotificationService.CreateAsync

**Files:**
- Modify: `Services/Interfaces/INotificationService.cs`, `Services/Implementations/NotificationService.cs`

- [ ] **Step 1: Extend `INotificationService.cs`** — add:

```csharp
using BlogGraphQlApp.Enums;
```
and
```csharp
        Task<NotificationDto> CreateAsync(Guid userId, NotificationType type, string message, Guid? relatedEntityId = null, int relatedEntityType = 0, string? metadata = null, CancellationToken ct = default);
```

- [ ] **Step 2: Extend `NotificationService.cs`**

Add `using BlogGraphQlApp.Enums;` and `using HotChocolate.Subscriptions;`; inject `ITopicEventSender` in the constructor (store as `_eventSender`). Add the method:

```csharp
        public async Task<NotificationDto> CreateAsync(
            Guid userId,
            NotificationType type,
            string message,
            Guid? relatedEntityId = null,
            int relatedEntityType = 0,
            string? metadata = null,
            CancellationToken ct = default)
        {
            var notification = new Notification
            {
                UserId = userId,
                NotificationType = type,
                Message = message,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType,
                Metadata = metadata
            };

            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.CompleteAsync(ct);

            var dto = _mapper.Map<NotificationDto>(notification);
            try
            {
                await _eventSender.SendAsync($"{userId}_User_NotificationReceived", dto, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish notification event for user {UserId}.", userId);
            }
            return dto;
        }
```

- [ ] **Step 3: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add Services/Interfaces/INotificationService.cs Services/Implementations/NotificationService.cs
git commit -m "feat: add notification create path with realtime event publishing"
```

### Task 10: GroupMessageService

**Files:**
- Create: `Services/Groups/IGroupMessageService.cs`, `Services/Groups/GroupMessageService.cs`

- [ ] **Step 1: Create `Services/Groups/IGroupMessageService.cs`**

```csharp
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Storage;

namespace BlogGraphQlApp.Services.Groups
{
    public interface IGroupMessageService
    {
        Task<ApiResponse<GroupMessageDto>> SendAsync(Guid groupId, Guid senderId, MessageType messageType, string? content, IFile? file, Guid? replyToMessageId, CancellationToken ct = default);
        Task<ApiResponse<GroupMessageDto>> EditAsync(Guid groupId, Guid messageId, Guid senderId, string content, CancellationToken ct = default);
        Task<ApiResponse<bool>> DeleteAsync(Guid groupId, Guid messageId, Guid senderId, CancellationToken ct = default);
        Task<ApiResponse<GroupMessageDto>> SetPinnedAsync(Guid groupId, Guid messageId, Guid actorId, bool pin, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleReactionAsync(Guid groupId, Guid messageId, Guid userId, string emoji, CancellationToken ct = default);
        Task<ApiResponse<bool>> RemoveReactionAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> MarkDeliveredAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> MarkReadAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> MarkAllReadAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMessagesAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default);
        Task<ApiResponse<GroupMessageDto>> GetMessageAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetPinnedMessagesAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> SearchAsync(Guid groupId, Guid userId, GroupMessageSearchInput input, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMediaAsync(Guid groupId, Guid userId, MessageType? mediaType, int page, int pageSize, CancellationToken ct = default);
        Task<ApiResponse<int>> GetUnreadCountAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<int>> GetUnreadGroupCountAsync(Guid userId, CancellationToken ct = default);
        Task<Dictionary<Guid, int>> GetUnreadCountsByGroupAsync(Guid userId, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMyMentionsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
        Task<GroupMessage> InsertSystemMessageAsync(ChatGroup group, Guid actorId, string content, string? metadata = null, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Create `Services/Groups/GroupMessageService.cs`**

```csharp
using System.Text.Json;
using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Storage;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Groups
{
    public class GroupMessageService : IGroupMessageService
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorage _fileStorage;
        private readonly INotificationService _notificationService;
        private readonly GroupPermissionService _permissions;
        private readonly ITopicEventSender _eventSender;
        private readonly IMapper _mapper;
        private readonly ILogger<GroupMessageService> _logger;

        public GroupMessageService(
            IUnitOfWork unitOfWork,
            IFileStorage fileStorage,
            INotificationService notificationService,
            GroupPermissionService permissions,
            ITopicEventSender eventSender,
            IMapper mapper,
            ILogger<GroupMessageService> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _notificationService = notificationService;
            _permissions = permissions;
            _eventSender = eventSender;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<GroupMessageDto>> SendAsync(
            Guid groupId, Guid senderId, MessageType messageType, string? content, IFile? file, Guid? replyToMessageId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, senderId, GroupPermissions.CanSendMessage, ct))
                return ApiResponse<GroupMessageDto>.Fail("You are not a member of this group.");

            if (string.IsNullOrWhiteSpace(content) && file is null)
                return ApiResponse<GroupMessageDto>.Fail("Message must have content or a file.");
            if (messageType == MessageType.System)
                return ApiResponse<GroupMessageDto>.Fail("System messages cannot be created by users.");

            var members = await LoadMembersAsync(groupId, ct);
            var membersById = members.ToDictionary(m => m.UserId);

            string? fileUrl = null;
            if (file is not null)
                fileUrl = await _fileStorage.UploadAsync(file, messageType.ToString() + "s");

            GroupMessage? replyTo = null;
            if (replyToMessageId.HasValue)
            {
                replyTo = await _unitOfWork.GroupMessages.Find(m => m.Id == replyToMessageId.Value && m.GroupId == groupId).FirstOrDefaultAsync(ct);
                if (replyTo is null)
                    return ApiResponse<GroupMessageDto>.Fail("The message you are replying to was not found.");
            }

            var usernames = MentionParser.Parse(content);
            var mentioned = members.Where(m => usernames.Contains(m.User.Username, StringComparer.OrdinalIgnoreCase)).ToList();

            var message = new GroupMessage
            {
                GroupId = groupId,
                SenderId = senderId,
                MessageType = messageType,
                Content = content?.Trim(),
                FileUrl = fileUrl,
                ReplyToMessageId = replyToMessageId,
                Status = MessageStatus.Sent
            };

            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null)
                return ApiResponse<GroupMessageDto>.Fail("Group not found.");

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await _unitOfWork.GroupMessages.AddAsync(message);

                foreach (var member in mentioned)
                {
                    await _unitOfWork.GroupMessageMentions.AddAsync(new GroupMessageMention
                    {
                        MessageId = message.Id,
                        UserId = member.UserId,
                        MentionText = "@" + member.User.Username
                    });
                }

                group.LastMessageId = message.Id;
                group.LastActivityAt = DateTime.UtcNow;
                group.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.ChatGroups.Update(group);

                foreach (var mentionedMember in mentioned)
                {
                    if (mentionedMember.UserId == senderId || !ShouldNotify(membersById[mentionedMember.UserId]))
                        continue;
                    await CreateMessageNotificationAsync(mentionedMember.UserId, NotificationType.GroupMention, message, group, ct);
                }

                if (replyTo is not null && replyTo.SenderId != senderId && ShouldNotify(membersById[replyTo.SenderId]))
                {
                    await CreateMessageNotificationAsync(replyTo.SenderId, NotificationType.GroupReply, message, group, ct);
                }

                await _unitOfWork.CompleteAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to send group message to group {GroupId}.", groupId);
                return ApiResponse<GroupMessageDto>.Fail("Failed to send message.");
            }

            var dto = await ToDtoAsync(message, group, members.Count, ct);
            await PublishAsync($"{groupId}_GroupMessage", dto, ct);
            return ApiResponse<GroupMessageDto>.Success(dto, "Message sent.");
        }

        public async Task<ApiResponse<GroupMessageDto>> EditAsync(Guid groupId, Guid messageId, Guid senderId, string content, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(content))
                return ApiResponse<GroupMessageDto>.Fail("Message content is required.");

            var (message, group, members) = await LoadForOperationAsync(groupId, messageId, senderId, ct);
            if (message is null) return ApiResponse<GroupMessageDto>.Fail("Message not found.");
            if (message.MessageType == MessageType.System) return ApiResponse<GroupMessageDto>.Fail("System messages cannot be edited.");
            if (message.SenderId != senderId) return ApiResponse<GroupMessageDto>.Fail("You can only edit your own messages.");

            message.Content = content.Trim();
            message.EditedAt = DateTime.UtcNow;
            message.EditedBy = senderId;
            _unitOfWork.GroupMessages.Update(message);

            try
            {
                await _unitOfWork.CompleteAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict editing group message {MessageId}.", messageId);
                return ApiResponse<GroupMessageDto>.Fail("This message was modified by someone else. Refresh and try again.");
            }

            var dto = await ToDtoAsync(message, group, members.Count, ct);
            await PublishAsync($"{groupId}_GroupMessageEdited", dto, ct);
            return ApiResponse<GroupMessageDto>.Success(dto, "Message edited.");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid groupId, Guid messageId, Guid senderId, CancellationToken ct = default)
        {
            var message = await _unitOfWork.GroupMessages.Find(m => m.Id == messageId && m.GroupId == groupId).FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.MessageType == MessageType.System) return ApiResponse<bool>.Fail("System messages cannot be deleted.");
            if (message.SenderId != senderId) return ApiResponse<bool>.Fail("You can only delete your own messages.");

            message.Deleted = true;
            message.Content = null;
            message.FileUrl = null;
            _unitOfWork.GroupMessages.Update(message);
            await _unitOfWork.CompleteAsync(ct);

            await PublishAsync($"{groupId}_GroupMessageDeleted", message.Id, ct);
            return ApiResponse<bool>.Success(true, "Message deleted.");
        }

        public async Task<ApiResponse<GroupMessageDto>> SetPinnedAsync(Guid groupId, Guid messageId, Guid actorId, bool pin, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanPinMessage, ct))
                return ApiResponse<GroupMessageDto>.Fail("Only admins and the owner can pin messages.");

            var (message, group, members) = await LoadForOperationAsync(groupId, messageId, actorId, ct);
            if (message is null) return ApiResponse<GroupMessageDto>.Fail("Message not found.");
            if (message.MessageType == MessageType.System) return ApiResponse<GroupMessageDto>.Fail("System messages cannot be pinned.");

            message.IsPinned = pin;
            message.PinnedAt = pin ? DateTime.UtcNow : null;
            message.PinnedBy = pin ? actorId : null;
            _unitOfWork.GroupMessages.Update(message);
            await _unitOfWork.CompleteAsync(ct);

            var dto = await ToDtoAsync(message, group, members.Count, ct);
            await PublishAsync($"{groupId}_GroupMessagePinned", dto, ct);
            return ApiResponse<GroupMessageDto>.Success(dto, pin ? "Message pinned." : "Message unpinned.");
        }

        public async Task<ApiResponse<bool>> ToggleReactionAsync(Guid groupId, Guid messageId, Guid userId, string emoji, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(emoji))
                return ApiResponse<bool>.Fail("Emoji is required.");

            var message = await _unitOfWork.GroupMessages.Find(m => m.Id == messageId && m.GroupId == groupId).FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.MessageType == MessageType.System) return ApiResponse<bool>.Fail("System messages cannot be reacted to.");
            if (!await _permissions.IsMemberAsync(groupId, userId, ct)) return ApiResponse<bool>.Fail("You are not a member of this group.");

            var existing = await _unitOfWork.Reactions
                .Find(r => r.GroupMessageId == messageId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                if (existing.Emoji == emoji)
                {
                    _unitOfWork.Reactions.Remove(existing);
                    await _unitOfWork.CompleteAsync(ct);
                    await PublishAsync($"{groupId}_GroupMessageReactionRemoved", messageId, ct);
                    return ApiResponse<bool>.Success(false, "Reaction removed.");
                }

                existing.Emoji = emoji;
                _unitOfWork.Reactions.Update(existing);
                await _unitOfWork.CompleteAsync(ct);
                await PublishAsync($"{groupId}_GroupMessageReactionAdded", messageId, ct);
                return ApiResponse<bool>.Success(true, "Reaction changed.");
            }

            await _unitOfWork.Reactions.AddAsync(new Reaction
            {
                GroupMessageId = messageId,
                UserId = userId,
                Emoji = emoji
            });
            await _unitOfWork.CompleteAsync(ct);

            var members = await LoadMembersAsync(groupId, ct);
            var reactor = members.FirstOrDefault(m => m.UserId == userId);
            await PublishAsync($"{groupId}_GroupMessageReactionAdded", messageId, ct);
            return ApiResponse<bool>.Success(true, "Reaction added.");
        }

        public async Task<ApiResponse<bool>> RemoveReactionAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default)
        {
            var reaction = await _unitOfWork.Reactions
                .Find(r => r.GroupMessageId == messageId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);
            if (reaction is null) return ApiResponse<bool>.Fail("Reaction not found.");

            _unitOfWork.Reactions.Remove(reaction);
            await _unitOfWork.CompleteAsync(ct);
            await PublishAsync($"{groupId}_GroupMessageReactionRemoved", messageId, ct);
            return ApiResponse<bool>.Success(true, "Reaction removed.");
        }

        public async Task<ApiResponse<bool>> MarkDeliveredAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default)
        {
            var message = await _unitOfWork.GroupMessages.Find(m => m.Id == messageId && m.GroupId == groupId).FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.SenderId == userId) return ApiResponse<bool>.Success(true, "Nothing to do.");

            var read = await _unitOfWork.GroupMessageReads
                .Find(r => r.MessageId == messageId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);

            if (read is null)
            {
                read = new GroupMessageRead { MessageId = messageId, UserId = userId, DeliveredAt = DateTime.UtcNow };
                await _unitOfWork.GroupMessageReads.AddAsync(read);
            }
            else
            {
                read.DeliveredAt ??= DateTime.UtcNow;
                _unitOfWork.GroupMessageReads.Update(read);
            }
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Message marked delivered.");
        }

        public async Task<ApiResponse<bool>> MarkReadAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default)
        {
            var message = await _unitOfWork.GroupMessages.Find(m => m.Id == messageId && m.GroupId == groupId).FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<bool>.Fail("Message not found.");
            if (message.SenderId == userId) return ApiResponse<bool>.Success(true, "Nothing to do.");

            var now = DateTime.UtcNow;
            var read = await _unitOfWork.GroupMessageReads
                .Find(r => r.MessageId == messageId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);

            if (read is null)
            {
                await _unitOfWork.GroupMessageReads.AddAsync(new GroupMessageRead { MessageId = messageId, UserId = userId, DeliveredAt = now, ReadAt = now });
            }
            else
            {
                read.ReadAt = now;
                read.DeliveredAt ??= now;
                _unitOfWork.GroupMessageReads.Update(read);
            }
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Message marked read.");
        }

        public async Task<ApiResponse<bool>> MarkAllReadAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");

            membership.LastReadAt = DateTime.UtcNow;
            _unitOfWork.ChatGroupMembers.Update(membership);

            var unread = await _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId && m.SenderId != userId && !m.Deleted)
                .ToListAsync(ct);
            var now = DateTime.UtcNow;
            foreach (var message in unread)
            {
                var existing = await _unitOfWork.GroupMessageReads
                    .Find(r => r.MessageId == message.Id && r.UserId == userId)
                    .FirstOrDefaultAsync(ct);
                if (existing is null)
                {
                    await _unitOfWork.GroupMessageReads.AddAsync(new GroupMessageRead { MessageId = message.Id, UserId = userId, DeliveredAt = now, ReadAt = now });
                }
                else
                {
                    existing.ReadAt = now;
                    existing.DeliveredAt ??= now;
                    _unitOfWork.GroupMessageReads.Update(existing);
                }
            }

            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "All messages marked read.");
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMessagesAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<PaginatedResult<GroupMessageDto>>.Fail("You are not a member of this group.");

            (page, pageSize) = Normalize(page, pageSize);
            var query = _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId)
                .OrderByDescending(m => m.CreatedAt);

            var total = await query.CountAsync(ct);
            var messages = await query
                .Include(m => m.Sender)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<ApiResponse<GroupMessageDto>> GetMessageAsync(Guid groupId, Guid messageId, Guid userId, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<GroupMessageDto>.Fail("You are not a member of this group.");

            var message = await _unitOfWork.GroupMessages
                .Find(m => m.Id == messageId && m.GroupId == groupId)
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(ct);
            if (message is null) return ApiResponse<GroupMessageDto>.Fail("Message not found.");

            var members = await LoadMembersAsync(groupId, ct);
            var dto = await ToDtoAsync(message, message.Group ?? await _unitOfWork.ChatGroups.GetByIdAsync(groupId), members.Count, ct);
            return ApiResponse<GroupMessageDto>.Success(dto);
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetPinnedMessagesAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<PaginatedResult<GroupMessageDto>>.Fail("You are not a member of this group.");

            (page, pageSize) = Normalize(page, pageSize);
            var query = _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId && m.IsPinned)
                .OrderByDescending(m => m.PinnedAt);

            var total = await query.CountAsync(ct);
            var messages = await query.Include(m => m.Sender).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> SearchAsync(Guid groupId, Guid userId, GroupMessageSearchInput input, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<PaginatedResult<GroupMessageDto>>.Fail("You are not a member of this group.");

            (var page, var pageSize) = Normalize(input.Page, input.PageSize);
            var query = _unitOfWork.GroupMessages.Find(m => m.GroupId == groupId && !m.Deleted);

            if (!string.IsNullOrWhiteSpace(input.Text))
                query = query.Where(m => m.Content != null && EF.Functions.Like(m.Content, $"%{input.Text.Trim()}%"));
            if (input.SenderId.HasValue)
                query = query.Where(m => m.SenderId == input.SenderId.Value);
            if (input.MentionedUserId.HasValue)
                query = query.Where(m => m.Mentions.Any(mn => mn.UserId == input.MentionedUserId.Value));
            if (input.Pinned.HasValue)
                query = query.Where(m => m.IsPinned == input.Pinned.Value);
            if (input.MediaType.HasValue)
                query = query.Where(m => m.MessageType == input.MediaType.Value);
            if (input.DateFrom.HasValue)
                query = query.Where(m => m.CreatedAt >= input.DateFrom.Value);
            if (input.DateTo.HasValue)
                query = query.Where(m => m.CreatedAt <= input.DateTo.Value);
            if (input.HasReactions.HasValue)
                query = input.HasReactions.Value
                    ? query.Where(m => m.Reactions.Any())
                    : query.Where(m => !m.Reactions.Any());
            if (input.RepliesOnly.HasValue && input.RepliesOnly.Value)
                query = query.Where(m => m.ReplyToMessageId != null);

            query = query.OrderByDescending(m => m.CreatedAt);
            var total = await query.CountAsync(ct);
            var messages = await query.Include(m => m.Sender).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMediaAsync(Guid groupId, Guid userId, MessageType? mediaType, int page, int pageSize, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<PaginatedResult<GroupMessageDto>>.Fail("You are not a member of this group.");

            var mediaTypes = new[] { MessageType.Image, MessageType.Video, MessageType.Document, MessageType.Audio };
            (page, pageSize) = Normalize(page, pageSize);
            var query = _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId && mediaTypes.Contains(m.MessageType) && !m.Deleted);

            if (mediaType.HasValue)
                query = query.Where(m => m.MessageType == mediaType.Value);

            query = query.OrderByDescending(m => m.CreatedAt);
            var total = await query.CountAsync(ct);
            var messages = await query.Include(m => m.Sender).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<ApiResponse<int>> GetUnreadCountAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<int>.Fail("You are not a member of this group.");

            var since = membership.LastReadAt;
            var count = await _unitOfWork.GroupMessages
                .Find(m => m.GroupId == groupId && m.SenderId != userId && !m.Deleted)
                .Where(m => since == null || m.CreatedAt > since.Value)
                .CountAsync(ct);

            return ApiResponse<int>.Success(count);
        }

        public async Task<ApiResponse<int>> GetUnreadGroupCountAsync(Guid userId, CancellationToken ct = default)
        {
            var counts = await GetUnreadCountsByGroupAsync(userId, ct);
            return ApiResponse<int>.Success(counts.Values.Sum());
        }

        public async Task<Dictionary<Guid, int>> GetUnreadCountsByGroupAsync(Guid userId, CancellationToken ct = default)
        {
            var query =
                from m in _unitOfWork.GroupMessages.GetAll()
                join mem in _unitOfWork.ChatGroupMembers.GetAll() on m.GroupId equals mem.GroupId
                where mem.UserId == userId && m.SenderId != userId && !m.Deleted
                      && (mem.LastReadAt == null || m.CreatedAt > mem.LastReadAt)
                group m by m.GroupId into g
                select new { GroupId = g.Key, Count = g.Count() };

            var rows = await query.ToListAsync(ct);
            return rows.ToDictionary(r => r.GroupId, r => r.Count);
        }

        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMyMentionsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            (page, pageSize) = Normalize(page, pageSize);
            var query = _unitOfWork.GroupMessageMentions
                .Find(mn => mn.UserId == userId)
                .Select(mn => mn.Message)
                .Distinct()
                .OrderByDescending(m => m.CreatedAt);

            var total = await query.CountAsync(ct);
            var messages = await query.Include(m => m.Sender).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var dtos = await ToDtosAsync(messages, ct);
            return ApiResponse<PaginatedResult<GroupMessageDto>>.Success(
                PaginatedResult<GroupMessageDto>.Create(dtos, page, pageSize, total));
        }

        public async Task<GroupMessage> InsertSystemMessageAsync(ChatGroup group, Guid actorId, string content, string? metadata = null, CancellationToken ct = default)
        {
            var message = new GroupMessage
            {
                GroupId = group.Id,
                SenderId = actorId,
                MessageType = MessageType.System,
                Content = content,
                Metadata = metadata,
                Status = MessageStatus.Sent
            };

            await _unitOfWork.GroupMessages.AddAsync(message);
            group.LastMessageId = message.Id;
            group.LastActivityAt = DateTime.UtcNow;
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);
            await _unitOfWork.CompleteAsync(ct);

            await PublishAsync($"{group.Id}_GroupMessage", ToMessageDto(message), ct);
            return message;
        }

        private async Task<(GroupMessage? Message, ChatGroup? Group, List<ChatGroupMember> Members)> LoadForOperationAsync(
            Guid groupId, Guid messageId, Guid actorId, CancellationToken ct)
        {
            var message = await _unitOfWork.GroupMessages
                .Find(m => m.Id == messageId && m.GroupId == groupId)
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(ct);
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            var members = await LoadMembersAsync(groupId, ct);
            return (message, group, members);
        }

        private async Task<List<ChatGroupMember>> LoadMembersAsync(Guid groupId, CancellationToken ct) =>
            await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId)
                .Include(m => m.User)
                .ToListAsync(ct);

        private async Task CreateMessageNotificationAsync(
            Guid userId, NotificationType type, GroupMessage message, ChatGroup group, CancellationToken ct)
        {
            var metadata = JsonSerializer.Serialize(new
            {
                groupId = group.Id,
                groupName = group.Name,
                messageId = message.Id,
                senderId = message.SenderId,
                preview = message.Content ?? message.MessageType.ToString()
            });

            await _notificationService.CreateAsync(
                userId,
                type,
                $"{type}: {group.Name}",
                message.Id,
                (int)type,
                metadata,
                ct);
        }

        private static bool ShouldNotify(ChatGroupMember member) =>
            member.NotificationLevel is NotificationLevel.All or NotificationLevel.MentionsOnly &&
            (!member.Muted || member.MutedUntil == null || member.MutedUntil.Value <= DateTime.UtcNow);

        private async Task<List<GroupMessageDto>> ToDtosAsync(List<GroupMessage> messages, CancellationToken ct)
        {
            if (messages.Count == 0) return [];

            var groupIds = messages.Select(m => m.GroupId).Distinct().ToList();
            var memberCounts = await _unitOfWork.ChatGroupMembers
                .Find(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var countsByGroup = memberCounts.ToDictionary(c => c.GroupId, c => c.Count);

            var messageIds = messages.Select(m => m.Id).ToList();
            var reads = await _unitOfWork.GroupMessageReads
                .Find(r => messageIds.Contains(r.MessageId))
                .ToListAsync(ct);

            var dtos = messages.Select(m => ToMessageDto(m)).ToList();
            foreach (var dto in dtos)
            {
                var messageReads = reads.Where(r => r.MessageId == dto.Id).ToList();
                dto.DeliveredCount = messageReads.Count(r => r.DeliveredAt != null);
                dto.ReadCount = messageReads.Count(r => r.ReadAt != null);
                dto.UnreadCount = Math.Max(0, countsByGroup.GetValueOrDefault(dto.GroupId) - dto.ReadCount);
            }

            return dtos;
        }

        private async Task<GroupMessageDto> ToDtoAsync(GroupMessage message, ChatGroup? group, int memberCount, CancellationToken ct)
        {
            var dtos = await ToDtosAsync([message], ct);
            return dtos.FirstOrDefault() ?? ToMessageDto(message);
        }

        private GroupMessageDto ToMessageDto(GroupMessage message) => _mapper.Map<GroupMessageDto>(message);

        private static (int Page, int PageSize) Normalize(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = DefaultPageSize;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;
            return (page, pageSize);
        }

        private async Task PublishAsync(string topic, object payload, CancellationToken ct)
        {
            try
            {
                await _eventSender.SendAsync(topic, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish event to topic {Topic}.", topic);
            }
        }
    }
}
```

Note: `GroupMessageService` has no dependency on `GroupService`/`GroupCallService` (no cycles). `INotificationService` is referenced from `BlogGraphQlApp.Core.Interfaces` (add `using BlogGraphQlApp.Core.Interfaces;` if the compiler complains about the interface namespace — the file already imports `BlogGraphQlApp.DTOs` which contains `NotificationDto`).

- [ ] **Step 3: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add Services/Groups/IGroupMessageService.cs Services/Groups/GroupMessageService.cs
git commit -m "feat: add group message service (send, edit, delete, pin, react, reads, search, media, mentions)"
```

### Task 11: GroupService extensions

**Files:**
- Modify: `Services/Groups/IGroupService.cs`, `Services/Groups/GroupService.cs`

- [ ] **Step 1: Replace `Services/Groups/IGroupService.cs`**

```csharp
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Storage;

namespace BlogGraphQlApp.Services.Groups
{
    public interface IGroupService
    {
        Task<ApiResponse<GroupDto>> CreateGroupAsync(Guid ownerId, string name, string? description, bool isPrivate, int? maxMembers, string? imageUrl, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> UpdateGroupAsync(Guid groupId, Guid actorId, string? name, string? description, bool? isPrivate, bool? archived, int? maxMembers, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> UploadGroupImageAsync(Guid groupId, Guid actorId, IFile file, CancellationToken ct = default);
        Task<ApiResponse<bool>> DeleteGroupAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> TransferOwnershipAsync(Guid groupId, Guid actorId, Guid targetUserId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupDto>>> GetGroupsAsync(Guid userId, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> GetGroupAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> AddMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> RemoveMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> LeaveGroupAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> PromoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> DemoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupMemberDto>>> GetMembersAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<string>> GenerateInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<bool>> RevokeInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<GroupDto>> JoinByInviteAsync(string inviteCode, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> RequestJoinAsync(Guid groupId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ApproveJoinRequestAsync(Guid groupId, Guid actorId, Guid requestId, CancellationToken ct = default);
        Task<ApiResponse<bool>> RejectJoinRequestAsync(Guid groupId, Guid actorId, Guid requestId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupJoinRequestDto>>> GetPendingJoinRequestsAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<string>> GetInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<bool>> MuteGroupAsync(Guid groupId, Guid userId, DateTime? mutedUntil, CancellationToken ct = default);
        Task<ApiResponse<bool>> SetNotificationLevelAsync(Guid groupId, Guid userId, NotificationLevel level, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Replace `Services/Groups/GroupService.cs`**

```csharp
using System.Text.Json;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Repositories.Interfaces;
using BlogGraphQlApp.Storage;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Services.Groups
{
    public class GroupService : IGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly GroupPermissionService _permissions;
        private readonly IGroupMessageService _messageService;
        private readonly INotificationService _notificationService;
        private readonly IFileStorage _fileStorage;
        private readonly PresenceTracker _presence;
        private readonly ITopicEventSender _eventSender;
        private readonly ILogger<GroupService> _logger;

        public GroupService(
            IUnitOfWork unitOfWork,
            GroupPermissionService permissions,
            IGroupMessageService messageService,
            INotificationService notificationService,
            IFileStorage fileStorage,
            PresenceTracker presence,
            ITopicEventSender eventSender,
            ILogger<GroupService> logger)
        {
            _unitOfWork = unitOfWork;
            _permissions = permissions;
            _messageService = messageService;
            _notificationService = notificationService;
            _fileStorage = fileStorage;
            _presence = presence;
            _eventSender = eventSender;
            _logger = logger;
        }

        public async Task<ApiResponse<GroupDto>> CreateGroupAsync(Guid ownerId, string name, string? description, bool isPrivate, int? maxMembers, string? imageUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ApiResponse<GroupDto>.Fail("Group name is required.");

            var group = new ChatGroup
            {
                Name = name.Trim(),
                Description = description?.Trim(),
                ImageUrl = imageUrl,
                IsPrivate = isPrivate,
                MaxMembers = maxMembers,
                InviteCode = GenerateInviteCode(),
                UpdatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.ChatGroups.AddAsync(group);
            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = group.Id,
                UserId = ownerId,
                Role = GroupMemberRole.Owner,
                LastReadAt = DateTime.UtcNow
            });
            await _unitOfWork.CompleteAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Group {GroupId} created by {UserId}.", group.Id, ownerId);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, ownerId, ct), "Group created.");
        }

        public async Task<ApiResponse<GroupDto>> UpdateGroupAsync(Guid groupId, Guid actorId, string? name, string? description, bool? isPrivate, bool? archived, int? maxMembers, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<GroupDto>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanUpdateGroup, ct))
                return ApiResponse<GroupDto>.Fail("You do not have permission to update this group.");

            var changes = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(name) && group.Name != name.Trim())
            {
                changes.Append($"Name changed to \"{name.Trim()}\". ");
                group.Name = name.Trim();
            }
            if (description is not null && group.Description != description.Trim())
            {
                changes.Append("Description changed. ");
                group.Description = description.Trim();
            }
            if (isPrivate.HasValue && group.IsPrivate != isPrivate.Value)
            {
                changes.Append($"Group is now {(isPrivate.Value ? "private" : "public")}. ");
                group.IsPrivate = isPrivate.Value;
            }
            if (archived.HasValue)
                group.Archived = archived.Value;
            if (maxMembers.HasValue)
                group.MaxMembers = maxMembers.Value;

            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);

            try
            {
                await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
                await _unitOfWork.CompleteAsync(ct);
                if (changes.Length > 0)
                    await _messageService.InsertSystemMessageAsync(group, actorId, changes.ToString().Trim(), JsonSerializer.Serialize(new { actorId }), ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating group {GroupId}.", groupId);
                return ApiResponse<GroupDto>.Fail("This group was modified by someone else. Refresh and try again.");
            }

            await PublishAsync($"{groupId}_GroupUpdated", await ToGroupDtoAsync(group, actorId, ct), ct);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, actorId, ct), "Group updated.");
        }

        public async Task<ApiResponse<GroupDto>> UploadGroupImageAsync(Guid groupId, Guid actorId, IFile file, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<GroupDto>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanChangeImage, ct))
                return ApiResponse<GroupDto>.Fail("Only admins and the owner can change the group image.");

            var oldUrl = group.ImageUrl;
            var newUrl = await _fileStorage.UploadAsync(file, "groupimages");
            group.ImageUrl = newUrl;
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, "Group image updated.", JsonSerializer.Serialize(new { actorId }), ct);
            await tx.CommitAsync(ct);

            if (oldUrl is not null)
                await _fileStorage.DeleteAsync(oldUrl);

            await PublishAsync($"{groupId}_GroupUpdated", await ToGroupDtoAsync(group, actorId, ct), ct);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, actorId, ct), "Group image updated.");
        }

        public async Task<ApiResponse<bool>> DeleteGroupAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanDeleteGroup, ct))
                return ApiResponse<bool>.Fail("Only the group owner can delete the group.");

            _unitOfWork.ChatGroups.Remove(group);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Group deleted.");
        }

        public async Task<ApiResponse<GroupDto>> TransferOwnershipAsync(Guid groupId, Guid actorId, Guid targetUserId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<GroupDto>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanTransferOwnership, ct))
                return ApiResponse<GroupDto>.Fail("Only the owner can transfer ownership.");

            var target = await _permissions.GetMembershipAsync(groupId, targetUserId, ct);
            if (target is null) return ApiResponse<GroupDto>.Fail("Target user is not a member of this group.");
            if (target.UserId == actorId) return ApiResponse<GroupDto>.Fail("You already own this group.");

            var actorMembership = await _permissions.GetMembershipAsync(groupId, actorId, ct);
            actorMembership!.Role = GroupMemberRole.Admin;
            target.Role = GroupMemberRole.Owner;
            group.CreatedBy = targetUserId;
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroupMembers.Update(actorMembership);
            _unitOfWork.ChatGroupMembers.Update(target);
            _unitOfWork.ChatGroups.Update(group);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"Ownership transferred to {target.User.FullName}.", JsonSerializer.Serialize(new { actorId, targetUserId }), ct);
            await _notificationService.CreateAsync(targetUserId, NotificationType.GroupRoleChanged, $"You are now the owner of {group.Name}.", group.Id, (int)NotificationType.GroupRoleChanged, null, ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupUpdated", await ToGroupDtoAsync(group, actorId, ct), ct);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, actorId, ct), "Ownership transferred.");
        }

        public async Task<ApiResponse<IEnumerable<GroupDto>>> GetGroupsAsync(Guid userId, CancellationToken ct = default)
        {
            var memberships = await _unitOfWork.ChatGroupMembers
                .Find(m => m.UserId == userId)
                .Include(m => m.Group)
                .ToListAsync(ct);

            if (memberships.Count == 0)
                return ApiResponse<IEnumerable<GroupDto>>.Success([]);

            var groupIds = memberships.Select(m => m.GroupId).ToList();

            var lastMessages = await _unitOfWork.GroupMessages
                .Find(m => groupIds.Contains(m.GroupId))
                .Include(m => m.Sender)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync(ct);
            var lastByGroup = lastMessages
                .GroupBy(m => m.GroupId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First());

            var memberCounts = await _unitOfWork.ChatGroupMembers
                .Find(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var counts = memberCounts.ToDictionary(c => c.GroupId, c => c.Count);

            var unread = await _messageService.GetUnreadCountsByGroupAsync(userId, ct);

            var dtos = new List<GroupDto>();
            foreach (var membership in memberships)
            {
                var last = lastByGroup.GetValueOrDefault(membership.GroupId);
                dtos.Add(ToGroupDto(
                    membership.Group,
                    counts.GetValueOrDefault(membership.GroupId),
                    unread.GetValueOrDefault(membership.GroupId),
                    last,
                    last?.Sender,
                    inviteCode: null));
            }

            return ApiResponse<IEnumerable<GroupDto>>.Success(dtos);
        }

        public async Task<ApiResponse<GroupDto>> GetGroupAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups
                .Find(g => g.Id == groupId)
                .Include(g => g.CreatedByUser)
                .FirstOrDefaultAsync(ct);
            if (group is null) return ApiResponse<GroupDto>.Fail("Group not found.");
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<GroupDto>.Fail("You are not a member of this group.");

            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, userId, ct));
        }

        public async Task<ApiResponse<bool>> AddMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanAddMember, ct))
                return ApiResponse<bool>.Fail("You do not have permission to add members.");

            var target = await _unitOfWork.Users.GetByIdAsync(userId);
            if (target is null) return ApiResponse<bool>.Fail("User not found.");

            var alreadyMember = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (alreadyMember is not null) return ApiResponse<bool>.Fail("User is already a member of this group.");

            var count = await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == groupId);
            if (group.MaxMembers.HasValue && count >= group.MaxMembers.Value)
                return ApiResponse<bool>.Fail("The group has reached its member limit.");

            if (!await AreFriendsAsync(actorId, userId))
                return ApiResponse<bool>.Fail("You can only add friends to a group.");

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = groupId,
                UserId = userId,
                Role = GroupMemberRole.Member,
                LastReadAt = DateTime.UtcNow
            });
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"{target.FullName} added to the group.", JsonSerializer.Serialize(new { actorId, userId }), ct);
            await _notificationService.CreateAsync(userId, NotificationType.GroupMemberAdded, $"You were added to {group.Name}.", group.Id, (int)NotificationType.GroupMemberAdded, JsonSerializer.Serialize(new { groupId, groupName = group.Name, imageUrl = group.ImageUrl, addedBy = actorId }), ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupMemberJoined", await ToMemberDtoAsync(userId, ct), ct);
            return ApiResponse<bool>.Success(true, "Member added.");
        }

        public async Task<ApiResponse<bool>> RemoveMemberAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");

            var actorMembership = await _permissions.GetMembershipAsync(groupId, actorId, ct);
            var targetMembership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (actorMembership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");
            if (targetMembership is null) return ApiResponse<bool>.Fail("User is not a member of this group.");
            if (!GroupPermissions.CanRemoveMember(actorMembership.Role, targetMembership.Role))
                return ApiResponse<bool>.Fail("You do not have permission to remove this member.");

            var target = await _unitOfWork.Users.GetByIdAsync(userId);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            _unitOfWork.ChatGroupMembers.Remove(targetMembership);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"{target?.FullName ?? "A member"} was removed from the group.", JsonSerializer.Serialize(new { actorId, userId }), ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupMemberLeft", await ToMemberDtoAsync(userId, ct), ct);
            return ApiResponse<bool>.Success(true, "Member removed.");
        }

        public async Task<ApiResponse<bool>> LeaveGroupAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");

            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");
            if (membership.Role == GroupMemberRole.Owner)
                return ApiResponse<bool>.Fail("The owner cannot leave; transfer ownership or delete the group.");

            var remainingMembers = await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId && m.UserId != userId)
                .Include(m => m.User)
                .ToListAsync(ct);

            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            _unitOfWork.ChatGroupMembers.Remove(membership);
            await _unitOfWork.CompleteAsync(ct);

            if (remainingMembers.Count == 0)
            {
                group.Archived = true;
                group.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.ChatGroups.Update(group);
                await _unitOfWork.CompleteAsync(ct);
            }
            else
            {
                await _messageService.InsertSystemMessageAsync(group, userId, $"{user?.FullName ?? "A member"} left the group.", JsonSerializer.Serialize(new { userId }), ct);
                foreach (var remaining in remainingMembers)
                {
                    await _notificationService.CreateAsync(remaining.UserId, NotificationType.GroupUpdated, $"{user?.FullName ?? "A member"} left {group.Name}.", group.Id, (int)NotificationType.GroupUpdated, null, ct);
                }
            }
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupMemberLeft", await ToMemberDtoAsync(userId, ct), ct);
            return ApiResponse<bool>.Success(true, "You left the group.");
        }

        public async Task<ApiResponse<bool>> PromoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default)
            => await ChangeRoleAsync(groupId, actorId, userId, GroupMemberRole.Admin, "promoted to admin", ct);

        public async Task<ApiResponse<bool>> DemoteAdminAsync(Guid groupId, Guid actorId, Guid userId, CancellationToken ct = default)
            => await ChangeRoleAsync(groupId, actorId, userId, GroupMemberRole.Member, "demoted to member", ct);

        public async Task<ApiResponse<IEnumerable<GroupMemberDto>>> GetMembersAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            if (!await _permissions.IsMemberAsync(groupId, userId, ct))
                return ApiResponse<IEnumerable<GroupMemberDto>>.Fail("You are not a member of this group.");

            var members = await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == groupId)
                .Include(m => m.User)
                .OrderBy(m => m.JoinedAt)
                .ToListAsync(ct);

            var dtos = new List<GroupMemberDto>();
            foreach (var member in members)
            {
                dtos.Add(new GroupMemberDto
                {
                    Id = member.Id,
                    GroupId = member.GroupId,
                    UserId = member.UserId,
                    Username = member.User.Username,
                    FullName = member.User.FullName,
                    Avatar = member.User.ProfilePictureUrl,
                    Role = member.Role.ToString(),
                    JoinedAt = member.JoinedAt,
                    Online = await _presence.IsOnline(member.UserId),
                    LastSeen = member.User.LastSeen
                });
            }

            return ApiResponse<IEnumerable<GroupMemberDto>>.Success(dtos);
        }

        public async Task<ApiResponse<string>> GenerateInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageInvite, ct))
                return ApiResponse<string>.Fail("Only admins and the owner can manage the invite link.");

            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<string>.Fail("Group not found.");

            group.InviteCode = GenerateInviteCode();
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, "Invite link regenerated.", JsonSerializer.Serialize(new { actorId }), ct);
            await tx.CommitAsync(ct);

            return ApiResponse<string>.Success(group.InviteCode, "Invite code generated.");
        }

        public async Task<ApiResponse<bool>> RevokeInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageInvite, ct))
                return ApiResponse<bool>.Fail("Only admins and the owner can manage the invite link.");

            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");

            group.InviteCode = null;
            group.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ChatGroups.Update(group);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Invite code revoked.");
        }

        public async Task<ApiResponse<GroupDto>> JoinByInviteAsync(string inviteCode, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups
                .Find(g => g.InviteCode == inviteCode)
                .FirstOrDefaultAsync(ct);
            if (group is null) return ApiResponse<GroupDto>.Fail("Invalid invite code.");
            if (group.IsPrivate) return ApiResponse<GroupDto>.Fail("This group is private; request to join instead.");
            if (await _permissions.GetMembershipAsync(group.Id, userId, ct) is not null)
                return ApiResponse<GroupDto>.Fail("You are already a member of this group.");

            var count = await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == group.Id);
            if (group.MaxMembers.HasValue && count >= group.MaxMembers.Value)
                return ApiResponse<GroupDto>.Fail("The group has reached its member limit.");

            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = group.Id,
                UserId = userId,
                Role = GroupMemberRole.Member,
                LastReadAt = DateTime.UtcNow
            });
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, userId, $"{user?.FullName ?? "A member"} joined the group.", JsonSerializer.Serialize(new { userId }), ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{group.Id}_GroupMemberJoined", await ToMemberDtoAsync(userId, ct), ct);
            return ApiResponse<GroupDto>.Success(await ToGroupDtoAsync(group, userId, ct), "Joined group.");
        }

        public async Task<ApiResponse<bool>> RequestJoinAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");
            if (!group.IsPrivate) return ApiResponse<bool>.Fail("This group is public; join directly.");

            var existing = await _unitOfWork.GroupJoinRequests
                .Find(r => r.GroupId == groupId && r.UserId == userId)
                .FirstOrDefaultAsync(ct);
            if (existing is not null)
                return ApiResponse<bool>.Fail(existing.Status == JoinRequestStatus.Pending ? "Your request is pending." : "You have already requested to join this group.");

            await _unitOfWork.GroupJoinRequests.AddAsync(new GroupJoinRequest { GroupId = groupId, UserId = userId });
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Join request submitted.");
        }

        public async Task<ApiResponse<bool>> ApproveJoinRequestAsync(Guid groupId, Guid actorId, Guid requestId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageJoinRequests, ct))
                return ApiResponse<bool>.Fail("Only admins and the owner can approve join requests.");

            var request = await _unitOfWork.GroupJoinRequests
                .Find(r => r.Id == requestId && r.GroupId == groupId)
                .Include(r => r.User)
                .FirstOrDefaultAsync(ct);
            if (request is null || request.Status != JoinRequestStatus.Pending)
                return ApiResponse<bool>.Fail("Join request not found.");

            var count = await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == groupId);
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group!.MaxMembers.HasValue && count >= group.MaxMembers.Value)
                return ApiResponse<bool>.Fail("The group has reached its member limit.");

            request.Status = JoinRequestStatus.Approved;
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedBy = actorId;
            _unitOfWork.GroupJoinRequests.Update(request);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.ChatGroupMembers.AddAsync(new ChatGroupMember
            {
                GroupId = groupId,
                UserId = request.UserId,
                Role = GroupMemberRole.Member,
                LastReadAt = DateTime.UtcNow
            });
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"{request.User.FullName} joined the group.", JsonSerializer.Serialize(new { actorId, userId = request.UserId }), ct);
            await _notificationService.CreateAsync(request.UserId, NotificationType.GroupMemberAdded, $"Your request to join {group.Name} was approved.", group.Id, (int)NotificationType.GroupMemberAdded, null, ct);
            await tx.CommitAsync(ct);

            await PublishAsync($"{groupId}_GroupMemberJoined", await ToMemberDtoAsync(request.UserId, ct), ct);
            return ApiResponse<bool>.Success(true, "Join request approved.");
        }

        public async Task<ApiResponse<bool>> RejectJoinRequestAsync(Guid groupId, Guid actorId, Guid requestId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageJoinRequests, ct))
                return ApiResponse<bool>.Fail("Only admins and the owner can reject join requests.");

            var request = await _unitOfWork.GroupJoinRequests
                .Find(r => r.Id == requestId && r.GroupId == groupId)
                .FirstOrDefaultAsync(ct);
            if (request is null || request.Status != JoinRequestStatus.Pending)
                return ApiResponse<bool>.Fail("Join request not found.");

            request.Status = JoinRequestStatus.Rejected;
            request.ResolvedAt = DateTime.UtcNow;
            request.ResolvedBy = actorId;
            _unitOfWork.GroupJoinRequests.Update(request);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Join request rejected.");
        }

        public async Task<ApiResponse<IEnumerable<GroupJoinRequestDto>>> GetPendingJoinRequestsAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageJoinRequests, ct))
                return ApiResponse<IEnumerable<GroupJoinRequestDto>>.Fail("Only admins and the owner can view join requests.");

            var requests = await _unitOfWork.GroupJoinRequests
                .Find(r => r.GroupId == groupId && r.Status == JoinRequestStatus.Pending)
                .Include(r => r.User)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync(ct);

            var dtos = requests.Select(r => new GroupJoinRequestDto
            {
                Id = r.Id,
                GroupId = r.GroupId,
                UserId = r.UserId,
                Username = r.User.Username,
                FullName = r.User.FullName,
                Avatar = r.User.ProfilePictureUrl,
                Status = r.Status,
                RequestedAt = r.RequestedAt
            });
            return ApiResponse<IEnumerable<GroupJoinRequestDto>>.Success(dtos);
        }

        public async Task<ApiResponse<string>> GetInviteCodeAsync(Guid groupId, Guid actorId, CancellationToken ct = default)
        {
            if (!await _permissions.CanAsync(groupId, actorId, GroupPermissions.CanManageInvite, ct))
                return ApiResponse<string>.Fail("Only admins and the owner can view the invite code.");

            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<string>.Fail("Group not found.");
            return ApiResponse<string>.Success(group.InviteCode ?? string.Empty);
        }

        public async Task<ApiResponse<bool>> MuteGroupAsync(Guid groupId, Guid userId, DateTime? mutedUntil, CancellationToken ct = default)
        {
            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");

            membership.Muted = mutedUntil is null;
            membership.MutedUntil = mutedUntil;
            _unitOfWork.ChatGroupMembers.Update(membership);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, mutedUntil is null ? "Group muted." : $"Group muted until {mutedUntil:u}.");
        }

        public async Task<ApiResponse<bool>> SetNotificationLevelAsync(Guid groupId, Guid userId, NotificationLevel level, CancellationToken ct = default)
        {
            var membership = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (membership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");

            membership.NotificationLevel = level;
            _unitOfWork.ChatGroupMembers.Update(membership);
            await _unitOfWork.CompleteAsync(ct);
            return ApiResponse<bool>.Success(true, "Notification level updated.");
        }

        private async Task<ApiResponse<bool>> ChangeRoleAsync(Guid groupId, Guid actorId, Guid userId, GroupMemberRole newRole, string action, CancellationToken ct)
        {
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
            if (group is null) return ApiResponse<bool>.Fail("Group not found.");

            var actorMembership = await _permissions.GetMembershipAsync(groupId, actorId, ct);
            if (actorMembership is null) return ApiResponse<bool>.Fail("You are not a member of this group.");
            var can = newRole == GroupMemberRole.Admin
                ? GroupPermissions.CanPromoteAdmin(actorMembership.Role)
                : GroupPermissions.CanDemoteAdmin(actorMembership.Role);
            if (!can) return ApiResponse<bool>.Fail("Only the group owner can change member roles.");

            var target = await _permissions.GetMembershipAsync(groupId, userId, ct);
            if (target is null) return ApiResponse<bool>.Fail("User is not a member of this group.");
            if (target.Role == GroupMemberRole.Owner) return ApiResponse<bool>.Fail("The group owner's role cannot be changed.");
            if (target.Role == newRole)
                return ApiResponse<bool>.Fail(newRole == GroupMemberRole.Admin ? "User is already an admin." : "User is already a regular member.");

            target.Role = newRole;
            _unitOfWork.ChatGroupMembers.Update(target);
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _unitOfWork.CompleteAsync(ct);
            await _messageService.InsertSystemMessageAsync(group, actorId, $"{target.User.FullName} was {action}.", JsonSerializer.Serialize(new { actorId, userId }), ct);
            await _notificationService.CreateAsync(userId, NotificationType.GroupRoleChanged, $"You were {action} in {group.Name}.", group.Id, (int)NotificationType.GroupRoleChanged, null, ct);
            await tx.CommitAsync(ct);
            return ApiResponse<bool>.Success(true, "Role updated.");
        }

        private async Task<GroupMemberDto> ToMemberDtoAsync(Guid userId, CancellationToken ct)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            return new GroupMemberDto
            {
                Id = Guid.Empty,
                UserId = userId,
                Username = user?.Username ?? string.Empty,
                FullName = user?.FullName ?? string.Empty,
                Avatar = user?.ProfilePictureUrl,
                Online = await _presence.IsOnline(userId),
                LastSeen = user?.LastSeen
            };
        }

        private async Task<GroupDto> ToGroupDtoAsync(ChatGroup group, Guid actorId, CancellationToken ct)
        {
            var count = await _unitOfWork.ChatGroupMembers.CountAsync(m => m.GroupId == group.Id);
            var unread = await _messageService.GetUnreadCountsByGroupAsync(actorId, ct);
            var membership = await _permissions.GetMembershipAsync(group.Id, actorId, ct);
            var canViewInvite = membership is not null && GroupPermissions.CanManageInvite(membership.Role);

            var last = await _unitOfWork.GroupMessages
                .Find(m => m.GroupId == group.Id)
                .Include(m => m.Sender)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            return ToGroupDto(group, count, unread.GetValueOrDefault(group.Id), last, last?.Sender, canViewInvite ? group.InviteCode : null);
        }

        private static GroupDto ToGroupDto(ChatGroup group, int memberCount, int unreadCount, GroupMessage? lastMessage, Models.User? lastSender, string? inviteCode) => new()
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            ImageUrl = group.ImageUrl,
            IsPrivate = group.IsPrivate,
            InviteCode = inviteCode,
            LastMessageId = group.LastMessageId,
            LastMessage = lastMessage is null ? null : new GroupMessageDto
            {
                Id = lastMessage.Id,
                GroupId = lastMessage.GroupId,
                SenderId = lastMessage.SenderId,
                SenderName = lastMessage.Sender?.FullName ?? string.Empty,
                MessageType = lastMessage.MessageType,
                Content = lastMessage.Content,
                FileUrl = lastMessage.FileUrl,
                CreatedAt = lastMessage.CreatedAt,
                Deleted = lastMessage.Deleted
            },
            LastSender = lastSender is null ? null : new UserDto { Id = lastSender.Id, FullName = lastSender.FullName, Username = lastSender.Username, ProfilePictureUrl = lastSender.ProfilePictureUrl },
            LastActivityAt = group.LastActivityAt,
            UpdatedAt = group.UpdatedAt,
            Archived = group.Archived,
            MaxMembers = group.MaxMembers,
            CreatedBy = group.CreatedBy,
            CreatedByName = group.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = group.CreatedAt,
            MemberCount = memberCount,
            UnreadCount = unreadCount
        };

        private static string GenerateInviteCode() => Convert.ToHexString(Guid.NewGuid().ToByteArray())[..12].ToLowerInvariant();

        private async Task<bool> AreFriendsAsync(Guid a, Guid b) =>
            await _unitOfWork.UserFollows.AnyAsync(f => f.FollowerId == a && f.FollowingId == b) &&
            await _unitOfWork.UserFollows.AnyAsync(f => f.FollowerId == b && f.FollowingId == a);

        private async Task PublishAsync(string topic, object payload, CancellationToken ct)
        {
            try
            {
                await _eventSender.SendAsync(topic, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish event to topic {Topic}.", topic);
            }
        }
    }
}
```

Notes for the implementer:
- Add `using BlogGraphQlApp.Core.Interfaces;` (for `INotificationService`) and `using BlogGraphQlApp.Services.Implementations;` (for `PresenceTracker`), plus `using BlogGraphQlApp.Services.Interfaces;` if `PresenceTracker` isn't found there — verify the correct namespace at build time (it is `BlogGraphQlApp.Services.Implementations` per `Services/Implementations/PresenceTracker.cs`).
- `UserDto` has properties `Id`, `FullName`, `Username`, `ProfilePictureUrl` — confirm the exact property names in `Dtos/UserDto.cs` while implementing and adjust `LastSender` mapping if `ProfilePictureUrl` is named differently (e.g. `ProfilePicture`).
- `GroupMessageService`/`IGroupMessageService` live in the same `BlogGraphQlApp.Services.Groups` namespace, so no extra using is needed for them.
- The old `SendMessageAsync`/`GetMessagesAsync` methods on `GroupService` are **removed** (superseded by `GroupMessageService`).

- [ ] **Step 2: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors. (Build errors here are expected only from `GroupMutations`/`GroupQueries` still calling old signatures — those are fixed in Task 16/17. If so, note them and proceed; the final build gate is Phase 4. If nothing else errors, commit.)
```bash
git add Services/Groups/IGroupService.cs Services/Groups/GroupService.cs
git commit -m "feat: extend group service (image upload, transfer ownership, invites, join requests, mute, system messages)"
```

### Task 12: GroupCallService extensions

**Files:**
- Modify: `Services/Groups/IGroupCallService.cs`, `Services/Groups/GroupCallService.cs`

- [ ] **Step 1: Replace `Services/Groups/IGroupCallService.cs`**

```csharp
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;

namespace BlogGraphQlApp.Services.Groups
{
    public interface IGroupCallService
    {
        Task<ApiResponse<GroupCallDto>> StartAsync(Guid groupId, Guid startedById, CallMediaType mediaType, CancellationToken ct = default);
        Task<ApiResponse<GroupCallDto>> JoinAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> LeaveAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> EndAsync(Guid callId, Guid actorId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleMuteAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleCameraAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleScreenshareAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ToggleHandRaisedAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<GroupCallDto>> GetAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<GroupCallDto>> GetTokenAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupCallParticipantDto>>> GetParticipantsAsync(Guid callId, Guid userId, CancellationToken ct = default);
        Task<ApiResponse<IEnumerable<GroupCallDto>>> GetActiveCallsAsync(Guid userId, CancellationToken ct = default);
        Task<ApiResponse<PaginatedResult<CallHistoryDto>>> GetHistoryAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default);
        Task MarkEndedAsync(Guid callId, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Modify `Services/Groups/GroupCallService.cs`**

Add constructor deps (`INotificationService`, `IGroupMessageService`) and change the `StartAsync` signature:

```csharp
        public async Task<ApiResponse<GroupCallDto>> StartAsync(Guid groupId, Guid startedById, CallMediaType mediaType, CancellationToken ct = default)
```

Inside `StartAsync`, after creating the call record, set `MediaType = mediaType`:
```csharp
                var call = new GroupVideoCall
                {
                    CallId = callId,
                    GroupId = groupId,
                    RoomName = roomName,
                    DailyRoomUrl = room.Url,
                    StartedBy = startedById,
                    Status = GroupCallStatus.Ringing,
                    MediaType = mediaType
                };
```

After `await _history.StartGroupAsync(...)` and `await NotifyGroupCallAsync(...)`, add notifications + system message (still inside the try, before `Map`):
```csharp
                var group = await _unitOfWork.ChatGroups.GetByIdAsync(groupId);
                if (group is not null)
                {
                    foreach (var memberId in otherMembers)
                    {
                        await _notificationService.CreateAsync(
                            memberId,
                            NotificationType.GroupCallStarted,
                            $"{starter?.FullName ?? "A member"} started a {(mediaType == CallMediaType.Voice ? "voice" : "video")} call in {group.Name}.",
                            call.CallId,
                            (int)NotificationType.GroupCallStarted,
                            null,
                            ct);
                    }
                    await _messageService.InsertSystemMessageAsync(group, startedById, "Call started.", null, ct);
                }
```

In `Map`, add `MediaType = call.MediaType,`.

- [ ] **Step 3: Add LeaveAsync + participant toggles + participant queries**

Add these methods to `GroupCallService`:

```csharp
        public async Task<ApiResponse<bool>> LeaveAsync(Guid callId, Guid userId, CancellationToken ct = default)
        {
            var call = await FindCallAsync(callId, ct);
            if (call is null) return ApiResponse<bool>.Fail("Group call not found.");

            var participant = await _unitOfWork.GroupVideoCallParticipants
                .Find(p => p.CallId == callId && p.UserId == userId)
                .FirstOrDefaultAsync(ct);
            if (participant is null) return ApiResponse<bool>.Fail("You are not in this call.");

            participant.LeftAt = DateTime.UtcNow;
            participant.Token = null;
            _unitOfWork.GroupVideoCallParticipants.Update(participant);
            await _unitOfWork.CompleteAsync(ct);

            await PublishAsync($"{callId}_GroupCallParticipantLeft", await ToParticipantDtoAsync(participant, ct), ct);
            return ApiResponse<bool>.Success(true, "Left the call.");
        }

        public async Task<ApiResponse<bool>> ToggleMuteAsync(Guid callId, Guid userId, CancellationToken ct = default)
            => await ToggleParticipantFlagAsync(callId, userId, p => p.IsMuted = !p.IsMuted, ct);

        public async Task<ApiResponse<bool>> ToggleCameraAsync(Guid callId, Guid userId, CancellationToken ct = default)
            => await ToggleParticipantFlagAsync(callId, userId, p => p.CameraEnabled = !p.CameraEnabled, ct);

        public async Task<ApiResponse<bool>> ToggleScreenshareAsync(Guid callId, Guid userId, CancellationToken ct = default)
            => await ToggleParticipantFlagAsync(callId, userId, p => p.ScreenSharing = !p.ScreenSharing, ct);

        public async Task<ApiResponse<bool>> ToggleHandRaisedAsync(Guid callId, Guid userId, CancellationToken ct = default)
            => await ToggleParticipantFlagAsync(callId, userId, p => p.HandRaised = !p.HandRaised, ct);

        private async Task<ApiResponse<bool>> ToggleParticipantFlagAsync(Guid callId, Guid userId, Action<GroupVideoCallParticipant> toggle, CancellationToken ct)
        {
            var participant = await _unitOfWork.GroupVideoCallParticipants
                .Find(p => p.CallId == callId && p.UserId == userId)
                .Include(p => p.User)
                .FirstOrDefaultAsync(ct);
            if (participant is null) return ApiResponse<bool>.Fail("You are not in this call.");

            toggle(participant);
            _unitOfWork.GroupVideoCallParticipants.Update(participant);
            await _unitOfWork.CompleteAsync(ct);

            await PublishAsync($"{callId}_GroupCallParticipantUpdated", await ToParticipantDtoAsync(participant, ct), ct);
            return ApiResponse<bool>.Success(true, "Participant state updated.");
        }

        public async Task<ApiResponse<IEnumerable<GroupCallParticipantDto>>> GetParticipantsAsync(Guid callId, Guid userId, CancellationToken ct = default)
        {
            var call = await FindCallAsync(callId, ct);
            if (call is null) return ApiResponse<IEnumerable<GroupCallParticipantDto>>.Fail("Group call not found.");
            if (!await GetMembershipAsync(call.GroupId, userId, ct) is null == false)
                return ApiResponse<IEnumerable<GroupCallParticipantDto>>.Fail("You are not a member of this group.");

            var participants = await _unitOfWork.GroupVideoCallParticipants
                .Find(p => p.CallId == callId)
                .Include(p => p.User)
                .OrderBy(p => p.JoinedAt)
                .ToListAsync(ct);

            var dtos = new List<GroupCallParticipantDto>();
            foreach (var p in participants)
                dtos.Add(await ToParticipantDtoAsync(p, ct));
            return ApiResponse<IEnumerable<GroupCallParticipantDto>>.Success(dtos);
        }

        public async Task<ApiResponse<IEnumerable<GroupCallDto>>> GetActiveCallsAsync(Guid userId, CancellationToken ct = default)
        {
            var groupIds = await _unitOfWork.ChatGroupMembers
                .Find(m => m.UserId == userId)
                .Select(m => m.GroupId)
                .ToListAsync(ct);

            var calls = await _unitOfWork.GroupVideoCalls
                .Find(c => groupIds.Contains(c.GroupId) && c.Status != GroupCallStatus.Ended)
                .Include(c => c.Group)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);

            var dtos = calls.Select(c => Map(c, c.Group, null, null)).ToList();
            return ApiResponse<IEnumerable<GroupCallDto>>.Success(dtos);
        }

        public async Task<ApiResponse<PaginatedResult<CallHistoryDto>>> GetHistoryAsync(Guid groupId, Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            if (await GetMembershipAsync(groupId, userId, ct) is null)
                return ApiResponse<PaginatedResult<CallHistoryDto>>.Fail("You are not a member of this group.");

            var query = new CallHistoryQuery { Page = page, PageSize = pageSize, CallType = CallType.Group };
            var history = await _history.GetHistoryAsync(userId, query, ct);

            // Filter to this group's calls (CallHistoryDto carries the group id).
            var filtered = new PaginatedResult<CallHistoryDto>(
                history.Items.Where(h => h.GroupId == groupId).ToList(),
                history.Page,
                history.PageSize,
                history.TotalItems);
            return ApiResponse<PaginatedResult<CallHistoryDto>>.Success(filtered);
        }
```

Notes for the implementer:
- `CallHistoryQuery` lives in `Dtos/CallHistoryQuery.cs`; `CallHistoryDto` in `Dtos/CallHistoryDto.cs`. `PaginatedResult<T>` has a private ctor — you cannot construct it directly. Instead, use `PaginatedResult<T>.Create(items, page, pageSize, total)`. For the group-filtered history, build with `PaginatedResult<CallHistoryDto>.Create(filteredList, page, pageSize, filteredTotal)` and compute `filteredTotal = history.Items.Count(h => h.GroupId == groupId)`. Adjust the method accordingly.
- `CallHistoryDto` — verify it exposes `GroupId`; if not, filter by loading history per group via `ICallHistoryService.GetHistoryAsync` using group context. If `GroupId` is unavailable on the DTO, return the full history filtered in-memory by matching the user's group list instead; keep the method signature.
- Add `await _push` usage is unchanged. Add the new notifications/system-message block inside `StartAsync`.

- [ ] **Step 4: Missed-call notifications in EndAsync**

In `FinishCallAsync`, after `await _history.EndGroupAsync(...)`, add missed-call notifications for members who never joined:

```csharp
            var group = await _unitOfWork.ChatGroups.GetByIdAsync(call.GroupId);
            var memberIds = await _unitOfWork.ChatGroupMembers
                .Find(m => m.GroupId == call.GroupId)
                .Select(m => m.UserId)
                .ToListAsync(ct);
            var joinedIds = participants.Select(p => p.UserId).ToHashSet();
            var missed = memberIds.Where(id => !joinedIds.Contains(id) && id != call.StartedBy).ToList();
            if (group is not null)
            {
                foreach (var missedId in missed)
                {
                    await _notificationService.CreateAsync(
                        missedId,
                        NotificationType.GroupCallMissed,
                        $"You missed a group call in {group.Name}.",
                        call.CallId,
                        (int)NotificationType.GroupCallMissed,
                        null,
                        ct);
                }
                await _messageService.InsertSystemMessageAsync(group, call.StartedBy, "Call ended.", null, ct);
            }
```

- [ ] **Step 5: Add participant DTO mapping helper**

```csharp
        private async Task<GroupCallParticipantDto> ToParticipantDtoAsync(GroupVideoCallParticipant participant, CancellationToken ct)
        {
            var user = participant.User ?? await _unitOfWork.Users.GetByIdAsync(participant.UserId);
            return new GroupCallParticipantDto
            {
                Id = participant.Id,
                CallId = participant.CallId,
                UserId = participant.UserId,
                FullName = user?.FullName ?? string.Empty,
                Avatar = user?.ProfilePictureUrl,
                JoinedAt = participant.JoinedAt,
                LeftAt = participant.LeftAt,
                IsMuted = participant.IsMuted,
                CameraEnabled = participant.CameraEnabled,
                ScreenSharing = participant.ScreenSharing,
                HandRaised = participant.HandRaised
            };
        }
```

- [ ] **Step 6: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: only errors from GraphQL callers still using the old `StartAsync` signature (`GroupCallMutations`) — fixed in Task 17. If nothing else errors, commit.
```bash
git add Services/Groups/IGroupCallService.cs Services/Groups/GroupCallService.cs
git commit -m "feat: expand group call service (media type, leave, participant toggles, history, missed calls)"
```

---

## Phase 3 — GraphQL

### Task 13: DataLoaders

**Files:**
- Create: `GraphQL/DataLoaders/ReactionsByGroupMessageIdDataLoader.cs`, `MentionsByGroupMessageIdDataLoader.cs`, `GroupMessageByIdDataLoader.cs`, `ReadsByGroupMessageIdDataLoader.cs`

- [ ] **Step 1: Create the four DataLoaders** (mirror `ReactionsByMessageIdDataLoader`)

`GraphQL/DataLoaders/ReactionsByGroupMessageIdDataLoader.cs`:
```csharp
using BlogGraphQlApp.Data;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.GraphQL.DataLoaders
{
    public class ReactionsByGroupMessageIdDataLoader(IBatchScheduler batchScheduler, IDbContextFactory<AppDbContext> dbContextFactory, DataLoaderOptions options)
        : GroupedDataLoader<Guid, Reaction>(batchScheduler, options)
    {
        protected override async Task<ILookup<Guid, Reaction>> LoadGroupedBatchAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var reactions = await dbContext.Reactions
                .Where(r => keys.Contains(r.GroupMessageId!.Value))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
            return reactions.ToLookup(r => r.GroupMessageId!.Value);
        }
    }
}
```

`GraphQL/DataLoaders/MentionsByGroupMessageIdDataLoader.cs`:
```csharp
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
```

`GraphQL/DataLoaders/GroupMessageByIdDataLoader.cs`:
```csharp
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
```

`GraphQL/DataLoaders/ReadsByGroupMessageIdDataLoader.cs`:
```csharp
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
```

- [ ] **Step 2: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors (DataLoaders are self-contained; registration happens in Task 19).
```bash
git add GraphQL/DataLoaders/
git commit -m "feat: add data loaders for group messages (reactions, mentions, reply, reads)"
```

### Task 14: GraphQL types

**Files:**
- Modify: `GraphQL/Types/GroupTypeGql.cs`, `GroupMessageTypeGql.cs`, `NotificationTypeGql.cs`
- Create: `GraphQL/Types/GroupMentionTypeGql.cs`, `GroupCallParticipantTypeGql.cs`, `GroupJoinRequestTypeGql.cs`

- [ ] **Step 1: Replace `GraphQL/Types/GroupMessageTypeGql.cs`**

```csharp
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Resolvers;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupMessageTypeGql : ObjectType<GroupMessageDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupMessageDto> descriptor)
        {
            descriptor.Description("A message sent inside a group chat.");

            descriptor.Field(m => m.Id).Description("The unique identifier of the message.");
            descriptor.Field(m => m.GroupId).Description("The group the message belongs to.");
            descriptor.Field(m => m.SenderId).Description("The user who sent the message.");
            descriptor.Field(m => m.MessageType).Description("The type of the message (text, image, video, document, audio, system).");
            descriptor.Field(m => m.Content).Description("The textual content of the message, if any.");
            descriptor.Field(m => m.FileUrl).Description("The URL of an uploaded file attachment, if any.");
            descriptor.Field(m => m.ReplyToMessageId).Description("The ID of the message this message replies to, if any.");
            descriptor.Field(m => m.CreatedAt).Description("When the message was created.");
            descriptor.Field(m => m.EditedAt).Description("When the message was last edited, if ever.");
            descriptor.Field(m => m.EditedBy).Description("Who edited the message, if ever.");
            descriptor.Field(m => m.Deleted).Description("Whether the message was soft-deleted.");
            descriptor.Field(m => m.IsPinned).Description("Whether the message is pinned.");
            descriptor.Field(m => m.PinnedAt).Description("When the message was pinned.");
            descriptor.Field(m => m.PinnedBy).Description("Who pinned the message.");
            descriptor.Field(m => m.Status).Description("Delivery status of the message.");
            descriptor.Field(m => m.DeliveredCount).Description("Number of members who have received the message.");
            descriptor.Field(m => m.ReadCount).Description("Number of members who have read the message.");
            descriptor.Field(m => m.UnreadCount).Description("Number of members who have not read the message.");

            descriptor.Field(m => m.ReplyToMessage)
                .Type<GroupMessageTypeGql>()
                .ResolveWith<GroupMessageResolvers>(r => r.GetReplyToMessage(default!, default!, default!, default!));

            descriptor.Field(m => m.Mentions)
                .Description("Users mentioned in this message.")
                .ResolveWith<GroupMessageResolvers>(r => r.GetMentions(default!, default!, default!, default!));

            descriptor.Field(m => m.Reactions)
                .Description("Reactions on this message.")
                .ResolveWith<GroupMessageResolvers>(r => r.GetReactions(default!, default!, default!, default!));
        }
    }
}
```

- [ ] **Step 2: Replace `GraphQL/Types/GroupTypeGql.cs`**

```csharp
using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupTypeGql : ObjectType<GroupDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupDto> descriptor)
        {
            descriptor.Description("A group chat group.");

            descriptor.Field(g => g.Id).Description("The unique identifier of the group.");
            descriptor.Field(g => g.Name).Description("The group name.");
            descriptor.Field(g => g.Description).Description("Optional group description.");
            descriptor.Field(g => g.ImageUrl).Description("Optional group image URL.");
            descriptor.Field(g => g.IsPrivate).Description("Whether the group is private (join requests required).");
            descriptor.Field(g => g.InviteCode).Description("Invite code; only visible to admins and the owner.");
            descriptor.Field(g => g.LastMessageId).Description("ID of the most recent message.");
            descriptor.Field(g => g.LastMessage).Type<GroupMessageTypeGql>().Description("The most recent message, for the group list.");
            descriptor.Field(g => g.LastSender).Description("Sender of the most recent message.");
            descriptor.Field(g => g.LastActivityAt).Description("When the group last had activity.");
            descriptor.Field(g => g.UpdatedAt).Description("When the group info was last updated.");
            descriptor.Field(g => g.Archived).Description("Whether the group is archived.");
            descriptor.Field(g => g.MaxMembers).Description("Optional member limit.");
            descriptor.Field(g => g.CreatedBy).Description("The user who created the group.");
            descriptor.Field(g => g.MemberCount).Description("Number of members.");
            descriptor.Field(g => g.UnreadCount).Description("Unread message count for the requesting user.");
        }
    }
}
```

- [ ] **Step 3: Create the three new type classes** (mirror the existing trivial pattern)

`GraphQL/Types/GroupMentionTypeGql.cs`:
```csharp
using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupMentionTypeGql : ObjectType<GroupMentionDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupMentionDto> descriptor)
            => descriptor.Description("A user mentioned in a group message.");
    }
}
```

`GraphQL/Types/GroupCallParticipantTypeGql.cs`:
```csharp
using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupCallParticipantTypeGql : ObjectType<GroupCallParticipantDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupCallParticipantDto> descriptor)
            => descriptor.Description("A participant of a group call and their live state.");
    }
}
```

`GraphQL/Types/GroupJoinRequestTypeGql.cs`:
```csharp
using BlogGraphQlApp.DTOs;
using HotChocolate.Types;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class GroupJoinRequestTypeGql : ObjectType<GroupJoinRequestDto>
    {
        protected override void Configure(IObjectTypeDescriptor<GroupJoinRequestDto> descriptor)
            => descriptor.Description("A pending join request for a private group.");
    }
}
```

- [ ] **Step 4: Extend `GraphQL/Types/NotificationTypeGql.cs`** (append inside `Configure`):

```csharp
            descriptor.Field(n => n.RelatedEntityId).Type<IdType>().Description("The entity this notification references, if any.");
            descriptor.Field(n => n.RelatedEntityType).Description("Type discriminator of the related entity.");
            descriptor.Field(n => n.Metadata).Description("Structured JSON metadata for the notification payload.");
```

- [ ] **Step 5: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors (resolvers referenced by the type exist from Task 15 — if the build fails because `GroupMessageResolvers` is missing, do Task 15 next and commit after).
```bash
git add GraphQL/Types/
git commit -m "feat: expand group GraphQL types"
```

### Task 15: Events + Resolvers

**Files:**
- Create: `GraphQL/Events/GroupTypingEvent.cs`, `GraphQL/Resolvers/GroupMessageResolvers.cs`
- Modify: `GraphQL/Events/ReactionPayload.cs`

- [ ] **Step 1: Create `GraphQL/Events/GroupTypingEvent.cs`**

```csharp
namespace BlogGraphQlApp.GraphQL.Events
{
    public record GroupTypingEvent(Guid UserId, string FullName, Guid GroupId, bool IsTyping, DateTime Timestamp);
}
```

- [ ] **Step 2: Extend `GraphQL/Events/ReactionPayload.cs`** — add:

```csharp
        public Guid? GroupMessageId { get; set; }
        public Guid? GroupId { get; set; }
```

- [ ] **Step 3: Create `GraphQL/Resolvers/GroupMessageResolvers.cs`**

```csharp
using AutoMapper;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.DataLoaders;

namespace BlogGraphQlApp.GraphQL.Resolvers
{
    public class GroupMessageResolvers
    {
        public async Task<IEnumerable<GroupMentionDto>> GetMentions(
            [Parent] GroupMessageDto message,
            MentionsByGroupMessageIdDataLoader loader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var mentions = await loader.LoadAsync(message.Id, cancellationToken);
            return mapper.Map<IEnumerable<GroupMentionDto>>(mentions);
        }

        public async Task<IEnumerable<ReactionDto>> GetReactions(
            [Parent] GroupMessageDto message,
            ReactionsByGroupMessageIdDataLoader loader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            var reactions = await loader.LoadAsync(message.Id, cancellationToken);
            return mapper.Map<IEnumerable<ReactionDto>>(reactions);
        }

        public async Task<GroupMessageDto?> GetReplyToMessage(
            [Parent] GroupMessageDto message,
            GroupMessageByIdDataLoader loader,
            [Service] IMapper mapper,
            CancellationToken cancellationToken)
        {
            if (message.ReplyToMessageId is null)
                return null;

            var replies = await loader.LoadAsync(message.ReplyToMessageId.Value, cancellationToken);
            var reply = replies.FirstOrDefault();
            return reply is null ? null : mapper.Map<GroupMessageDto>(reply);
        }
    }
}
```

- [ ] **Step 4: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors.
```bash
git add GraphQL/Events/ GraphQL/Resolvers/GroupMessageResolvers.cs
git commit -m "feat: add group message resolvers and typing event"
```

### Task 16: Queries

**Files:**
- Modify: `GraphQL/Queries/GroupQueries.cs`
- Create: `GraphQL/Queries/GroupMessageQueries.cs`, `GraphQL/Queries/GroupCallQueries.cs`

- [ ] **Step 1: Modify `GraphQL/Queries/GroupQueries.cs`** — change the `GetGroupMessagesAsync` method to call `IGroupMessageService` (message queries move to `GroupMessageQueries`), add invite-code + join-request queries, and add the new `IGroupService`/`IGroupMessageService` services:

Replace the whole `GroupQueries` class body with:

```csharp
using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GroupQueries
    {
        [Authorize]
        [GraphQLDescription("Gets all groups the current user is a member of.")]
        public async Task<ApiResponse<IEnumerable<GroupDto>>> GetGroupsAsync(
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.GetGroupsAsync(userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets a single group the current user is a member of.")]
        public async Task<ApiResponse<GroupDto>> GetGroupAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.GetGroupAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the members of a group the current user belongs to.")]
        public async Task<ApiResponse<IEnumerable<GroupMemberDto>>> GetGroupMembersAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.GetMembersAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the invite code for a group (admins and owner only).")]
        public async Task<ApiResponse<string>> GetGroupInviteCodeAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.GetInviteCodeAsync(groupId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets pending join requests for a private group (admins and owner only).")]
        public async Task<ApiResponse<IEnumerable<GroupJoinRequestDto>>> GetPendingGroupJoinRequestsAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.GetPendingJoinRequestsAsync(groupId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the state of a group video call the user can join.")]
        public async Task<ApiResponse<GroupCallDto>> GetGroupCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetAsync(callId, userId, cancellationToken);
        }
    }
}
```

- [ ] **Step 2: Create `GraphQL/Queries/GroupMessageQueries.cs`**

```csharp
using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GroupMessageQueries
    {
        [Authorize]
        [GraphQLDescription("Gets paginated messages for a group, newest first.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetGroupMessagesAsync(
            Guid groupId,
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetMessagesAsync(groupId, userId, page ?? 1, pageSize ?? 20, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets a single group message.")]
        public async Task<ApiResponse<GroupMessageDto>> GetGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetMessageAsync(groupId, messageId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets pinned messages for a group.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetPinnedGroupMessagesAsync(
            Guid groupId,
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetPinnedMessagesAsync(groupId, userId, page ?? 1, pageSize ?? 20, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Searches group messages with the given filters.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> SearchGroupMessagesAsync(
            Guid groupId,
            GroupMessageSearchInput input,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.SearchAsync(groupId, userId, input, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets only media messages (images, videos, documents, audio) from a group.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetGroupMediaAsync(
            Guid groupId,
            MessageType? mediaType,
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetMediaAsync(groupId, userId, mediaType, page ?? 1, pageSize ?? 20, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the unread message count for a group.")]
        public async Task<ApiResponse<int>> GetGroupUnreadCountAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetUnreadCountAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the total unread count across all the current user's groups.")]
        public async Task<ApiResponse<int>> GetUnreadGroupCountAsync(
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetUnreadGroupCountAsync(userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets group messages that mention the current user.")]
        public async Task<ApiResponse<PaginatedResult<GroupMessageDto>>> GetMyGroupMentionsAsync(
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.GetMyMentionsAsync(userId, page ?? 1, pageSize ?? 20, cancellationToken);
        }
    }
}
```

- [ ] **Step 3: Create `GraphQL/Queries/GroupCallQueries.cs`**

```csharp
using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class GroupCallQueries
    {
        [Authorize]
        [GraphQLDescription("Gets active group calls across the current user's groups.")]
        public async Task<ApiResponse<IEnumerable<GroupCallDto>>> GetActiveGroupCallsAsync(
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetActiveCallsAsync(userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the participants of a group call.")]
        public async Task<ApiResponse<IEnumerable<GroupCallParticipantDto>>> GetGroupCallParticipantsAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetParticipantsAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets the call history for a group.")]
        public async Task<ApiResponse<PaginatedResult<CallHistoryDto>>> GetGroupCallHistoryAsync(
            Guid groupId,
            int? page,
            int? pageSize,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetHistoryAsync(groupId, userId, page ?? 1, pageSize ?? 20, cancellationToken);
        }
    }
}
```

- [ ] **Step 4: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: build may still fail from old `GroupService.SendMessageAsync`/`GetMessagesAsync` references in `GroupMutations` — those are fixed in Task 17. Commit regardless once query files compile.
```bash
git add GraphQL/Queries/
git commit -m "feat: add group message and call queries"
```

### Task 17: Mutations

**Files:**
- Modify: `GraphQL/Mutations/GroupMutations.cs`, `GraphQL/Mutations/GroupCallMutations.cs`
- Create: `GraphQL/Mutations/GroupMessageMutations.cs`

- [ ] **Step 1: Replace `GraphQL/Mutations/GroupMutations.cs`**

```csharp
using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GroupMutations
    {
        [Authorize]
        [GraphQLDescription("Creates a group chat and adds the creator as owner.")]
        public async Task<ApiResponse<GroupDto>> CreateGroupAsync(
            string name,
            string? description,
            bool isPrivate,
            int? maxMembers,
            string? imageUrl,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var ownerId = claimsPrincipal.GetUserId();
            return await groupService.CreateGroupAsync(ownerId, name, description, isPrivate, maxMembers, imageUrl, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Updates a group's name, description, privacy, archived flag, or member limit (owner or admin only).")]
        public async Task<ApiResponse<GroupDto>> UpdateGroupAsync(
            Guid groupId,
            string? name,
            string? description,
            bool? isPrivate,
            bool? archived,
            int? maxMembers,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.UpdateGroupAsync(groupId, actorId, name, description, isPrivate, archived, maxMembers, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Uploads a new group image (owner or admin only).")]
        public async Task<ApiResponse<GroupDto>> UploadGroupImageAsync(
            Guid groupId,
            IFile image,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.UploadGroupImageAsync(groupId, actorId, image, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Deletes a group (owner only).")]
        public async Task<ApiResponse<bool>> DeleteGroupAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.DeleteGroupAsync(groupId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Transfers group ownership to another member (owner only).")]
        public async Task<ApiResponse<GroupDto>> TransferGroupOwnershipAsync(
            Guid groupId,
            Guid targetUserId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.TransferOwnershipAsync(groupId, actorId, targetUserId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Adds a user to a group (owner or admin only).")]
        public async Task<ApiResponse<bool>> AddGroupMemberAsync(
            Guid groupId,
            Guid userId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.AddMemberAsync(groupId, actorId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Removes a member from a group (owner or admin only).")]
        public async Task<ApiResponse<bool>> RemoveGroupMemberAsync(
            Guid groupId,
            Guid userId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.RemoveMemberAsync(groupId, actorId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Leaves a group the current user belongs to.")]
        public async Task<ApiResponse<bool>> LeaveGroupAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.LeaveGroupAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Promotes a member to admin (owner only).")]
        public async Task<ApiResponse<bool>> PromoteGroupAdminAsync(
            Guid groupId,
            Guid userId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.PromoteAdminAsync(groupId, actorId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Demotes an admin back to member (owner only).")]
        public async Task<ApiResponse<bool>> DemoteGroupAdminAsync(
            Guid groupId,
            Guid userId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.DemoteAdminAsync(groupId, actorId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Generates (or regenerates) the invite code for a group (owner or admin only).")]
        public async Task<ApiResponse<string>> GenerateGroupInviteCodeAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.GenerateInviteCodeAsync(groupId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Revokes the invite code for a group (owner or admin only).")]
        public async Task<ApiResponse<bool>> RevokeGroupInviteCodeAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.RevokeInviteCodeAsync(groupId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Joins a public group using an invite code.")]
        public async Task<ApiResponse<GroupDto>> JoinGroupByInviteAsync(
            string inviteCode,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.JoinByInviteAsync(inviteCode, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Requests to join a private group.")]
        public async Task<ApiResponse<bool>> RequestGroupJoinAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.RequestJoinAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Approves a join request and adds the user (owner or admin only).")]
        public async Task<ApiResponse<bool>> ApproveGroupJoinRequestAsync(
            Guid groupId,
            Guid requestId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.ApproveJoinRequestAsync(groupId, actorId, requestId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Rejects a join request (owner or admin only).")]
        public async Task<ApiResponse<bool>> RejectGroupJoinRequestAsync(
            Guid groupId,
            Guid requestId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupService.RejectJoinRequestAsync(groupId, actorId, requestId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Mutes a group for the current user (optionally until a date).")]
        public async Task<ApiResponse<bool>> MuteGroupAsync(
            Guid groupId,
            DateTime? mutedUntil,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.MuteGroupAsync(groupId, userId, mutedUntil, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Unmutes a group for the current user.")]
        public async Task<ApiResponse<bool>> UnmuteGroupAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.MuteGroupAsync(groupId, userId, null, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Sets the notification level for the current user in a group.")]
        public async Task<ApiResponse<bool>> SetGroupNotificationLevelAsync(
            Guid groupId,
            NotificationLevel level,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupService groupService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupService.SetNotificationLevelAsync(groupId, userId, level, cancellationToken);
        }
    }
}
```

- [ ] **Step 2: Create `GraphQL/Mutations/GroupMessageMutations.cs`**

```csharp
using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.GraphQL.Events;
using BlogGraphQlApp.Services.Groups;
using FluentValidation;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GroupMessageMutations
    {
        public record SendGroupMessageInput(Guid GroupId, MessageType MessageType, string? Content, IFile? file, Guid? ReplyToMessageId);

        [Authorize]
        [GraphQLDescription("Sends a message (text or media) in a group.")]
        public async Task<ApiResponse<GroupMessageDto>> SendGroupMessageAsync(
            SendGroupMessageInput input,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            [Service] ITopicEventSender eventSender,
            [Service] IValidator<SendGroupMessageInput> validator,
            CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(input, cancellationToken);
            if (!validationResult.IsValid)
                return ApiResponse<GroupMessageDto>.Fail("Validation failed.", validationResult.Errors.Select(e => e.ErrorMessage).ToList());

            var senderId = claimsPrincipal.GetUserId();
            var response = await messageService.SendAsync(input.GroupId, senderId, input.MessageType, input.Content, input.file, input.ReplyToMessageId, cancellationToken);
            return response;
        }

        [Authorize]
        [GraphQLDescription("Edits a group message (sender only).")]
        public async Task<ApiResponse<GroupMessageDto>> EditGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            string content,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var senderId = claimsPrincipal.GetUserId();
            return await messageService.EditAsync(groupId, messageId, senderId, content, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Soft-deletes a group message (sender only).")]
        public async Task<ApiResponse<bool>> DeleteGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var senderId = claimsPrincipal.GetUserId();
            return await messageService.DeleteAsync(groupId, messageId, senderId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Pins a group message (owner or admin only).")]
        public async Task<ApiResponse<GroupMessageDto>> PinGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await messageService.SetPinnedAsync(groupId, messageId, actorId, true, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Unpins a group message (owner or admin only).")]
        public async Task<ApiResponse<GroupMessageDto>> UnpinGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await messageService.SetPinnedAsync(groupId, messageId, actorId, false, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Adds or toggles a reaction emoji on a group message.")]
        public async Task<ApiResponse<bool>> ReactToGroupMessageAsync(
            Guid groupId,
            Guid messageId,
            string emoji,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.ToggleReactionAsync(groupId, messageId, userId, emoji, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Removes the current user's reaction from a group message.")]
        public async Task<ApiResponse<bool>> RemoveGroupReactionAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.RemoveReactionAsync(groupId, messageId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Marks a group message as delivered for the current user.")]
        public async Task<ApiResponse<bool>> MarkGroupMessageDeliveredAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.MarkDeliveredAsync(groupId, messageId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Marks a group message as read for the current user.")]
        public async Task<ApiResponse<bool>> MarkGroupMessageReadAsync(
            Guid groupId,
            Guid messageId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.MarkReadAsync(groupId, messageId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Marks all group messages as read for the current user.")]
        public async Task<ApiResponse<bool>> MarkAllGroupMessagesReadAsync(
            Guid groupId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await messageService.MarkAllReadAsync(groupId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Broadcasts a typing indicator to members of a group.")]
        public async Task<GroupTypingEvent> NotifyGroupTypingAsync(
            Guid groupId,
            bool isTyping,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupMessageService messageService,
            [Service] ITopicEventSender eventSender,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            var typingEvent = new GroupTypingEvent(userId, claimsPrincipal.Identity?.Name ?? string.Empty, groupId, isTyping, DateTime.UtcNow);
            await eventSender.SendAsync($"{groupId}_GroupTyping", typingEvent, cancellationToken);
            return typingEvent;
        }
    }
}
```

- [ ] **Step 3: Replace `GraphQL/Mutations/GroupCallMutations.cs`**

```csharp
using System.Security.Claims;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Services.Groups;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Mutations
{
    [ExtendObjectType("Mutation")]
    public class GroupCallMutations
    {
        [Authorize]
        [GraphQLDescription("Starts a group call (voice or video) for a group the current user is a member of.")]
        public async Task<ApiResponse<GroupCallDto>> StartGroupCallAsync(
            Guid groupId,
            CallMediaType mediaType,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var startedById = claimsPrincipal.GetUserId();
            return await groupCallService.StartAsync(groupId, startedById, mediaType, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Joins an active group call and returns the Daily room URL + meeting token.")]
        public async Task<ApiResponse<GroupCallDto>> JoinGroupCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.JoinAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Leaves an active group call.")]
        public async Task<ApiResponse<bool>> LeaveGroupCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.LeaveAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Ends a group call.")]
        public async Task<ApiResponse<bool>> EndGroupCallAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var actorId = claimsPrincipal.GetUserId();
            return await groupCallService.EndAsync(callId, actorId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Toggles the current user's mute state in a group call.")]
        public async Task<ApiResponse<bool>> ToggleGroupCallMuteAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.ToggleMuteAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Toggles the current user's camera in a group call.")]
        public async Task<ApiResponse<bool>> ToggleGroupCallCameraAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.ToggleCameraAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Toggles the current user's screen sharing in a group call.")]
        public async Task<ApiResponse<bool>> ToggleGroupCallScreenshareAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.ToggleScreenshareAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Toggles the current user's raised hand in a group call.")]
        public async Task<ApiResponse<bool>> ToggleGroupCallHandRaisedAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.ToggleHandRaisedAsync(callId, userId, cancellationToken);
        }

        [Authorize]
        [GraphQLDescription("Gets a fresh Daily meeting token for an active group call.")]
        public async Task<ApiResponse<GroupCallDto>> GetGroupCallTokenAsync(
            Guid callId,
            ClaimsPrincipal claimsPrincipal,
            [Service] IGroupCallService groupCallService,
            CancellationToken cancellationToken)
        {
            var userId = claimsPrincipal.GetUserId();
            return await groupCallService.GetTokenAsync(callId, userId, cancellationToken);
        }
    }
}
```

- [ ] **Step 4: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: may still fail because `IGroupMessageService` isn't registered in DI yet (Task 19). Commit once compile errors are limited to the missing DI registration.
```bash
git add GraphQL/Mutations/
git commit -m "feat: add group message mutations and extend group/call mutations"
```

### Task 18: Subscriptions

**Files:**
- Modify: `GraphQL/Subscriptions/CallSubscription.cs`

- [ ] **Step 1: Extend `CallSubscription.cs`** — add the new subscription methods (keep all existing ones):

```csharp
        [Subscribe(With = nameof(SubscribeToGroupMessageEditedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group message is edited.")]
        public GroupMessageDto GroupMessageEdited([EventMessage] GroupMessageDto message) => message;

        public static async ValueTask<ISourceStream<GroupMessageDto>> SubscribeToGroupMessageEditedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupMessageDto>($"{groupId}_GroupMessageEdited");

        [Subscribe(With = nameof(SubscribeToGroupMessageDeletedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group message is deleted.")]
        public Guid GroupMessageDeleted([EventMessage] Guid messageId) => messageId;

        public static async ValueTask<ISourceStream<Guid>> SubscribeToGroupMessageDeletedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<Guid>($"{groupId}_GroupMessageDeleted");

        [Subscribe(With = nameof(SubscribeToGroupMessagePinnedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group message is pinned or unpinned.")]
        public GroupMessageDto GroupMessagePinned([EventMessage] GroupMessageDto message) => message;

        public static async ValueTask<ISourceStream<GroupMessageDto>> SubscribeToGroupMessagePinnedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupMessageDto>($"{groupId}_GroupMessagePinned");

        [Subscribe(With = nameof(SubscribeToGroupMessageReactionAddedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when someone reacts to a group message.")]
        public Guid GroupMessageReactionAdded([EventMessage] Guid messageId) => messageId;

        public static async ValueTask<ISourceStream<Guid>> SubscribeToGroupMessageReactionAddedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<Guid>($"{groupId}_GroupMessageReactionAdded");

        [Subscribe(With = nameof(SubscribeToGroupMessageReactionRemovedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a reaction is removed from a group message.")]
        public Guid GroupMessageReactionRemoved([EventMessage] Guid messageId) => messageId;

        public static async ValueTask<ISourceStream<Guid>> SubscribeToGroupMessageReactionRemovedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<Guid>($"{groupId}_GroupMessageReactionRemoved");

        [Subscribe(With = nameof(SubscribeToGroupMemberJoinedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a member joins a group.")]
        public GroupMemberDto GroupMemberJoined([EventMessage] GroupMemberDto member) => member;

        public static async ValueTask<ISourceStream<GroupMemberDto>> SubscribeToGroupMemberJoinedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupMemberDto>($"{groupId}_GroupMemberJoined");

        [Subscribe(With = nameof(SubscribeToGroupMemberLeftAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a member leaves a group.")]
        public GroupMemberDto GroupMemberLeft([EventMessage] GroupMemberDto member) => member;

        public static async ValueTask<ISourceStream<GroupMemberDto>> SubscribeToGroupMemberLeftAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupMemberDto>($"{groupId}_GroupMemberLeft");

        [Subscribe(With = nameof(SubscribeToGroupUpdatedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group is updated.")]
        public GroupDto GroupUpdated([EventMessage] GroupDto group) => group;

        public static async ValueTask<ISourceStream<GroupDto>> SubscribeToGroupUpdatedAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupDto>($"{groupId}_GroupUpdated");

        [Subscribe(With = nameof(SubscribeToGroupTypingAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a member starts or stops typing in a group.")]
        public GroupTypingEvent UserTypingInGroup([EventMessage] GroupTypingEvent typingEvent) => typingEvent;

        public static async ValueTask<ISourceStream<GroupTypingEvent>> SubscribeToGroupTypingAsync(
            Guid groupId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupTypingEvent>($"{groupId}_GroupTyping");

        [Subscribe(With = nameof(SubscribeToGroupCallParticipantJoinedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a participant joins a group call.")]
        public GroupCallParticipantDto GroupCallParticipantJoined([EventMessage] GroupCallParticipantDto participant) => participant;

        public static async ValueTask<ISourceStream<GroupCallParticipantDto>> SubscribeToGroupCallParticipantJoinedAsync(
            Guid callId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupCallParticipantDto>($"{callId}_GroupCallParticipantJoined");

        [Subscribe(With = nameof(SubscribeToGroupCallParticipantLeftAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a participant leaves a group call.")]
        public GroupCallParticipantDto GroupCallParticipantLeft([EventMessage] GroupCallParticipantDto participant) => participant;

        public static async ValueTask<ISourceStream<GroupCallParticipantDto>> SubscribeToGroupCallParticipantLeftAsync(
            Guid callId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupCallParticipantDto>($"{callId}_GroupCallParticipantLeft");

        [Subscribe(With = nameof(SubscribeToGroupCallParticipantUpdatedAsync))]
        [Authorize]
        [GraphQLDescription("Receives a realtime event when a group call participant's state changes (mute/camera/screenshare/hand).")]
        public GroupCallParticipantDto GroupCallParticipantUpdated([EventMessage] GroupCallParticipantDto participant) => participant;

        public static async ValueTask<ISourceStream<GroupCallParticipantDto>> SubscribeToGroupCallParticipantUpdatedAsync(
            Guid callId, [Service] ITopicEventReceiver eventReceiver)
            => await eventReceiver.SubscribeAsync<GroupCallParticipantDto>($"{callId}_GroupCallParticipantUpdated");
```

Notes:
- The published payload types must match the subscription field types exactly: `GroupMessageDeleted`/`GroupMessageReactionAdded`/`GroupMessageReactionRemoved` publish a `Guid` message id (that's what `GroupMessageService` sends), the others publish the DTO.
- The existing `GroupCallService` publishes `GroupCallParticipantDto` on `{callId}_GroupCallParticipantUpdated`/`Left` (Task 12) — matches these subscriptions.

- [ ] **Step 2: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors once types resolve. Commit:
```bash
git add GraphQL/Subscriptions/CallSubscription.cs
git commit -m "feat: add group message, typing, member and call participant subscriptions"
```

### Task 19: Validators + Program.cs wiring

**Files:**
- Create: `Validators/SendGroupMessageValidator.cs`, `Validators/SearchGroupMessagesValidator.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Create `Validators/SendGroupMessageValidator.cs`** (mirror `SendMessageInputValidator`)

```csharp
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.GraphQL.Mutations;
using FluentValidation;

namespace BlogGraphQlApp.Validators
{
    public class SendGroupMessageValidator : AbstractValidator<GroupMessageMutations.SendGroupMessageInput>
    {
        private static readonly string[] AllowedAudioTypes = ["audio/mpeg", "audio/wav", "audio/aac", "audio/ogg", "audio/mp3"];
        private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/svg+xml", "image/tiff", "image/avif"];
        private static readonly string[] AllowedVideoTypes = ["video/mp4", "video/webm", "video/ogg", "video/quicktime"];
        private static readonly string[] AllowedDocumentTypes =
        [
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.google-apps.document",
            "application/vnd.google-apps.spreadsheet",
            "application/vnd.google-apps.presentation"
        ];

        public SendGroupMessageValidator()
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.Content) || x.file != null)
                .WithMessage("Message must have either text content or a file.");

            When(x => x.MessageType == MessageType.Text, () =>
            {
                RuleFor(x => x.Content).NotEmpty().WithMessage("Text message cannot be empty.");
                RuleFor(x => x.file).Null().WithMessage("Text message cannot have a file attachment.");
            });

            When(x => x.MessageType == MessageType.System, () =>
            {
                RuleFor(x => x.file).Null().WithMessage("System messages cannot have a file attachment.");
            });

            When(x => x.file != null, () =>
            {
                When(x => x.MessageType == MessageType.Audio, () => {
                    RuleFor(x => x.file!.ContentType).Must(ct => AllowedAudioTypes.Contains(ct)).WithMessage($"Invalid audio file type. Allowed types are: {string.Join(", ", AllowedAudioTypes)}");
                });

                When(x => x.MessageType == MessageType.Image, () => {
                    RuleFor(x => x.file!.ContentType).Must(ct => AllowedImageTypes.Contains(ct)).WithMessage($"Invalid image file type. Allowed types are: {string.Join(", ", AllowedImageTypes)}");
                });

                When(x => x.MessageType == MessageType.Video, () => {
                    RuleFor(x => x.file!.ContentType).Must(ct => AllowedVideoTypes.Contains(ct)).WithMessage($"Invalid video file type. Allowed types are: {string.Join(", ", AllowedVideoTypes)}");
                });

                When(x => x.MessageType == MessageType.Document, () => {
                    RuleFor(x => x.file!.ContentType).Must(ct => AllowedDocumentTypes.Contains(ct)).WithMessage($"Invalid file type. Allowed types are: {string.Join(", ", AllowedDocumentTypes)}.");
                });
            });
        }
    }
}
```

- [ ] **Step 2: Create `Validators/SearchGroupMessagesValidator.cs`**

```csharp
using BlogGraphQlApp.DTOs;
using FluentValidation;

namespace BlogGraphQlApp.Validators
{
    public class SearchGroupMessagesValidator : AbstractValidator<GroupMessageSearchInput>
    {
        public SearchGroupMessagesValidator()
        {
            RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be at least 1.");
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
        }
    }
}
```

- [ ] **Step 3: Wire up `Program.cs`**

In the service registrations block (after `builder.Services.AddScoped<IGroupService, GroupService>();`), add:
```csharp
builder.Services.AddScoped<IGroupMessageService, GroupMessageService>();
builder.Services.AddScoped<GroupPermissionService>();
```

In the GraphQL type extensions (after `.AddTypeExtension<GroupQueries>()`), add:
```csharp
        .AddTypeExtension<GroupMessageQueries>()
        .AddTypeExtension<GroupCallQueries>()
```
After `.AddTypeExtension<GroupMutations>()`, add:
```csharp
        .AddTypeExtension<GroupMessageMutations>()
```
After `.AddTypeExtension<CallSubscription>()`, add:
```csharp
        .AddTypeExtension<NotificationSubscription>()
```
After `.AddType<GroupCallTypeGql>()`, add:
```csharp
    .AddType<GroupMentionTypeGql>()
    .AddType<GroupCallParticipantTypeGql>()
    .AddType<GroupJoinRequestTypeGql>()
```
In the DataLoader registrations (after `.AddDataLoader<ReactionsByMessageIdDataLoader>()` — note the existing registration is `.AddDataLoader<ReactionsByReplyIdDataLoader>()` etc.), add:
```csharp
    .AddDataLoader<ReactionsByGroupMessageIdDataLoader>()
    .AddDataLoader<MentionsByGroupMessageIdDataLoader>()
    .AddDataLoader<GroupMessageByIdDataLoader>()
    .AddDataLoader<ReadsByGroupMessageIdDataLoader>()
```

- [ ] **Step 4: Build and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors. Fix any leftover compile issues (e.g. `PresenceTracker` namespace import in `GroupService`, `CallHistoryDto.GroupId` availability, `INotificationService` using in `GroupMessageService`/`GroupCallService`).
```bash
git add Program.cs Validators/
git commit -m "feat: wire group message service, queries, mutations, types and data loaders into GraphQL schema"
```

---

## Phase 4 — Docs & verification

### Task 20: End-to-end verification

**Files:**
- None (verification only).

- [ ] **Step 1: Fresh build**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj`
Expected: 0 errors, no new warnings beyond the pre-existing NU1903/AutoMapper warnings.

- [ ] **Step 2: Apply the migration**

Start the app once (or run `dotnet ef database update --project BlogGraphQlApp.csproj`). The startup `ApplyMigrationAsync` auto-applies `GroupChatUpgrade`. Confirm the migration applied and no SQL errors (defaults on existing tables).

- [ ] **Step 3: Live GraphQL smoke tests** (via Banana Cake Pop at `http://localhost:5250/graphql` or an authenticated client)

Register two users (or reuse the existing test flow: register via GraphQL, set `IsEmailVerified = 1` directly in MySQL using the `.tmp_mysqlfix` MySqlConnector helper pattern, login).

1. `createGroup(name, description, isPrivate:false)` → returns `inviteCode`, `memberCount:1`, `lastActivityAt`.
2. `sendGroupMessage` text with `@<username>` mention → message returns `messageType:TEXT`, `mentions` includes the member; second user's `notificationReceived`/`unreadGroupCount` reflects it.
3. `sendGroupMessage` with an `IFile` (image) → `messageType:IMAGE`, `fileUrl` present.
4. `reactToGroupMessage` + `removeGroupReaction` → reaction appears/disappears.
5. `pinGroupMessage` → `isPinned:true`; `getPinnedGroupMessages` returns it; member (non-admin) gets permission failure.
6. `markGroupMessageRead` → `readCount` increments; `getGroupUnreadCount` drops.
7. `editGroupMessage` → `editedAt` set; `deleteGroupMessage` → `deleted:true`, content nulled, reply preserved.
8. `searchGroupMessages(text:, senderId:, pinned:, hasReactions:)` + `getGroupMedia(mediaType:IMAGE)` → correct results.
9. `transferGroupOwnership` → owner changes; new owner can `leaveGroup`? (owner cannot leave); `requestGroupJoin` on a private group + `approveGroupJoinRequest`/`rejectGroupJoinRequest`.
10. `startGroupCall(mediaType:VOICE)` → `mediaType:VOICE`, `groupCallStarted` notification row created; `leaveGroupCall`/`toggleGroupCallMute` → participant events.
11. Typing: `notifyGroupTyping` → subscribed member receives `userTypingInGroup`.
12. Verify `MessageType.System` rows appear in `getGroupMessages` for member added / group updated and cannot be edited/deleted/reacted to.

- [ ] **Step 4: Fix any issues found and re-verify** (loop until the smoke tests pass).

### Task 21: Docs

**Files:**
- Modify: `AGENTS.md`, `GRAPHQL_SCHEMA.md`, `REALTIME_FEATURES.md`

- [ ] **Step 1: Update `REALTIME_FEATURES.md`**

Add the group chat feature table (entities incl. `GroupMessageMention`, `GroupMessageRead`, `GroupJoinRequest`, `GroupMemberSettings` folded into `ChatGroupMember`; new enums; GraphQL query/mutation/subscription list; call media types; notification types; subscription topic naming table).

- [ ] **Step 2: Update `GRAPHQL_SCHEMA.md`**

Document all new/changed group message, group, join-request, call-participant, and notification fields.

- [ ] **Step 3: Update `AGENTS.md`**

Add a "Group Chat" section: one migration (`GroupChatUpgrade`), `GroupMessageService` responsibilities, `GroupPermissions` as single source of truth, system messages as the audit trail, per-user read tracking (`GroupMessageRead.DeliveredAt`/`ReadAt`), unread counts computed from `ChatGroupMember.LastReadAt`, transaction + RowVersion rules.

- [ ] **Step 4: Commit**

```bash
git add AGENTS.md GRAPHQL_SCHEMA.md REALTIME_FEATURES.md
git commit -m "docs: document group chat upgrade"
```

---

## Self-Review

**Spec coverage:** Every spec section maps to tasks — §2 data model (Tasks 1–6), §3.1 messages/mentions/replies/media/search/reads/pins (Task 10), §3.2 groups/invites/join-requests/mute/presence (Task 11), §3.3 calls (Task 12), §4 GraphQL (Tasks 13–19), §5 validation (Task 19), §6 performance (indexes Task 3, DataLoaders Task 13, pagination everywhere), §7 transactions + concurrency (Tasks 4/10/11), §8 docs (Task 21), production additions 1–15 (MessageStatus Task 1/2/10, unread counts Task 10, last-message preview Task 11, typing Task 17/18, presence Task 11, muting Task 11, leave rules Task 11, join requests Task 11, search filters Task 10, media gallery Task 10, system messages Task 2/10/11, centralized permissions Task 8, RowVersion Task 2/3, transactions Task 4/10/11).

**Placeholder scan:** The plan contains no TBD/TODO. The few "notes for the implementer" items are verification instructions (e.g. confirm `UserDto` property name, confirm `CallHistoryDto.GroupId`), not placeholders — each points to the exact file to verify and gives the fallback behavior.

**Type consistency:** `IGroupMessageService.SendAsync(groupId, senderId, messageType, content, file, replyToMessageId)` is used identically by `GroupMessageMutations` (Task 17) and implemented in Task 10. `InsertSystemMessageAsync(ChatGroup, Guid, string, string?, ct)` matches usage in Tasks 11/12. `GroupMessageDto.Content` (not `Text`) is used everywhere after Task 5. `GroupPermissions.CanManageInvite` is used by `GetInviteCodeAsync`/`GenerateInviteCodeAsync`/`RevokeInviteCodeAsync` and defined in Task 8. Subscription payload types (`Guid` for delete/reaction events, DTOs otherwise) match what `GroupMessageService` publishes in Task 10 and what `CallSubscription` subscribes to in Task 18.
