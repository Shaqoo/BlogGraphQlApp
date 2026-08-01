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

            builder.HasOne(g => g.CreatedByUser).WithMany().HasForeignKey(g => g.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
