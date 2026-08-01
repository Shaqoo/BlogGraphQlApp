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
