using Domain.Common;
using Domain.Entites;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Infrastructure.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Post> Posts => Set<Post>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<PostLikes> PostLikes => Set<PostLikes>();
        public DbSet<Friendship> Friendships => Set<Friendship>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<EmailOtp> EmailOtps => Set<EmailOtp>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Registers all configuration classes in this assembly
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AuditBaseEntities();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            AuditBaseEntities();
            return base.SaveChanges();
        }

        private void AuditBaseEntities()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity.GetType().BaseType != null &&
                            e.Entity.GetType().BaseType!.IsGenericType &&
                            e.Entity.GetType().BaseType!.GetGenericTypeDefinition() == typeof(BaseEntity<>));

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    var createdAtProperty = entry.Property(nameof(BaseEntity<int>.CreatedAt));
                    if (createdAtProperty.CurrentValue == null || (DateTime)createdAtProperty.CurrentValue == default)
                    {
                        createdAtProperty.CurrentValue = DateTime.UtcNow;
                    }
                }
                else if (entry.State == EntityState.Deleted)
                {
                    // Convert hard deletes into soft deletes automatically
                    entry.State = EntityState.Modified;
                    entry.Property(nameof(BaseEntity<int>.IsDeleted)).CurrentValue = true;
                }
            }
        }
    }
}
