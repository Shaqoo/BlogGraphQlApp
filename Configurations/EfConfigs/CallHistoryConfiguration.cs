using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class CallHistoryConfiguration : IEntityTypeConfiguration<CallHistory>
    {
        public void Configure(EntityTypeBuilder<CallHistory> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.RoomName).IsRequired();

            builder.HasIndex(c => c.CallId).IsUnique();
            builder.HasIndex(c => c.CallerId);
            builder.HasIndex(c => c.RecipientId);
            builder.HasIndex(c => c.GroupId);
            builder.HasIndex(c => c.Status);
            builder.HasIndex(c => c.StartedAt);

            builder.HasOne(c => c.Caller).WithMany().HasForeignKey(c => c.CallerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(c => c.Recipient).WithMany().HasForeignKey(c => c.RecipientId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(c => c.Group).WithMany().HasForeignKey(c => c.GroupId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(c => c.EndedByUser).WithMany().HasForeignKey(c => c.EndedByUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(c => c.Participants).WithOne(p => p.CallHistory).HasForeignKey(p => p.CallHistoryId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
