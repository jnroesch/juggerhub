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

    // Feature 012 — Badges & Achievements (two separate families).
    public DbSet<BadgeDefinition> BadgeDefinitions => Set<BadgeDefinition>();

    public DbSet<BadgeIcon> BadgeIcons => Set<BadgeIcon>();

    public DbSet<BadgeAward> BadgeAwards => Set<BadgeAward>();

    public DbSet<AchievementDefinition> AchievementDefinitions => Set<AchievementDefinition>();

    public DbSet<AchievementIcon> AchievementIcons => Set<AchievementIcon>();

    public DbSet<AchievementAward> AchievementAwards => Set<AchievementAward>();

    // Feature 013 — append-only admin account-action log.
    public DbSet<AdminActionRecord> AdminActionRecords => Set<AdminActionRecord>();

    // Feature 016 — event parties (temporary team subset per event).
    public DbSet<Party> Parties => Set<Party>();

    public DbSet<PartyMember> PartyMembers => Set<PartyMember>();

    public DbSet<PartyNewsPost> PartyNewsPosts => Set<PartyNewsPost>();

    public DbSet<PartyAdminInvitation> PartyAdminInvitations => Set<PartyAdminInvitation>();

    // Feature 017 — event marketplace (mercenaries).
    public DbSet<MercenaryListing> MercenaryListings => Set<MercenaryListing>();

    public DbSet<MarketRequest> MarketRequests => Set<MarketRequest>();

    // Feature 018 — team-scoped recurring trainings.
    public DbSet<Training> Trainings => Set<Training>();

    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();

    public DbSet<TrainingResponse> TrainingResponses => Set<TrainingResponse>();

    // Feature 019: chat.
    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PlayerProfile>(entity =>
        {
            // Feature 013 ban invisibility: a banned (soft-deleted) account's profile is
            // hidden from EVERY query by default — public profile, browse, rosters,
            // participant lists all drop out without per-call-site Where clauses (fails
            // closed). Only the admin services opt out via IgnoreQueryFilters(); the
            // admin users list is the one place banned players remain findable.
            entity.HasQueryFilter(p => p.User.Status != AccountStatus.Banned);
            entity.Property(p => p.Handle).HasMaxLength(30).IsRequired();
            entity.Property(p => p.DisplayName).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Hometown).HasMaxLength(80);
            entity.Property(p => p.Description).HasMaxLength(280);
            // Feature 026: anonymous visibility is opt-in. Non-null, default private — the
            // migration backfills every existing row to false (FR-017/FR-018).
            entity.Property(p => p.IsPublic).IsRequired().HasDefaultValue(false);

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
            // Matches the PlayerProfile ban filter (013) so direct pompfen queries and
            // includes stay consistent with their (possibly hidden) parent profile.
            entity.HasQueryFilter(pp => pp.Profile.User.Status != AccountStatus.Banned);

            // A profile selects each pompfe at most once.
            entity.HasIndex(pp => new { pp.ProfileId, pp.Pompfe }).IsUnique();

            entity.HasOne(pp => pp.Profile)
                .WithMany(p => p.Pompfen)
                .HasForeignKey(pp => pp.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProfileAvatar>(entity =>
        {
            // Matches the PlayerProfile ban filter (013): a banned player's avatar is
            // not served either.
            entity.HasQueryFilter(a => a.Profile.User.Status != AccountStatus.Banned);

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
            // Matches the PlayerProfile ban filter (013): banned players drop out of
            // participant lists queried directly off this set.
            entity.HasQueryFilter(ep => ep.Profile.User.Status != AccountStatus.Banned);

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

        // ---- Feature 012: Badges & Achievements (two parallel families) ----

        builder.Entity<BadgeDefinition>(entity =>
        {
            entity.Property(d => d.Name).HasMaxLength(60).IsRequired();
            entity.Property(d => d.Description).HasMaxLength(280).IsRequired();
            // Grant pickers list active (non-retired) definitions.
            entity.HasIndex(d => d.IsRetired);

            entity.HasOne(d => d.Icon)
                .WithOne(i => i.Definition)
                .HasForeignKey<BadgeIcon>(i => i.BadgeDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BadgeIcon>(entity =>
        {
            entity.Property(i => i.ContentType).HasMaxLength(64).IsRequired();
        });

        builder.Entity<BadgeAward>(entity =>
        {
            entity.Property(a => a.RevokedReason).HasMaxLength(280);
            entity.Property(a => a.Note).HasMaxLength(280);

            // Exactly one subject (player XOR team).
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_BadgeAward_OneSubject",
                "(\"PlayerProfileId\" IS NOT NULL) <> (\"TeamId\" IS NOT NULL)"));

            // At most one ACTIVE award per (definition, subject); revoked rows allow a re-grant.
            entity.HasIndex(a => new { a.BadgeDefinitionId, a.PlayerProfileId })
                .IsUnique()
                .HasFilter("\"Status\" = 0 AND \"PlayerProfileId\" IS NOT NULL");
            entity.HasIndex(a => new { a.BadgeDefinitionId, a.TeamId })
                .IsUnique()
                .HasFilter("\"Status\" = 0 AND \"TeamId\" IS NOT NULL");
            // Back the embedded display query: a subject's active awards.
            entity.HasIndex(a => a.PlayerProfileId);
            entity.HasIndex(a => a.TeamId);

            entity.HasOne(a => a.Definition)
                .WithMany(d => d.Awards)
                .HasForeignKey(a => a.BadgeDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.PlayerProfile)
                .WithMany()
                .HasForeignKey(a => a.PlayerProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Team)
                .WithMany()
                .HasForeignKey(a => a.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            // Preserve who granted; admins aren't deleted casually.
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.GrantedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AchievementDefinition>(entity =>
        {
            entity.Property(d => d.Name).HasMaxLength(60).IsRequired();
            entity.Property(d => d.Description).HasMaxLength(280).IsRequired();
            entity.HasIndex(d => d.IsRetired);

            entity.HasOne(d => d.Icon)
                .WithOne(i => i.Definition)
                .HasForeignKey<AchievementIcon>(i => i.AchievementDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AchievementIcon>(entity =>
        {
            entity.Property(i => i.ContentType).HasMaxLength(64).IsRequired();
        });

        builder.Entity<AchievementAward>(entity =>
        {
            entity.Property(a => a.RevokedReason).HasMaxLength(280);
            entity.Property(a => a.Note).HasMaxLength(280);
            entity.Property(a => a.ContextLabel).HasMaxLength(120);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_AchievementAward_OneSubject",
                "(\"PlayerProfileId\" IS NOT NULL) <> (\"TeamId\" IS NOT NULL)"));

            entity.HasIndex(a => new { a.AchievementDefinitionId, a.PlayerProfileId })
                .IsUnique()
                .HasFilter("\"Status\" = 0 AND \"PlayerProfileId\" IS NOT NULL");
            entity.HasIndex(a => new { a.AchievementDefinitionId, a.TeamId })
                .IsUnique()
                .HasFilter("\"Status\" = 0 AND \"TeamId\" IS NOT NULL");
            entity.HasIndex(a => a.PlayerProfileId);
            entity.HasIndex(a => a.TeamId);

            entity.HasOne(a => a.Definition)
                .WithMany(d => d.Awards)
                .HasForeignKey(a => a.AchievementDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.PlayerProfile)
                .WithMany()
                .HasForeignKey(a => a.PlayerProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Team)
                .WithMany()
                .HasForeignKey(a => a.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.GrantedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Feature 013: admin account-action log ----

        builder.Entity<AdminActionRecord>(entity =>
        {
            entity.Property(r => r.Note).HasMaxLength(280);

            // Future per-player history view reads a target's actions newest-first.
            entity.HasIndex(r => new { r.TargetUserId, r.CreatedDate });

            // History must never vanish with an account row.
            entity.HasOne(r => r.Actor)
                .WithMany()
                .HasForeignKey(r => r.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Target)
                .WithMany()
                .HasForeignKey(r => r.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Feature 016: event parties ----

        builder.Entity<Party>(entity =>
        {
            entity.Property(p => p.Message).HasMaxLength(500);
            entity.Property(p => p.RecruitBlurb).HasMaxLength(500);

            // One party per (team, event) — the race-safe backstop behind the service pre-check.
            entity.HasIndex(p => new { p.TeamId, p.EventId }).IsUnique();
            // The team-space discovery read scans a team's active parties.
            entity.HasIndex(p => p.TeamId);
            // 1:1 applied↔signup link (only set while Applied).
            entity.HasIndex(p => p.EventSignupId).IsUnique().HasFilter("\"EventSignupId\" IS NOT NULL");

            entity.HasOne(p => p.Team)
                .WithMany()
                .HasForeignKey(p => p.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(p => p.Event)
                .WithMany()
                .HasForeignKey(p => p.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            // The party owns the signup's lifecycle explicitly (apply/withdraw/disband); do not
            // cascade-delete the party when the signup is removed.
            entity.HasOne(p => p.EventSignup)
                .WithMany()
                .HasForeignKey(p => p.EventSignupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(p => p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PartyMember>(entity =>
        {
            // At most one row per member per party.
            entity.HasIndex(m => new { m.PartyId, m.UserId }).IsUnique();
            // Roster group reads + In-count.
            entity.HasIndex(m => new { m.PartyId, m.Status });

            entity.HasOne(m => m.Party)
                .WithMany(p => p.Members)
                .HasForeignKey(m => m.PartyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PartyNewsPost>(entity =>
        {
            entity.Property(n => n.Body).HasMaxLength(1000).IsRequired();
            entity.HasIndex(n => new { n.PartyId, n.CreatedDate });

            entity.HasOne(n => n.Party)
                .WithMany(p => p.News)
                .HasForeignKey(n => n.PartyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(n => n.Author)
                .WithMany()
                .HasForeignKey(n => n.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PartyAdminInvitation>(entity =>
        {
            entity.Property(i => i.Token).HasMaxLength(64).IsRequired();

            entity.HasIndex(i => i.Token).IsUnique();
            entity.HasIndex(i => i.PartyId);
            // At most one active (pending) shared link per party (Kind=Link=0, Status=Pending=0).
            entity.HasIndex(i => i.PartyId)
                .IsUnique()
                .HasFilter("\"Kind\" = 0 AND \"Status\" = 0");
            // At most one pending targeted invite per (party, user) (Kind=Targeted=1, Status=Pending=0).
            entity.HasIndex(i => new { i.PartyId, i.TargetUserId })
                .IsUnique()
                .HasFilter("\"Kind\" = 1 AND \"Status\" = 0");

            entity.HasOne(i => i.Party)
                .WithMany(p => p.Invitations)
                .HasForeignKey(i => i.PartyId)
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

        // ---- Feature 017: event marketplace (mercenaries) ----

        builder.Entity<MercenaryListing>(entity =>
        {
            entity.Property(l => l.Pitch).HasMaxLength(280).IsRequired();

            // One live listing per (user, event) — race-safe backstop behind the service pre-check.
            entity.HasIndex(l => new { l.UserId, l.EventId }).IsUnique();
            // The board's free-agents read scans an event's listings.
            entity.HasIndex(l => l.EventId);

            entity.HasOne(l => l.Event)
                .WithMany()
                .HasForeignKey(l => l.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MarketRequest>(entity =>
        {
            // At most one active (pending) request per (party, user) (Status=Pending=0) — the race-safe
            // backstop behind the service pre-check; terminal rows don't conflict (a fresh request may
            // follow a decline/revoke). Mirrors the PartyAdminInvitation pending filters.
            entity.HasIndex(r => new { r.PartyId, r.UserId })
                .IsUnique()
                .HasFilter("\"Status\" = 0");
            // The recruiting inbox reads a party's pending applications/invites.
            entity.HasIndex(r => new { r.PartyId, r.Status });
            // The mercenary inbox + dashboard read a user's requests.
            entity.HasIndex(r => new { r.UserId, r.Status });

            entity.HasOne(r => r.Party)
                .WithMany(p => p.MarketRequests)
                .HasForeignKey(r => r.PartyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // CreatedByUserId is a plain audit column (the applicant or inviting admin); no FK/nav.
        });

        // ---- Feature 018: team-scoped recurring trainings ----

        builder.Entity<Training>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(120).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(2000);
            entity.Property(t => t.Location).HasMaxLength(300);
            entity.Property(t => t.VirtualLink).HasMaxLength(500);

            // The Trainings tab and active-series overview scan a team's trainings.
            entity.HasIndex(t => t.TeamId);

            entity.HasOne(t => t.Team)
                .WithMany()
                .HasForeignKey(t => t.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.CreatedBy)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TrainingSession>(entity =>
        {
            entity.Property(s => s.LocationOverride).HasMaxLength(300);
            entity.Property(s => s.VirtualLinkOverride).HasMaxLength(500);

            // The tab list and dashboard agenda order by (team, date); the reconciliation reads by training.
            entity.HasIndex(s => new { s.TeamId, s.SessionDate });
            entity.HasIndex(s => s.TrainingId);

            entity.HasOne(s => s.Training)
                .WithMany(t => t.Sessions)
                .HasForeignKey(s => s.TrainingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TrainingResponse>(entity =>
        {
            // Exactly one current answer per person per session (upsert on change).
            entity.HasIndex(r => new { r.TrainingSessionId, r.UserId }).IsUnique();
            entity.HasIndex(r => r.TrainingSessionId);

            entity.HasOne(r => r.Session)
                .WithMany(s => s.Responses)
                .HasForeignKey(r => r.TrainingSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Feature 019: chat ----

        builder.Entity<Conversation>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(120);
            entity.Property(c => c.DirectPairKey).HasMaxLength(73); // two 36-char GUIDs + ':'

            // Exactly one chat per team / per party (FR-024), enforced in the DATABASE rather than by
            // a service check-then-insert: EnsureForTeamAsync is racy by nature (two roster members
            // opening Chat at the same moment), and the filtered unique index is what makes the loser
            // of that race collide instead of creating a second chat.
            //
            // Feature 027 reuses TeamId for TeamInquiry threads too, so this one-per-team rule must be
            // scoped to the team CHAT (Kind = 2) — otherwise a second inquiry for the same team would
            // collide with the team chat. Inquiry uniqueness is a separate (target, requester) index below.
            entity.HasIndex(c => c.TeamId).IsUnique().HasFilter("\"TeamId\" IS NOT NULL AND \"Kind\" = 2");
            entity.HasIndex(c => c.PartyId).IsUnique().HasFilter("\"PartyId\" IS NOT NULL");

            // At most one inquiry thread per (requester, target) (feature 027, FR-004). Same race-safety
            // reasoning as DirectPairKey: two tabs sending a first message at once must resolve to one
            // row, and only the database can promise it. Kind 4 = TeamInquiry, 5 = EventInquiry.
            entity.HasIndex(c => new { c.TeamId, c.RequesterUserId }).IsUnique().HasFilter("\"Kind\" = 4");
            entity.HasIndex(c => new { c.EventId, c.RequesterUserId }).IsUnique().HasFilter("\"Kind\" = 5");

            // At most one direct conversation per pair (FR-008). Same reasoning: two clients starting
            // the same DM simultaneously must resolve to one row, and only the database can promise
            // that. The key is built from the ORDERED pair, so (A,B) and (B,A) collide.
            entity.HasIndex(c => c.DirectPairKey).IsUnique().HasFilter("\"DirectPairKey\" IS NOT NULL");

            // The inbox orders by recency.
            entity.HasIndex(c => c.LastMessageDate);

            entity.HasOne(c => c.Team)
                .WithMany()
                .HasForeignKey(c => c.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.Party)
                .WithMany()
                .HasForeignKey(c => c.PartyId)
                .OnDelete(DeleteBehavior.Restrict);
            // Restrict (fail-closed), mirroring Team/Party: a delete path that forgets to archive-first
            // (snapshotting the derived roster) fails loudly rather than orphaning an Active inquiry
            // whose membership resolves to nobody. Archival nulls EventId before any event hard-delete.
            entity.HasOne(c => c.Event)
                .WithMany()
                .HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.Requester)
                .WithMany()
                .HasForeignKey(c => c.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ConversationParticipant>(entity =>
        {
            // One state row per player per conversation. Also makes a duplicate add a no-op at the
            // database level rather than relying on a service-side existence check (US3).
            entity.HasIndex(p => new { p.ConversationId, p.UserId }).IsUnique();
            // Drives "my inbox".
            entity.HasIndex(p => p.UserId);

            entity.HasOne(p => p.Conversation)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatMessage>(entity =>
        {
            entity.Property(m => m.Body).HasMaxLength(2000);

            // Backs BOTH hot paths on one composite: the keyset history page
            // (WHERE ConversationId = x AND Id < cursor ORDER BY Id DESC) and the unread count
            // (WHERE ConversationId = x AND Id > lastRead). Id is a UUIDv7, so it sorts chronologically.
            entity.HasIndex(m => new { m.ConversationId, m.Id });
            entity.HasIndex(m => m.SenderId);

            entity.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            // Restrict, not cascade: a departing account must not silently delete its side of other
            // people's conversations. Feature 013 soft-deletes accounts anyway; history is preserved
            // and the sender projects to a neutral placeholder.
            entity.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserBlock>(entity =>
        {
            // Blocking is idempotent — a double-block collides here rather than stacking rows.
            entity.HasIndex(b => new { b.BlockerUserId, b.BlockedUserId }).IsUnique();
            // The "has anyone blocked me?" direction, which every direct send checks.
            entity.HasIndex(b => b.BlockedUserId);

            // Restrict on both sides: a block is a safety record and must never be dropped as a side
            // effect of touching a user row.
            entity.HasOne(b => b.Blocker)
                .WithMany()
                .HasForeignKey(b => b.BlockerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(b => b.Blocked)
                .WithMany()
                .HasForeignKey(b => b.BlockedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
