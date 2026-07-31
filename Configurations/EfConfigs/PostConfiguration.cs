using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class PostConfiguration : IEntityTypeConfiguration<Post>
    {
        public void Configure(EntityTypeBuilder<Post> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Title).IsRequired().HasMaxLength(200);

            builder.HasIndex(p => p.Title).IsFullText();

            builder.HasOne(p => p.User).WithMany(u => u.Posts).HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(p => p.Reactions).WithOne(r => r.Post).HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(p => p.Replies).WithOne(r => r.Post).HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}