using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class ActiveVideoCallConfiguration : IEntityTypeConfiguration<ActiveVideoCall>
    {
        public void Configure(EntityTypeBuilder<ActiveVideoCall> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.RoomName).IsRequired();
            builder.Property(c => c.DailyRoomUrl).IsRequired();
            builder.Property(c => c.MediaType).HasDefaultValue(CallMediaType.Video);

            builder.HasIndex(c => c.CallId).IsUnique();
            builder.HasIndex(c => c.Status);
            builder.HasIndex(c => c.CallerId);
            builder.HasIndex(c => c.RecipientId);
            builder.HasIndex(c => c.MediaType);

            builder.HasOne(c => c.Caller).WithMany().HasForeignKey(c => c.CallerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(c => c.Recipient).WithMany().HasForeignKey(c => c.RecipientId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
