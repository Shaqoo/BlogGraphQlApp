using BlogGraphQlApp.Entities;
using BlogGraphQlApp.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class GroupVideoCallConfiguration : IEntityTypeConfiguration<GroupVideoCall>
    {
        public void Configure(EntityTypeBuilder<GroupVideoCall> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.RoomName).IsRequired();
            builder.Property(c => c.DailyRoomUrl).IsRequired();
            builder.Property(c => c.MediaType).HasDefaultValue(CallMediaType.Video);

            builder.HasIndex(c => c.CallId).IsUnique();
            builder.HasIndex(c => c.GroupId);
            builder.HasIndex(c => c.Status);

            builder.HasOne(c => c.Group).WithMany(g => g.VideoCalls).HasForeignKey(c => c.GroupId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
