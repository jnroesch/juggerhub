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

    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();

    public DbSet<ProfilePompfe> ProfilePompfen => Set<ProfilePompfe>();

    public DbSet<ProfileAvatar> ProfileAvatars => Set<ProfileAvatar>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventParticipation> EventParticipations => Set<EventParticipation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PlayerProfile>(entity =>
        {
            entity.Property(p => p.Handle).HasMaxLength(30).IsRequired();
            entity.Property(p => p.DisplayName).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Hometown).HasMaxLength(80);
            entity.Property(p => p.Description).HasMaxLength(280);

            // Handle addresses the public profile — unique & the true uniqueness guarantee.
            entity.HasIndex(p => p.Handle).IsUnique();
            // 1:1 with the account.
            entity.HasIndex(p => p.UserId).IsUnique();

            entity.HasOne(p => p.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<PlayerProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Avatar)
                .WithOne(a => a.Profile)
                .HasForeignKey<ProfileAvatar>(a => a.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProfilePompfe>(entity =>
        {
            // A profile selects each pompfe at most once.
            entity.HasIndex(pp => new { pp.ProfileId, pp.Pompfe }).IsUnique();

            entity.HasOne(pp => pp.Profile)
                .WithMany(p => p.Pompfen)
                .HasForeignKey(pp => pp.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProfileAvatar>(entity =>
        {
            entity.Property(a => a.ContentType).HasMaxLength(64).IsRequired();
            entity.HasIndex(a => a.ProfileId).IsUnique();
        });

        builder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(120).IsRequired();
            entity.HasIndex(e => e.Date);
        });

        builder.Entity<EventParticipation>(entity =>
        {
            entity.Property(ep => ep.TeamLabel).HasMaxLength(80).IsRequired();
            // A player participates in a given event once.
            entity.HasIndex(ep => new { ep.ProfileId, ep.EventId }).IsUnique();

            entity.HasOne(ep => ep.Profile)
                .WithMany(p => p.Participations)
                .HasForeignKey(ep => ep.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ep => ep.Event)
                .WithMany(e => e.Participations)
                .HasForeignKey(ep => ep.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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
