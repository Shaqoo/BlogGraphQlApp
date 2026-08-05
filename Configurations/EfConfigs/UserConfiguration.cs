using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(u => u.Username).IsUnique();

            builder.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(u => u.Email).IsUnique();

            builder.HasIndex(u => new { u.Username, u.Email, u.FullName })
                   .IsFullText();

            builder.Property(u => u.FailedLoginAttempts).HasDefaultValue(0);

            builder.HasMany(u => u.Posts).WithOne(p => p.User).HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(u => u.Reels).WithOne(r => r.User).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(u => u.Notifications).WithOne(n => n.User).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(u => u.Reactions).WithOne(n => n.User).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_Reactions_Users_UserId");
            builder.HasMany(u => u.Replies).WithOne(n => n.User).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_Replies_Users_UserId");
            builder.HasMany(u => u.Mentions).WithOne(n => n.MentionedUser).HasForeignKey(n => n.MentionedUserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(u => u.Messages).WithOne(n => n.Sender).HasForeignKey(n => n.SenderId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(u => u.Conversations).WithMany(n => n.Participants);
        }
    }
}