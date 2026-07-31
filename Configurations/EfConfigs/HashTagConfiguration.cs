using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Configurations.EfConfigs
{
    public class HashTagConfiguration : IEntityTypeConfiguration<Hashtag>
    {
        public void Configure(EntityTypeBuilder<Hashtag> builder)
        {
            builder.ToTable("Hashtags");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Tag)
                  .IsRequired()
                  .HasMaxLength(128);

            builder.HasIndex(x => x.Tag)
                    .IsUnique();

            builder.HasMany(a => a.PostHashtags)
                   .WithOne(a => a.Hashtag)
                   .HasForeignKey(a => a.HashtagId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
