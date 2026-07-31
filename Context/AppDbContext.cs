using BlogGraphQlApp.Data.Configurations;
using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Reflection;

namespace BlogGraphQlApp.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Post> Posts => Set<Post>();
        public DbSet<Reel> Reels => Set<Reel>();
        public DbSet<Reaction> Reactions => Set<Reaction>();
        public DbSet<Reply> Replies => Set<Reply>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<UserFollow> UserFollows => Set<UserFollow>();
        public DbSet<UserInteraction> UserInteractions => Set<UserInteraction>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<PostHashtag> PostHashtags => Set<PostHashtag>();
        public DbSet<Hashtag> Hashtags => Set<Hashtag>();
        public DbSet<PostMention> PostMentions => Set<PostMention>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserFollow>()
        .HasKey(uf => new { uf.FollowerId, uf.FollowingId });

            modelBuilder.Entity<UserFollow>()
                .HasOne(uf => uf.Follower)
                .WithMany(u => u.Following)
                .HasForeignKey(uf => uf.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserFollow>()
                .HasOne(uf => uf.Following)
                .WithMany(u => u.Followers)
                .HasForeignKey(uf => uf.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.ReplyToMessage)
                .WithMany()
                .HasForeignKey(m => m.ReplyToMessageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ModerationResult>(entity =>
            {
                entity.Property(m => m.Categories)
                      .HasColumnType("json");    
            });



            modelBuilder.Entity<ModerationResult>().ToTable("ModerationResults");
            modelBuilder.Entity<AiUsage>().ToTable("AiUsages");


            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            base.OnModelCreating(modelBuilder);
        }
    }
}