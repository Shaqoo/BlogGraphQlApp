using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class ChatGroupMemberConfiguration : IEntityTypeConfiguration<ChatGroupMember>
    {
        public void Configure(EntityTypeBuilder<ChatGroupMember> builder)
        {
            builder.HasKey(m => m.Id);

            builder.HasIndex(m => m.GroupId);
            builder.HasIndex(m => m.UserId);
            builder.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();

            builder.Property(m => m.Muted).HasDefaultValue(false);
            builder.Property(m => m.NotificationLevel).HasDefaultValue(NotificationLevel.All);

            builder.HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
