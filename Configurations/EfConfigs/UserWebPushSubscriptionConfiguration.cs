using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class UserWebPushSubscriptionConfiguration : IEntityTypeConfiguration<UserWebPushSubscription>
    {
        public void Configure(EntityTypeBuilder<UserWebPushSubscription> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Endpoint).IsRequired();
            builder.Property(s => s.P256dh).IsRequired();
            builder.Property(s => s.Auth).IsRequired();

            builder.HasIndex(s => s.UserId);
            builder.HasIndex(s => s.Endpoint).IsUnique();

            builder.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
