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
