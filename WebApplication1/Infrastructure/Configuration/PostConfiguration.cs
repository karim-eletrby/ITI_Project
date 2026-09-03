using Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configuration
{
    public class PostConfiguration : IEntityTypeConfiguration<Post>
    {
        public void Configure(EntityTypeBuilder<Post> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Content)
                .IsRequired();

            builder.Property(p => p.MediaUrl)
                .HasMaxLength(500);

            builder.Property(p => p.Privacy)
                .IsRequired();

            builder.Property(p => p.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Global Soft Delete Filter
            builder.HasQueryFilter(p => !p.IsDeleted);

            // Feed Ordering Index
            builder.HasIndex(p => new { p.UserId, p.CreatedAt });

            builder.HasOne(p => p.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(p => p.SharedPost)
                .WithMany()
                .HasForeignKey(p => p.SharedPostId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
