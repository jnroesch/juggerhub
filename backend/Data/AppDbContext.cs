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

    /// <summary>
    /// Maps to the PostgreSQL <c>unaccent(text)</c> function (from the <c>unaccent</c> extension,
    /// created by the AddDiscoveryFields migration). Used only inside LINQ queries for
    /// accent-insensitive search (feature 007) — e.g. <c>ILike(Unaccent(col), Unaccent(pattern))</c>.
    /// Never call it in application code; it throws outside query translation.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1822", Justification = "EF DbFunction target must be a discoverable public static method.")]
    [DbFunction("unaccent", Schema = "public")]
    public static string Unaccent(string value) =>
        throw new NotSupportedException("Unaccent is only usable inside an EF Core query.");

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

    public DbSet<EventSignup> EventSignups => Set<EventSignup>();

    public DbSet<EventAdmin> EventAdmins => Set<EventAdmin>();

    public DbSet<EventAdminInvitation> EventAdminInvitations => Set<EventAdminInvitation>();

    public DbSet<EventContact> EventContacts => Set<EventContact>();

    public DbSet<EventNewsPost> EventNewsPosts => Set<EventNewsPost>();

    public DbSet<TeamJoinRequest> TeamJoinRequests => Set<TeamJoinRequest>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PlayerProfile>(entity =>
        {
            entity.Property(p => p.Handle).HasMaxLength(30).IsRequired();
            entity.Property(p => p.DisplayName).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Hometown).HasMaxLength(80);
            entity.Property(p => p.Description).HasMaxLength(280);
            entity.Property(p => p.AppearInSearch).HasDefaultValue(false);

            // Handle addresses the public profile — unique & the true uniqueness guarantee.
            entity.HasIndex(p => p.Handle).IsUnique();
            // Partial index backs the opt-in browse scan (feature 007) — only opted-in rows.
            entity.HasIndex(p => p.AppearInSearch).HasFilter("\"AppearInSearch\"");
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
            entity.Property(e => e.CustomTypeLabel).HasMaxLength(40);
            entity.Property(e => e.Description).HasMaxLength(4000).IsRequired();
            // Legacy free-text location, retained for existing activity display (see Event remarks).
            entity.Property(e => e.Location).HasMaxLength(120).IsRequired();
            entity.Property(e => e.VenueName).HasMaxLength(120);
            entity.Property(e => e.Street).HasMaxLength(160);
            entity.Property(e => e.PostalCode).HasMaxLength(20);
            entity.Property(e => e.City).HasMaxLength(120);
            entity.Property(e => e.Country).HasMaxLength(80);
            entity.Property(e => e.VirtualLink).HasMaxLength(500);
            entity.Property(e => e.FeeAmount).HasPrecision(12, 2);
            entity.Property(e => e.FeeCurrency).HasMaxLength(3);
            entity.Property(e => e.FeeRecipientName).HasMaxLength(120);
            entity.Property(e => e.FeeIban).HasMaxLength(34);
            entity.HasIndex(e => e.StartsAt);
            // Browse excludes cancelled events (feature 007).
            entity.HasIndex(e => e.Status);
        });

        builder.Entity<EventSignup>(entity =>
        {
            // Exactly one subject per row: an individual (UserId) XOR a team (TeamId).
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_EventSignup_Subject", "(\"UserId\" IS NULL) <> (\"TeamId\" IS NULL)"));
            // Occupied-count + group reads.
            entity.HasIndex(s => new { s.EventId, s.Status });
            // Home "up next" (feature 008) scans a player's own sign-ups and their teams' entries;
            // the FK-convention indexes on UserId and TeamId already cover those scans (no new index).
            // No duplicate entry per event (partial per subject kind).
            entity.HasIndex(s => new { s.EventId, s.UserId }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
            entity.HasIndex(s => new { s.EventId, s.TeamId }).IsUnique().HasFilter("\"TeamId\" IS NOT NULL");

            entity.HasOne(s => s.Event)
                .WithMany(e => e.Signups)
                .HasForeignKey(s => s.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.Team)
                .WithMany()
                .HasForeignKey(s => s.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventAdmin>(entity =>
        {
            // One admin grant per user per event; (EventId) backs the last-admin count.
            entity.HasIndex(a => new { a.EventId, a.UserId }).IsUnique();
            entity.HasIndex(a => a.EventId);
            entity.HasIndex(a => a.UserId);

            entity.HasOne(a => a.Event)
                .WithMany(e => e.Admins)
                .HasForeignKey(a => a.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventAdminInvitation>(entity =>
        {
            entity.Property(i => i.Token).HasMaxLength(64).IsRequired();

            entity.HasIndex(i => i.Token).IsUnique();
            entity.HasIndex(i => i.EventId);
            // At most one active (pending) shared link per event (Kind=Link=0, Status=Pending=0).
            entity.HasIndex(i => i.EventId)
                .IsUnique()
                .HasFilter("\"Kind\" = 0 AND \"Status\" = 0");
            // At most one pending targeted invite per (event, user) (Kind=Targeted=1, Status=Pending=0).
            entity.HasIndex(i => new { i.EventId, i.TargetUserId })
                .IsUnique()
                .HasFilter("\"Kind\" = 1 AND \"Status\" = 0");

            entity.HasOne(i => i.Event)
                .WithMany(e => e.Invitations)
                .HasForeignKey(i => i.EventId)
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

        builder.Entity<EventContact>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(120).IsRequired();
            entity.Property(c => c.Role).HasMaxLength(80).IsRequired();
            entity.Property(c => c.Phone).HasMaxLength(40);
            entity.Property(c => c.Email).HasMaxLength(256);
            entity.HasIndex(c => c.EventId);

            entity.HasOne(c => c.Event)
                .WithMany(e => e.Contacts)
                .HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventNewsPost>(entity =>
        {
            entity.Property(n => n.Body).HasMaxLength(2000).IsRequired();
            entity.HasIndex(n => new { n.EventId, n.CreatedDate });

            entity.HasOne(n => n.Event)
                .WithMany(e => e.News)
                .HasForeignKey(n => n.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(n => n.Author)
                .WithMany()
                .HasForeignKey(n => n.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(t => t.BeginnersWelcome).HasDefaultValue(false);

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

        builder.Entity<TeamJoinRequest>(entity =>
        {
            // The admin request queue reads pending requests per team.
            entity.HasIndex(r => new { r.TeamId, r.Status });
            // At most one PENDING request per (team, player) (Status = Pending = 0).
            entity.HasIndex(r => new { r.TeamId, r.UserId })
                .IsUnique()
                .HasFilter("\"Status\" = 0");

            entity.HasOne(r => r.Team)
                .WithMany()
                .HasForeignKey(r => r.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.DecidedBy)
                .WithMany()
                .HasForeignKey(r => r.DecidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.Property(n => n.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(n => n.DedupeKey).HasMaxLength(200);

            // The inbox list: a recipient's notifications, newest-first.
            entity.HasIndex(n => new { n.RecipientUserId, n.CreatedDate });
            // The unread badge count — partial index over only unread rows.
            entity.HasIndex(n => n.RecipientUserId)
                .HasFilter("NOT \"IsRead\"")
                .HasDatabaseName("IX_Notifications_RecipientUserId_Unread");
            // Idempotency: at most one notification per (recipient, dedupe key).
            entity.HasIndex(n => new { n.RecipientUserId, n.DedupeKey })
                .IsUnique()
                .HasFilter("\"DedupeKey\" IS NOT NULL");

            entity.HasOne(n => n.Recipient)
                .WithMany()
                .HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Actor is display-only; keep the notification if the actor account is removed.
            entity.HasOne(n => n.Actor)
                .WithMany()
                .HasForeignKey(n => n.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<NotificationPreference>(entity =>
        {
            // One row per (user, category, channel); also the per-user read + upsert target.
            entity.HasIndex(p => new { p.UserId, p.Category, p.Channel }).IsUnique();

            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
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
