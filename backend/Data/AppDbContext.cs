using JuggerHub.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Data;

/// <summary>
/// The application's EF Core context. Backed by PostgreSQL (Npgsql) and built on
/// ASP.NET Core Identity with <see cref="Guid"/> keys (UUIDv7).
/// </summary>
/// <remarks>
/// Registers <see cref="AuditFieldsInterceptor"/> so audit timestamps are set
/// automatically. The initial migration creates the Identity schema; future
/// domain entities derive from <see cref="BaseEntity"/>.
/// </remarks>
public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(entity =>
        {
            entity.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(t => t.CreatedByIp).HasMaxLength(64);
            entity.Property(t => t.RevokedReason).HasMaxLength(64);

            // Lookups hash the presented cookie value and match here; uniqueness also
            // guards against (astronomically unlikely) collisions.
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.UserId);
            entity.HasIndex(t => t.FamilyId);

            entity.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
