using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class ReelConfiguration : IEntityTypeConfiguration<Reel>
    {
        public void Configure(EntityTypeBuilder<Reel> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Title).IsRequired().HasMaxLength(150);

            builder.HasOne(r => r.User).WithMany(u => u.Reels).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(r => r.Reactions).WithOne(reaction => reaction.Reel).HasForeignKey(reaction => reaction.ReelId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(r => r.Replies).WithOne(reply => reply.Reel).HasForeignKey(reply => reply.ReelId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}