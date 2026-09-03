using Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configuration
{
    public class EmailOtpConfiguration : IEntityTypeConfiguration<EmailOtp>
    {
        public void Configure(EntityTypeBuilder<EmailOtp> builder)
        {
            builder.HasKey(o => o.Id);

            builder.Property(o => o.Email)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(o => o.TargetEmail)
                .HasMaxLength(256);

            builder.Property(o => o.CodeHash)
                .IsRequired()
                .HasMaxLength(128);

            builder.Property(o => o.Purpose)
                .IsRequired();

            builder.HasIndex(o => new { o.UserId, o.Purpose, o.Email, o.IsUsed });
            builder.HasIndex(o => new { o.UserId, o.Purpose, o.TargetEmail, o.IsUsed });
            builder.HasIndex(o => o.ExpiresAt);

            builder.HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
