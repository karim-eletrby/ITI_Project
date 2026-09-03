using Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace Infrastructure.Configuration
{
    public class PostLikeConfiguration : IEntityTypeConfiguration<PostLikes>
    {
        public void Configure(EntityTypeBuilder<PostLikes> builder)
        {
            // Composite PK prevents duplicate likes per post/user
            builder.HasKey(pl => new { pl.PostId, pl.UserId });

            builder.Property(pl => pl.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            builder.HasOne(pl => pl.Post)
                .WithMany(p => p.Likes)
                .HasForeignKey(pl => pl.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pl => pl.User)
                .WithMany(u => u.PostLikes)
                .HasForeignKey(pl => pl.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
