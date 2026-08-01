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
