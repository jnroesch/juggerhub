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

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();

    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();

    public DbSet<TeamNewsPost> TeamNewsPosts => Set<TeamNewsPost>();

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

            // Feature 005 — real team attribution; SetNull preserves activity history on team delete.
            entity.HasOne(ep => ep.Team)
                .WithMany()
                .HasForeignKey(ep => ep.TeamId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(ep => ep.TeamId);
        });

        builder.Entity<Team>(entity =>
        {
            entity.Property(t => t.Slug).HasMaxLength(30).IsRequired();
            entity.Property(t => t.Name).HasMaxLength(50).IsRequired();
            entity.Property(t => t.City).HasMaxLength(80);

            // Slug addresses the team (/t/<slug>) — unique & the true uniqueness guarantee.
            entity.HasIndex(t => t.Slug).IsUnique();
        });

        builder.Entity<TeamMembership>(entity =>
        {
            // One membership per user per team; (TeamId, Role) backs admin-count checks.
            entity.HasIndex(m => new { m.TeamId, m.UserId }).IsUnique();
            entity.HasIndex(m => new { m.TeamId, m.Role });
            entity.HasIndex(m => m.UserId);

            entity.HasOne(m => m.Team)
                .WithMany(t => t.Memberships)
                .HasForeignKey(m => m.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeamInvitation>(entity =>
        {
            entity.Property(i => i.Token).HasMaxLength(64).IsRequired();

            entity.HasIndex(i => i.Token).IsUnique();
            entity.HasIndex(i => i.TeamId);
            // At most one active (pending) shared link per team (Kind=Link=0, Status=Pending=0).
            entity.HasIndex(i => i.TeamId)
                .IsUnique()
                .HasFilter("\"Kind\" = 0 AND \"Status\" = 0");
            // At most one pending targeted invite per (team, user) (Kind=Targeted=1, Status=Pending=0).
            entity.HasIndex(i => new { i.TeamId, i.TargetUserId })
                .IsUnique()
                .HasFilter("\"Kind\" = 1 AND \"Status\" = 0");

            entity.HasOne(i => i.Team)
                .WithMany(t => t.Invitations)
                .HasForeignKey(i => i.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.CreatedBy)
                .WithMany()
                .HasForeignKey(i => i.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.TargetUser)
                .WithMany()
                .HasForeignKey(i => i.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TeamNewsPost>(entity =>
        {
            entity.Property(n => n.Body).HasMaxLength(1000).IsRequired();
            entity.HasIndex(n => new { n.TeamId, n.CreatedDate });

            entity.HasOne(n => n.Team)
                .WithMany(t => t.News)
                .HasForeignKey(n => n.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.Author)
                .WithMany()
                .HasForeignKey(n => n.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
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
