using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Dtos.Teams;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuggerHub.Services.Teams;

/// <summary>
/// EF-Core-direct implementation of <see cref="ITeamJoinRequestService"/> (feature 009). Feature 058
/// added who is told: every current admin when a request arrives, and the player when it is
/// answered.
/// </summary>
public sealed class TeamJoinRequestService : ITeamJoinRequestService
{
    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;
    private readonly INotificationService _notifications;
    private readonly INotificationPreferenceService _preferences;
    private readonly TeamEmailService _email;
    private readonly ILogger<TeamJoinRequestService> _logger;

    public TeamJoinRequestService(
        AppDbContext db,
        TeamMembershipGuard guard,
        INotificationService notifications,
        INotificationPreferenceService preferences,
        TeamEmailService email,
        ILogger<TeamJoinRequestService> logger)
    {
        _db = db;
        _guard = guard;
        _notifications = notifications;
        _preferences = preferences;
        _email = email;
        _logger = logger;
    }

    public async Task<JoinRequestOutcome> RequestAsync(string slug, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { } a)
        {
            return JoinRequestOutcome.TeamNotFound;
        }

        if (a.IsMember)
        {
            return JoinRequestOutcome.AlreadyMember;
        }

        var pending = await _db.TeamJoinRequests
            .AnyAsync(r => r.TeamId == a.TeamId && r.UserId == userId && r.Status == JoinRequestStatus.Pending, ct);
        if (pending)
        {
            return JoinRequestOutcome.AlreadyPending;
        }

        var request = new TeamJoinRequest
        {
            TeamId = a.TeamId,
            UserId = userId,
            Status = JoinRequestStatus.Pending,
        };
        _db.TeamJoinRequests.Add(request);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost the partial-unique race to a concurrent request by the same player. The winner
            // announces it; announcing here too would tell every admin twice (FR-003).
            return JoinRequestOutcome.AlreadyPending;
        }

        await AnnounceAsync(request.Id, a.TeamId, userId, ct);
        return JoinRequestOutcome.Created;
    }

    /// <summary>
    /// Tell every current admin that a player is waiting (feature 058, FR-001–FR-006): the Alerts
    /// row and the device notification through the engine, the email from here. Best-effort — the
    /// request is stored, and no delivery problem may fail it or undo it.
    /// </summary>
    /// <remarks>
    /// The player is the notification's <b>actor</b> and is not in the payload: an admin's row
    /// outlives the player's account, and 037 FR-023 forbids a surviving record that identifies an
    /// erased member. The email does carry the name — it is composed now and leaves our hands.
    /// </remarks>
    private async Task AnnounceAsync(Guid requestId, Guid teamId, Guid playerId, CancellationToken ct)
    {
        var team = await _db.Teams.AsNoTracking()
            .Where(t => t.Id == teamId)
            .Select(t => new { t.Slug, t.Name })
            .FirstAsync(ct);

        // The admins at this moment, with everything the email needs, in one projection — no
        // per-recipient lookup inside the send loop (039). A banned admin cannot act on anything,
        // so is not told. Names come through the profile set, never User.Profile: the ban filter
        // makes that navigation misbehave (044).
        var admins = await _db.TeamMemberships.AsNoTracking()
            .Where(m => m.TeamId == teamId && m.Role == TeamRole.Admin && m.User.Status != AccountStatus.Banned)
            .Select(m => new
            {
                m.UserId,
                m.User.Email,
                m.User.PreferredLanguage,
                Name = _db.PlayerProfiles.Where(p => p.UserId == m.UserId).Select(p => p.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(ct);
        if (admins.Count == 0)
        {
            return;
        }

        var adminIds = admins.Select(x => x.UserId).ToList();

        try
        {
            await _notifications.CreateManyAsync(
                adminIds,
                NotificationType.TeamJoinRequest,
                new TeamJoinRequestPayload(requestId, team.Slug, team.Name),
                actorUserId: playerId,
                dedupeKeyPrefix: AlertPrefix(requestId),
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to alert the admins of team {TeamId} about join request {RequestId}.", teamId, requestId);
        }

        try
        {
            var emailRecipients = await _preferences.GetEnabledRecipientsAsync(
                adminIds, NotificationCategory.InvitesAndRoster, NotificationChannel.Email, ct);
            if (emailRecipients.Count == 0)
            {
                return;
            }

            var playerName = await _db.PlayerProfiles.AsNoTracking()
                .Where(p => p.UserId == playerId)
                .Select(p => p.DisplayName)
                .FirstOrDefaultAsync(ct);

            foreach (var admin in admins.Where(x => !string.IsNullOrEmpty(x.Email) && emailRecipients.Contains(x.UserId)))
            {
                var culture = SupportedLanguages.ResolveOrDefault(admin.PreferredLanguage);
                await _email.SendJoinRequestEmailAsync(
                    admin.Email!,
                    admin.Name ?? MemberPlaceholder.For(culture),
                    playerName ?? MemberPlaceholder.For(culture),
                    team.Name,
                    team.Slug,
                    culture,
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to email the admins of team {TeamId} about join request {RequestId}.", teamId, requestId);
        }
    }

    public async Task<JoinCancelOutcome> CancelAsync(string slug, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { } a)
        {
            return JoinCancelOutcome.TeamNotFound;
        }

        // Withdraw the caller's own pending request. A cancelled self-request keeps no audit trail
        // (unlike an admin decline), so the row is deleted — which also frees the partial-unique
        // slot so the player can cleanly request again later.
        var request = await _db.TeamJoinRequests
            .FirstOrDefaultAsync(r => r.TeamId == a.TeamId && r.UserId == userId && r.Status == JoinRequestStatus.Pending, ct);
        if (request is null)
        {
            return JoinCancelOutcome.NothingToCancel;
        }

        _db.TeamJoinRequests.Remove(request);
        await _db.SaveChangesAsync(ct);
        return JoinCancelOutcome.Cancelled;
    }

    public async Task<JoinQueueResult> ListPendingAsync(
        string slug, Guid adminUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, adminUserId, ct);
        if (access is not { } a)
        {
            return new JoinQueueResult(JoinQueueGate.NotFound, null);
        }

        if (!a.IsAdmin)
        {
            return new JoinQueueResult(JoinQueueGate.Forbidden, null);
        }

        // Only requests that still wait (feature 058): a banned player's request and one from
        // somebody who has joined another way are not the admins' to decide, and the queue used to
        // show the first as a nameless row.
        var query = _db.TeamJoinRequests.AsNoTracking()
            .Where(r => r.TeamId == a.TeamId)
            .Where(JoinRequestWaiting.Predicate(_db));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.CreatedDate).ThenBy(r => r.Id) // arrival order, stable
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(r => new JoinRequestDto(
                r.Id,
                r.User.Profile!.Handle,
                r.User.Profile!.DisplayName,
                r.User.Profile!.Avatar != null,
                r.CreatedDate))
            .ToListAsync(ct);

        return new JoinQueueResult(JoinQueueGate.Ok,
            new PagedResult<JoinRequestDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    public async Task<JoinDecisionOutcome> ApproveAsync(string slug, Guid requestId, Guid adminUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, adminUserId, ct);
        if (access is not { } a)
        {
            return JoinDecisionOutcome.TeamNotFound;
        }

        if (!a.IsAdmin)
        {
            return JoinDecisionOutcome.Forbidden;
        }

        var request = await _db.TeamJoinRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TeamId == a.TeamId && r.Status == JoinRequestStatus.Pending, ct);
        if (request is null)
        {
            return JoinDecisionOutcome.RequestNotFound;
        }

        // Create the membership unless the player already joined (idempotent), then resolve the request.
        var alreadyMember = await _db.TeamMemberships
            .AnyAsync(m => m.TeamId == a.TeamId && m.UserId == request.UserId, ct);
        if (!alreadyMember)
        {
            _db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = a.TeamId,
                UserId = request.UserId,
                Role = TeamRole.Member,
                JoinedDate = DateTime.UtcNow,
            });
        }

        request.Status = JoinRequestStatus.Approved;
        request.DecidedByUserId = adminUserId;
        request.DecidedDate = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent join created the membership first — the request is still approved.
            request.Status = JoinRequestStatus.Approved;
            await _db.SaveChangesAsync(ct);
        }

        return JoinDecisionOutcome.Done;
    }

    public async Task<JoinDecisionOutcome> DeclineAsync(string slug, Guid requestId, Guid adminUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, adminUserId, ct);
        if (access is not { } a)
        {
            return JoinDecisionOutcome.TeamNotFound;
        }

        if (!a.IsAdmin)
        {
            return JoinDecisionOutcome.Forbidden;
        }

        var request = await _db.TeamJoinRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TeamId == a.TeamId && r.Status == JoinRequestStatus.Pending, ct);
        if (request is null)
        {
            return JoinDecisionOutcome.RequestNotFound;
        }

        request.Status = JoinRequestStatus.Declined;
        request.DecidedByUserId = adminUserId;
        request.DecidedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return JoinDecisionOutcome.Done;
    }

    /// <summary>
    /// The dedupe-key prefix a request's admin alerts are written under. The announcement writes
    /// them with it, and a withdrawal finds them by it — never by the roster, which would miss an
    /// admin demoted since (feature 057's lesson) — so it is spelled once.
    /// </summary>
    private static string AlertPrefix(Guid requestId) => $"join-request:{requestId}";

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
