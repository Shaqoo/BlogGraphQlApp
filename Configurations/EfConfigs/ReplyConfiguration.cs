using BlogGraphQlApp.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.Data.Configurations
{
    public class ReplyConfiguration : IEntityTypeConfiguration<Reply>
    {
        public void Configure(EntityTypeBuilder<Reply> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Content).IsRequired();

            builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(r => r.Post).WithMany(p => p.Replies).HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(r => r.Reel).WithMany(re => re.Replies).HasForeignKey(r => r.ReelId).OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.ParentReply).WithMany(pr => pr.NestedReplies).HasForeignKey(r => r.ParentReplyId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}