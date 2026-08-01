using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class GroupCallParticipantHistoryConfiguration : IEntityTypeConfiguration<GroupCallParticipantHistory>
    {
        public void Configure(EntityTypeBuilder<GroupCallParticipantHistory> builder)
        {
            builder.HasKey(p => p.Id);

            builder.HasIndex(p => p.CallHistoryId);
            builder.HasIndex(p => p.UserId);

            builder.HasOne(p => p.CallHistory)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.CallHistoryId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
