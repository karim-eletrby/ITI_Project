using Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.TargetUrl)
            .HasMaxLength(500);

        builder.Property(n => n.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasQueryFilter(n => !n.IsDeleted);

        // Fast lookup for unread notifications & badge counter
        builder.HasIndex(n => new { n.RecipientId, n.IsRead });

        // Change CASCADE to Restrict / NoAction to prevent SQL Server Error 1785
        builder.HasOne(n => n.Recipient)
            .WithMany(u => u.ReceivedNotifications)
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Change SET NULL to Restrict / NoAction
        builder.HasOne(n => n.TriggeredBy)
            .WithMany(u => u.TriggeredNotifications)
            .HasForeignKey(n => n.TriggeredById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}