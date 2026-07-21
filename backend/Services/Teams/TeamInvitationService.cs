using System.Security.Cryptography;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Dtos.Teams;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JuggerHub.Services.Teams;

/// <summary>EF-Core-direct implementation of <see cref="ITeamInvitationService"/>.</summary>
public sealed class TeamInvitationService : ITeamInvitationService
{
    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;
    private readonly TeamEmailService _email;
    private readonly INotificationService _notifications;
    private readonly INotificationPreferenceService _preferences;
    private readonly ILogger<TeamInvitationService> _logger;
    private readonly TeamOptions _teamOptions;
    private readonly EmailOptions _emailOptions;

    public TeamInvitationService(
        AppDbContext db,
        TeamMembershipGuard guard,
        TeamEmailService email,
        INotificationService notifications,
        INotificationPreferenceService preferences,
        ILogger<TeamInvitationService> logger,
        IOptions<TeamOptions> teamOptions,
        IOptions<EmailOptions> emailOptions)
    {
        _db = db;
        _guard = guard;
        _email = email;
        _notifications = notifications;
        _preferences = preferences;
        _logger = logger;
        _teamOptions = teamOptions.Value;
        _emailOptions = emailOptions.Value;
    }

    // --- Link -----------------------------------------------------------------

    public async Task<InviteLinkResult> GetActiveLinkAsync(string slug, Guid actorUserId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(slug, actorUserId, ct);
        if (gate.Status != InviteAdminStatus.Ok)
        {
            return new InviteLinkResult(gate.Status, null);
        }

        var now = DateTime.UtcNow;
        var link = await _db.TeamInvitations.AsNoTracking()
            .Where(i => i.TeamId == gate.TeamId && i.Kind == InvitationKind.Link
                && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
            .Select(i => new { i.Token, i.ExpiresDate })
            .FirstOrDefaultAsync(ct);

        if (link is null)
        {
            return new InviteLinkResult(InviteAdminStatus.Ok, null);
        }

        var url = TeamEmailService.BuildJoinLink(_emailOptions.FrontendBaseUrl, TeamSlugPolicy.Normalize(slug), link.Token);
        return new InviteLinkResult(InviteAdminStatus.Ok, new InviteLinkDto(url, link.Token, link.ExpiresDate));
    }

    public async Task<InviteLinkResult> CreateOrRotateLinkAsync(string slug, Guid actorUserId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(slug, actorUserId, ct);
        if (gate.Status != InviteAdminStatus.Ok)
        {
            return new InviteLinkResult(gate.Status, null);
        }

        var now = DateTime.UtcNow;

        // Revoke any current active link (ExecuteUpdate bypasses the interceptor — set ModifiedDate).
        await _db.TeamInvitations
            .Where(i => i.TeamId == gate.TeamId && i.Kind == InvitationKind.Link && i.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, InvitationStatus.Revoked)
                .SetProperty(i => i.ModifiedDate, now), ct);

        var invite = new TeamInvitation
        {
            TeamId = gate.TeamId,
            Kind = InvitationKind.Link,
            Token = NewToken(),
            Status = InvitationStatus.Pending,
            ExpiresDate = now.AddDays(_teamOptions.InviteLinkTtlDays),
            CreatedByUserId = actorUserId,
            TargetUserId = null,
        };
        _db.TeamInvitations.Add(invite);
        await _db.SaveChangesAsync(ct);

        var url = TeamEmailService.BuildJoinLink(_emailOptions.FrontendBaseUrl, TeamSlugPolicy.Normalize(slug), invite.Token);
        return new InviteLinkResult(InviteAdminStatus.Ok, new InviteLinkDto(url, invite.Token, invite.ExpiresDate));
    }

    public async Task<RevokeStatus> RevokeAsync(string slug, Guid actorUserId, Guid invitationId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(slug, actorUserId, ct);
        if (gate.Status == InviteAdminStatus.NotFoundOrNotMember)
        {
            return RevokeStatus.NotFoundOrNotMember;
        }

        if (gate.Status == InviteAdminStatus.Forbidden)
        {
            return RevokeStatus.Forbidden;
        }

        var affected = await _db.TeamInvitations
            .Where(i => i.Id == invitationId && i.TeamId == gate.TeamId && i.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, InvitationStatus.Revoked)
                .SetProperty(i => i.ModifiedDate, DateTime.UtcNow), ct);

        return affected > 0 ? RevokeStatus.Revoked : RevokeStatus.NotFound;
    }

    // --- Targeted -------------------------------------------------------------

    public async Task<TargetedInviteResult> CreateTargetedAsync(string slug, Guid actorUserId, Guid targetUserId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(slug, actorUserId, ct);
        if (gate.Status == InviteAdminStatus.NotFoundOrNotMember)
        {
            return new TargetedInviteResult(TargetedInviteStatus.NotFoundOrNotMember, null);
        }

        if (gate.Status == InviteAdminStatus.Forbidden)
        {
            return new TargetedInviteResult(TargetedInviteStatus.Forbidden, null);
        }

        var team = await _db.Teams.AsNoTracking()
            .Where(t => t.Id == gate.TeamId)
            .Select(t => new { t.Slug, t.Name })
            .FirstAsync(ct);

        var target = await _db.Users.AsNoTracking()
            .Where(u => u.Id == targetUserId)
            .Select(u => new { u.Email, DisplayName = u.Profile!.DisplayName })
            .FirstOrDefaultAsync(ct);
        if (target is null || string.IsNullOrEmpty(target.Email))
        {
            return new TargetedInviteResult(TargetedInviteStatus.TargetNotFound, null);
        }

        if (await _db.TeamMemberships.AnyAsync(m => m.TeamId == gate.TeamId && m.UserId == targetUserId, ct))
        {
            return new TargetedInviteResult(TargetedInviteStatus.AlreadyMember, null);
        }

        var now = DateTime.UtcNow;
        var existing = await _db.TeamInvitations.AsNoTracking()
            .Where(i => i.TeamId == gate.TeamId && i.Kind == InvitationKind.Targeted
                && i.TargetUserId == targetUserId && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
            .Select(i => new { i.Id, i.CreatedDate, i.ExpiresDate })
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return new TargetedInviteResult(TargetedInviteStatus.AlreadyInvited,
                new TeamInvitationDto(existing.Id, InvitationKind.Targeted, target.DisplayName, existing.CreatedDate, existing.ExpiresDate, InvitationStatus.Pending));
        }

        var inviterName = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == actorUserId)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(ct) ?? "A teammate";

        var invite = new TeamInvitation
        {
            TeamId = gate.TeamId,
            Kind = InvitationKind.Targeted,
            Token = NewToken(),
            Status = InvitationStatus.Pending,
            ExpiresDate = now.AddDays(_teamOptions.InviteLinkTtlDays),
            CreatedByUserId = actorUserId,
            TargetUserId = targetUserId,
        };
        _db.TeamInvitations.Add(invite);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent duplicate lost the partial-unique race — treat as already invited.
            return new TargetedInviteResult(TargetedInviteStatus.AlreadyInvited, null);
        }

        // Email is gated by the target's Invites & roster → Email preference (feature 011).
        if (await _preferences.IsEnabledAsync(targetUserId, NotificationCategory.InvitesAndRoster, NotificationChannel.Email, ct))
        {
            await _email.SendTeamInviteEmailAsync(
                target.Email, target.DisplayName, team.Name, inviterName, team.Slug, invite.Token, invite.ExpiresDate, ct);
        }

        // In-app notification (feature 010) — complements the email, never blocks the invite.
        try
        {
            await _notifications.CreateAsync(
                recipientUserId: targetUserId,
                type: NotificationType.TeamInvite,
                payload: new TeamInvitePayload(invite.Id, invite.Token, team.Slug, team.Name, inviterName),
                actorUserId: actorUserId,
                dedupeKey: $"invite:{invite.Id}",
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create in-app notification for team invite {InviteId}.", invite.Id);
        }

        return new TargetedInviteResult(TargetedInviteStatus.Created,
            new TeamInvitationDto(invite.Id, InvitationKind.Targeted, target.DisplayName, invite.CreatedDate, invite.ExpiresDate, InvitationStatus.Pending));
    }

    // --- Admin lists ----------------------------------------------------------

    public async Task<InviteListResult> ListPendingAsync(string slug, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(slug, actorUserId, ct);
        if (gate.Status != InviteAdminStatus.Ok)
        {
            return new InviteListResult(gate.Status, null);
        }

        var now = DateTime.UtcNow;
        var query = _db.TeamInvitations.AsNoTracking()
            .Where(i => i.TeamId == gate.TeamId && i.Status == InvitationStatus.Pending && i.ExpiresDate > now);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.Kind) // link (targeted=1 first actually) — order by created for stability
            .ThenByDescending(i => i.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(i => new TeamInvitationDto(
                i.Id,
                i.Kind,
                i.TargetUser != null ? i.TargetUser.Profile!.DisplayName : null,
                i.CreatedDate,
                i.ExpiresDate,
                i.Status))
            .ToListAsync(ct);

        return new InviteListResult(InviteAdminStatus.Ok,
            new PagedResult<TeamInvitationDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    public async Task<UserSearchResult> SearchUsersAsync(string slug, Guid actorUserId, string query, PaginationRequest pagination, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(slug, actorUserId, ct);
        if (gate.Status != InviteAdminStatus.Ok)
        {
            return new UserSearchResult(gate.Status, null);
        }

        var term = (query ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return new UserSearchResult(InviteAdminStatus.Ok,
                new PagedResult<InvitableUserDto>([], 0, pagination.NormalizedSkip, pagination.NormalizedTake));
        }

        var now = DateTime.UtcNow;
        var teamId = gate.TeamId;
        var pattern = $"%{term}%";
        var candidates = _db.PlayerProfiles.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.DisplayName, pattern) || EF.Functions.ILike(p.Handle, pattern));

        var total = await candidates.CountAsync(ct);
        var items = await candidates
            .OrderBy(p => p.DisplayName)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new InvitableUserDto(
                p.UserId,
                p.Handle,
                p.DisplayName,
                p.Hometown,
                _db.TeamMemberships.Any(m => m.TeamId == teamId && m.UserId == p.UserId)
                    ? UserRelation.Member
                    : _db.TeamInvitations.Any(i => i.TeamId == teamId && i.Kind == InvitationKind.Targeted
                        && i.TargetUserId == p.UserId && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
                        ? UserRelation.Invited
                        : UserRelation.Invitable))
            .ToListAsync(ct);

        return new UserSearchResult(InviteAdminStatus.Ok,
            new PagedResult<InvitableUserDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    // --- Invitee self-service list (feature 023) ------------------------------

    public async Task<PagedResult<MyInvitationDto>> ListMineAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Scoped to the caller: only usable (pending + unexpired) TARGETED invites addressed to them.
        // Link invites (no target) and expired/revoked/consumed invites are never returned.
        var query = _db.TeamInvitations.AsNoTracking()
            .Where(i => i.Kind == InvitationKind.Targeted
                && i.TargetUserId == userId
                && i.Status == InvitationStatus.Pending
                && i.ExpiresDate > now);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(i => new MyInvitationDto(
                i.Token,
                i.Team.Name,
                i.Team.Slug,
                i.Team.Type,
                i.Team.City,
                i.Team.Memberships.Count,
                i.CreatedBy.Profile != null ? i.CreatedBy.Profile.DisplayName : "A teammate",
                i.CreatedDate,
                i.ExpiresDate))
            .ToListAsync(ct);

        return new PagedResult<MyInvitationDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    // --- Invitee token flow (anonymous preview + authed accept/decline) -------

    public async Task<InvitePreviewDto?> GetPreviewAsync(string token, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var invite = await _db.TeamInvitations.AsNoTracking()
            .Where(i => i.Token == token)
            .Select(i => new
            {
                i.Status,
                i.ExpiresDate,
                TeamName = i.Team.Name,
                TeamSlug = i.Team.Slug,
                i.Team.Type,
                i.Team.City,
                MemberCount = i.Team.Memberships.Count,
                InviterName = i.CreatedBy.Profile != null ? i.CreatedBy.Profile.DisplayName : "A teammate",
            })
            .FirstOrDefaultAsync(ct);

        if (invite is null)
        {
            return null;
        }

        var state = invite.Status == InvitationStatus.Pending && invite.ExpiresDate > now
            ? InviteState.Usable
            : invite.Status == InvitationStatus.Pending
                ? InviteState.Expired
                : InviteState.Invalid;

        return new InvitePreviewDto(invite.TeamName, invite.TeamSlug, invite.Type, invite.City, invite.MemberCount, invite.InviterName, state);
    }

    public async Task<AcceptResult> AcceptAsync(string token, Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var invite = await _db.TeamInvitations.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null)
        {
            return new AcceptResult(AcceptStatus.NotFound, null);
        }

        var teamSlug = await _db.Teams.AsNoTracking().Where(t => t.Id == invite.TeamId).Select(t => t.Slug).FirstAsync(ct);

        if (!(invite.Status == InvitationStatus.Pending && invite.ExpiresDate > now))
        {
            return new AcceptResult(AcceptStatus.NotUsable, null);
        }

        if (await _db.TeamMemberships.AnyAsync(m => m.TeamId == invite.TeamId && m.UserId == userId, ct))
        {
            // Idempotent: consume a targeted invite, report already-member.
            if (invite.Kind == InvitationKind.Targeted)
            {
                invite.Status = InvitationStatus.Accepted;
                await _db.SaveChangesAsync(ct);
            }

            return new AcceptResult(AcceptStatus.AlreadyMember, teamSlug);
        }

        _db.TeamMemberships.Add(new TeamMembership
        {
            TeamId = invite.TeamId,
            UserId = userId,
            Role = TeamRole.Member,
            JoinedDate = now,
        });

        // A shared link stays Pending (reusable by other users); a targeted invite is consumed.
        if (invite.Kind == InvitationKind.Targeted)
        {
            invite.Status = InvitationStatus.Accepted;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent double-accept of the same link by the same user.
            return new AcceptResult(AcceptStatus.AlreadyMember, teamSlug);
        }

        return new AcceptResult(AcceptStatus.Joined, teamSlug);
    }

    public async Task<DeclineStatus> DeclineAsync(string token, Guid userId, CancellationToken ct = default)
    {
        var invite = await _db.TeamInvitations.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null)
        {
            return DeclineStatus.NotFound;
        }

        // Declining consumes a pending targeted invite; a shared link decline is a no-op.
        if (invite.Kind == InvitationKind.Targeted && invite.Status == InvitationStatus.Pending)
        {
            invite.Status = InvitationStatus.Declined;
            await _db.SaveChangesAsync(ct);
        }

        return DeclineStatus.Declined;
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<(InviteAdminStatus Status, Guid TeamId)> GateAdminAsync(string slug, Guid userId, CancellationToken ct)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return (InviteAdminStatus.NotFoundOrNotMember, Guid.Empty);
        }

        return a.IsAdmin
            ? (InviteAdminStatus.Ok, a.TeamId)
            : (InviteAdminStatus.Forbidden, a.TeamId);
    }

    private static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
