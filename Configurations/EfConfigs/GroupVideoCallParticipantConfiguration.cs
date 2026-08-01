using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class GroupVideoCallParticipantConfiguration : IEntityTypeConfiguration<GroupVideoCallParticipant>
    {
        public void Configure(EntityTypeBuilder<GroupVideoCallParticipant> builder)
        {
            builder.HasKey(p => p.Id);

            builder.HasIndex(p => p.CallId);
            builder.HasIndex(p => p.UserId);
            builder.HasIndex(p => new { p.CallId, p.UserId }).IsUnique();

            builder.HasOne(p => p.Call).WithMany(c => c.Participants).HasForeignKey(p => p.CallId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
