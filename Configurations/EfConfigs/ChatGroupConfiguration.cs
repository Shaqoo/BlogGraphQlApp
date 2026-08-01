using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class ChatGroupConfiguration : IEntityTypeConfiguration<ChatGroup>
    {
        public void Configure(EntityTypeBuilder<ChatGroup> builder)
        {
            builder.HasKey(g => g.Id);

            builder.Property(g => g.Name).IsRequired().HasMaxLength(120);
            builder.Property(g => g.Description).HasMaxLength(500);
            builder.Property(g => g.InviteCode).HasMaxLength(32);
            builder.Property(g => g.IsPrivate).HasDefaultValue(false);
            builder.Property(g => g.Archived).HasDefaultValue(false);
            builder.Property(g => g.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            builder.Property(g => g.RowVersion).IsRowVersion();

            builder.HasIndex(g => g.InviteCode).IsUnique();

            builder.HasOne(g => g.CreatedByUser).WithMany().HasForeignKey(g => g.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(g => g.LastMessage).WithOne().HasForeignKey<ChatGroup>(g => g.LastMessageId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
