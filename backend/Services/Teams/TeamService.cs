using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Dtos.Profile;
using JuggerHub.Dtos.Teams;
using JuggerHub.Entities;
using JuggerHub.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JuggerHub.Services.Teams;

/// <summary>
/// EF-Core-direct implementation of <see cref="ITeamService"/>. Reads use projections +
/// <c>AsNoTracking</c>; all lists paginate. Membership/role mutations run in a
/// <see cref="System.Data.IsolationLevel.Serializable"/> transaction so the last-admin invariant holds
/// under concurrency (constitution Principle I/III).
/// </summary>
public sealed class TeamService : ITeamService
{
    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;
    private readonly INotificationService _notifications;
    private readonly INotificationPreferenceService _preferences;
    private readonly Email.TeamEmailService _email;
    private readonly Recognition.IRecognitionDisplayService _recognitions;
    private readonly ILogger<TeamService> _logger;
    private readonly TeamOptions _options;

    /// <summary>Feature 019: a team's chat must be archived (snapshotted) before the team row is deleted.</summary>
    private readonly Chat.IChatConversationService _chat;

    public TeamService(
        AppDbContext db,
        TeamMembershipGuard guard,
        INotificationService notifications,
        INotificationPreferenceService preferences,
        Email.TeamEmailService email,
        Recognition.IRecognitionDisplayService recognitions,
        ILogger<TeamService> logger,
        IOptions<TeamOptions> options,
        Chat.IChatConversationService chat)
    {
        _db = db;
        _guard = guard;
        _chat = chat;
        _recognitions = recognitions;
        _notifications = notifications;
        _preferences = preferences;
        _email = email;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<SlugAvailabilityDto> CheckSlugAsync(string rawSlug, CancellationToken ct = default)
    {
        var normalized = TeamSlugPolicy.Normalize(rawSlug);
        var rejection = TeamSlugPolicy.Validate(normalized, _options.SlugMinLength, _options.SlugMaxLength);
        if (rejection != SlugRejection.None)
        {
            return new SlugAvailabilityDto(rawSlug, normalized, false,
                TeamSlugPolicy.Describe(rejection, _options.SlugMinLength, _options.SlugMaxLength));
        }

        var taken = await _db.Teams.AsNoTracking().AnyAsync(t => t.Slug == normalized, ct);
        return new SlugAvailabilityDto(rawSlug, normalized, !taken,
            taken ? "That team address isn't available." : null);
    }

    public async Task<CreateTeamResult> CreateAsync(Guid userId, CreateTeamRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length < 2 || name.Length > _options.NameMaxLength)
        {
            return CreateTeamResult.Fail(CreateTeamStatus.InvalidName, $"Use a team name of 2–{_options.NameMaxLength} characters.");
        }

        var slug = TeamSlugPolicy.Normalize(request.Slug);
        var rejection = TeamSlugPolicy.Validate(slug, _options.SlugMinLength, _options.SlugMaxLength);
        if (rejection != SlugRejection.None)
        {
            return CreateTeamResult.Fail(CreateTeamStatus.InvalidSlug,
                TeamSlugPolicy.Describe(rejection, _options.SlugMinLength, _options.SlugMaxLength) ?? "Invalid team address.");
        }

        string? city = null;
        if (request.Type == TeamType.CityTeam)
        {
            city = (request.City ?? string.Empty).Trim();
            if (city.Length == 0)
            {
                return CreateTeamResult.Fail(CreateTeamStatus.InvalidCity, "A city team needs a city.");
            }

            if (city.Length > 80)
            {
                return CreateTeamResult.Fail(CreateTeamStatus.InvalidCity, "Use a city of at most 80 characters.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.City))
        {
            return CreateTeamResult.Fail(CreateTeamStatus.InvalidCity, "A Mixteam doesn't have a city.");
        }

        if (await _db.Teams.AsNoTracking().AnyAsync(t => t.Slug == slug, ct))
        {
            return CreateTeamResult.Fail(CreateTeamStatus.SlugTaken, "That team address is already taken.");
        }

        // Explicit DbSet.Add for both rows (client-set UUIDv7 nav-insert gotcha); one
        // SaveChanges wraps them in a single transaction.
        var team = new Team { Slug = slug, Name = name, Type = request.Type, City = city };
        _db.Teams.Add(team);
        _db.TeamMemberships.Add(new TeamMembership
        {
            TeamId = team.Id,
            UserId = userId,
            Role = TeamRole.Admin,
            JoinedDate = DateTime.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost the slug race to a concurrent create.
            return CreateTeamResult.Fail(CreateTeamStatus.SlugTaken, "That team address is already taken.");
        }

        return CreateTeamResult.Ok(new TeamDetailDto(team.Slug, team.Name, team.Type, team.City, 1, TeamRole.Admin));
    }

    public async Task<TeamDetailDto?> GetDetailAsync(string slug, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return null;
        }

        var header = await _db.Teams.AsNoTracking()
            .Where(t => t.Id == a.TeamId)
            .Select(t => new { t.Slug, t.Name, t.Type, t.City, MemberCount = t.Memberships.Count, t.BeginnersWelcome })
            .FirstAsync(ct);

        return new TeamDetailDto(header.Slug, header.Name, header.Type, header.City, header.MemberCount,
            a.Role!.Value, header.BeginnersWelcome);
    }

    public async Task<TeamPublicDto?> GetPublicAsync(string slug, CancellationToken ct = default)
    {
        var normalized = TeamSlugPolicy.Normalize(slug);
        return await _db.Teams.AsNoTracking()
            .Where(t => t.Slug == normalized)
            .Select(t => new TeamPublicDto(t.Slug, t.Name, t.Type, t.City, t.Memberships.Count))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TeamPublicDetailDto?> GetPublicDetailAsync(string slug, Guid? viewerUserId, CancellationToken ct = default)
    {
        var normalized = TeamSlugPolicy.Normalize(slug);
        var now = DateTime.UtcNow;
        var cutoff = now.AddMonths(-12);

        var team = await _db.Teams.AsNoTracking()
            .Where(t => t.Slug == normalized)
            .Select(t => new
            {
                t.Id,
                t.Slug,
                t.Name,
                t.Type,
                t.City,
                t.BeginnersWelcome,
                MemberCount = t.Memberships.Count,
                // Active = created within 12 months OR a participation within the window (feature 007/008).
                IsActive = t.CreatedDate >= cutoff
                    || _db.EventParticipations.Any(ep => ep.TeamId == t.Id && ep.Event.StartsAt >= cutoff),
                ViewerRole = viewerUserId == null
                    ? (TeamRole?)null
                    : t.Memberships.Where(m => m.UserId == viewerUserId).Select(m => (TeamRole?)m.Role).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        if (team is null)
        {
            return null;
        }

        // Viewer relation is decided server-side (constitution Principle I).
        TeamViewerRelation relation;
        if (viewerUserId is null)
        {
            relation = TeamViewerRelation.Anonymous;
        }
        else if (team.ViewerRole == TeamRole.Admin)
        {
            relation = TeamViewerRelation.Admin;
        }
        else if (team.ViewerRole == TeamRole.Member)
        {
            relation = TeamViewerRelation.Member;
        }
        else
        {
            var pending = await _db.TeamJoinRequests.AsNoTracking().AnyAsync(
                r => r.TeamId == team.Id && r.UserId == viewerUserId && r.Status == JoinRequestStatus.Pending, ct);
            relation = pending ? TeamViewerRelation.Requested : TeamViewerRelation.NonMember;
        }

        // Public roster (identity + position only; NO contact details), capped.
        var roster = await _db.TeamMemberships.AsNoTracking()
            .Where(m => m.TeamId == team.Id)
            .OrderByDescending(m => m.Role).ThenBy(m => m.JoinedDate)
            .Take(48)
            .Select(m => new PublicMemberDto(
                m.User.Profile!.Handle,
                m.User.Profile!.DisplayName,
                m.Role,
                m.User.Profile!.Avatar != null,
                m.User.Profile!.Pompfen.Select(p => p.Pompfe).ToList()))
            .ToListAsync(ct);

        // Recent activity (public — distinct past events the team played), capped.
        var activityRows = await _db.EventParticipations.AsNoTracking()
            .Where(ep => ep.TeamId == team.Id)
            .Select(ep => new { ep.EventId, ep.Event.Name, ep.Event.StartsAt, ep.Event.Location, ep.TeamLabel })
            .Distinct()
            .OrderByDescending(x => x.StartsAt).ThenBy(x => x.EventId)
            .Take(6)
            .ToListAsync(ct);
        var activity = activityRows
            .Select(x => new ActivityItemDto(x.Name, DateOnly.FromDateTime(x.StartsAt), x.Location, x.TeamLabel))
            .ToList();

        var recognitions = await _recognitions.ForTeamAsync(team.Id, ct);
        return new TeamPublicDetailDto(team.Id, team.Slug, team.Name, team.Type, team.City, team.MemberCount,
            team.BeginnersWelcome, team.IsActive, relation, roster, activity,
            recognitions.Badges, recognitions.Achievements);
    }

    public async Task<PagedResult<TeamMemberDto>?> GetRosterAsync(string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return null;
        }

        var query = _db.TeamMemberships.AsNoTracking().Where(m => m.TeamId == a.TeamId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.Role) // admins first
            .ThenBy(m => m.JoinedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(m => new TeamMemberDto(
                m.UserId,
                m.User.Profile!.Handle,
                m.User.Profile!.DisplayName,
                m.Role,
                m.User.Profile!.Avatar != null,
                m.User.Profile!.Pompfen.Select(p => p.Pompfe).ToList()))
            .ToListAsync(ct);

        return new PagedResult<TeamMemberDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public Task<MemberOpResult> SetRoleAsync(string slug, Guid actorUserId, Guid targetUserId, TeamRole role, CancellationToken ct = default) =>
        MutateMembershipAsync(slug, actorUserId, targetUserId, newRole: role, remove: false, ct);

    public Task<MemberOpResult> RemoveMemberAsync(string slug, Guid actorUserId, Guid targetUserId, CancellationToken ct = default) =>
        MutateMembershipAsync(slug, actorUserId, targetUserId, newRole: null, remove: true, ct);

    public Task<MemberOpResult> StepDownAsync(string slug, Guid actorUserId, CancellationToken ct = default) =>
        MutateMembershipAsync(slug, actorUserId, actorUserId, newRole: TeamRole.Member, remove: false, ct);

    public async Task<DeleteTeamStatus> DeleteAsync(string slug, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { IsMember: true } a)
        {
            return DeleteTeamStatus.NotFoundOrNotMember;
        }

        if (!a.IsAdmin)
        {
            return DeleteTeamStatus.Forbidden;
        }

        // Archive the team's chat BEFORE the team goes (feature 019, data-model R3a). Order matters:
        // TeamMemberships cascade away below, and the chat DERIVES its membership from them, so
        // archiving afterwards would leave a conversation nobody can read. It also clears the chat's
        // Restrict FK, which would otherwise block this delete outright.
        await _chat.ArchiveForTeamAsync(a.TeamId, ct);

        // DB-level ON DELETE CASCADE removes memberships/invites/news; participations SET NULL
        // (event history preserved). ExecuteDelete is a single statement.
        await _db.Teams.Where(t => t.Id == a.TeamId).ExecuteDeleteAsync(ct);
        return DeleteTeamStatus.Deleted;
    }

    public async Task<UpdateTeamSettingsStatus> UpdateSettingsAsync(
        string slug, Guid actorUserId, UpdateTeamSettingsRequest request, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { IsMember: true } a)
        {
            return UpdateTeamSettingsStatus.NotFoundOrNotMember;
        }

        if (!a.IsAdmin)
        {
            return UpdateTeamSettingsStatus.Forbidden;
        }

        // Targeted single-column update; ExecuteUpdate bypasses the change tracker, so set
        // ModifiedDate explicitly (constitution Principle III).
        await _db.Teams
            .Where(t => t.Id == a.TeamId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.BeginnersWelcome, request.BeginnersWelcome)
                .SetProperty(t => t.ModifiedDate, DateTime.UtcNow), ct);

        return UpdateTeamSettingsStatus.Updated;
    }

    /// <summary>
    /// Apply a role change or removal, enforcing the last-admin guard. Serializes membership
    /// mutations for the team on the team row (<c>SELECT … FOR UPDATE</c>) so the admin-count
    /// check + write is atomic — the concurrent "two admins demote each other" race can never
    /// drop the team below one admin (the second waiter blocks, then sees the committed change).
    /// </summary>
    private async Task<MemberOpResult> MutateMembershipAsync(
        string slug, Guid actorUserId, Guid targetUserId, TeamRole? newRole, bool remove, CancellationToken ct)
    {
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { IsMember: true } a)
        {
            return MemberOpResult.Fail(MemberOpStatus.NotFoundOrNotMember);
        }

        var isSelf = targetUserId == actorUserId;
        // Admin-only, except a member may remove themselves (leave).
        if (!a.IsAdmin && !(remove && isSelf))
        {
            return MemberOpResult.Fail(MemberOpStatus.Forbidden, "Only admins can manage members.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Pessimistic lock on the team row serializes concurrent membership mutations for it.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Teams\" WHERE \"Id\" = {a.TeamId} FOR UPDATE", ct);

        var target = await _db.TeamMemberships
            .FirstOrDefaultAsync(m => m.TeamId == a.TeamId && m.UserId == targetUserId, ct);
        if (target is null)
        {
            return MemberOpResult.Fail(MemberOpStatus.MemberNotFound);
        }

        var removesAdmin = target.Role == TeamRole.Admin
            && (remove || (newRole is { } r && r != TeamRole.Admin));
        if (removesAdmin)
        {
            var adminCount = await _db.TeamMemberships
                .CountAsync(m => m.TeamId == a.TeamId && m.Role == TeamRole.Admin, ct);
            if (adminCount <= 1)
            {
                return MemberOpResult.Fail(MemberOpStatus.LastAdmin,
                    "Make someone else an admin before you step down or leave.");
            }
        }

        var previousRole = target.Role;
        if (remove)
        {
            _db.TeamMemberships.Remove(target);
        }
        else if (newRole is { } nr)
        {
            target.Role = nr;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (remove)
        {
            return MemberOpResult.Ok();
        }

        // Notify the member whose role actually changed — never the acting admin about themselves,
        // and only on a real change (feature 010). Best-effort: must not fail the role change.
        if (!isSelf && newRole is { } changedRole && changedRole != previousRole)
        {
            var team = await _db.Teams.AsNoTracking()
                .Where(t => t.Id == a.TeamId)
                .Select(t => new { t.Slug, t.Name })
                .FirstAsync(ct);

            // In-app notification (feature 010) — engine honors the recipient's in-app preference.
            try
            {
                await _notifications.CreateAsync(
                    recipientUserId: targetUserId,
                    type: NotificationType.TeamRoleChanged,
                    payload: new TeamRoleChangedPayload(team.Slug, team.Name, changedRole),
                    actorUserId: actorUserId,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create role-change notification for user {UserId} on team {TeamId}.",
                    targetUserId, a.TeamId);
            }

            // Email (feature 011), gated by the target's Invites & roster → Email preference. Best-effort.
            try
            {
                if (await _preferences.IsEnabledAsync(targetUserId, NotificationCategory.InvitesAndRoster, NotificationChannel.Email, ct))
                {
                    var recipientEmail = await _db.Users.AsNoTracking()
                        .Where(u => u.Id == targetUserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
                    var actorName = await _db.PlayerProfiles.AsNoTracking()
                        .Where(p => p.UserId == actorUserId).Select(p => p.DisplayName).FirstOrDefaultAsync(ct);

                    if (!string.IsNullOrEmpty(recipientEmail))
                    {
                        await _email.SendRoleChangedEmailAsync(recipientEmail, team.Name, team.Slug, actorName, changedRole, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send role-change email for user {UserId} on team {TeamId}.",
                    targetUserId, a.TeamId);
            }
        }

        var dto = await _db.TeamMemberships.AsNoTracking()
            .Where(m => m.TeamId == a.TeamId && m.UserId == targetUserId)
            .Select(m => new TeamMemberDto(
                m.UserId,
                m.User.Profile!.Handle,
                m.User.Profile!.DisplayName,
                m.Role,
                m.User.Profile!.Avatar != null,
                m.User.Profile!.Pompfen.Select(p => p.Pompfe).ToList()))
            .FirstOrDefaultAsync(ct);

        return MemberOpResult.Ok(dto);
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation });
}
