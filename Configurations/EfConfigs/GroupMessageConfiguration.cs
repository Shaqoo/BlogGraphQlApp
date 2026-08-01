using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class GroupMessageConfiguration : IEntityTypeConfiguration<GroupMessage>
    {
        public void Configure(EntityTypeBuilder<GroupMessage> builder)
        {
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Text).IsRequired().HasMaxLength(2000);

            builder.HasIndex(m => m.GroupId);
            builder.HasIndex(m => m.SenderId);

            builder.HasOne(m => m.Group).WithMany(g => g.Messages).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
