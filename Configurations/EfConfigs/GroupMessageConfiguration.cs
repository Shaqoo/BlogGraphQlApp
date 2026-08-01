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
