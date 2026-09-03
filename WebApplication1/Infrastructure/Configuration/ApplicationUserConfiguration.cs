using Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace Infrastructure.Configuration
{
    public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            builder.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(u => u.Bio)
                .HasMaxLength(500);

            builder.Property(u => u.ProfilePictureUrl)
                .HasMaxLength(500);

            builder.Property(u => u.CoverPictureUrl)
                .HasMaxLength(500);

            // Map DateOnly directly to SQL Server 'date' type
            builder.Property(u => u.DateOfBirth)
                .HasColumnType("date")
                .IsRequired();

            // Display names are not unique; usernames are enforced by Identity.
            builder.HasIndex(u => u.DisplayName);
            builder.HasIndex(u => u.DateOfBirth);
        }
    }

}
