using System.Security.Cryptography;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Events;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using InviteState = JuggerHub.Dtos.Teams.InviteState;
using UserRelation = JuggerHub.Dtos.Teams.UserRelation;

namespace JuggerHub.Services.Events;

/// <summary>EF-Core-direct implementation of <see cref="IEventInvitationService"/>, mirroring the
/// team invitation slice. Accepting grants an <see cref="EventAdmin"/> (not a membership).</summary>
public sealed class EventInvitationService : IEventInvitationService
{
    private readonly AppDbContext _db;
    private readonly EventAdminGuard _guard;
    private readonly EventEmailService _email;
    private readonly EventOptions _eventOptions;
    private readonly EmailOptions _emailOptions;

    public EventInvitationService(
        AppDbContext db,
        EventAdminGuard guard,
        EventEmailService email,
        IOptions<EventOptions> eventOptions,
        IOptions<EmailOptions> emailOptions)
    {
        _db = db;
        _guard = guard;
        _email = email;
        _eventOptions = eventOptions.Value;
        _emailOptions = emailOptions.Value;
    }

    // --- Link -----------------------------------------------------------------

    public async Task<EventInviteLinkResult> GetActiveLinkAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(eventId, actorUserId, ct);
        if (gate != EventAdminGate.Ok)
        {
            return new EventInviteLinkResult(gate, null);
        }

        var now = DateTime.UtcNow;
        var link = await _db.EventAdminInvitations.AsNoTracking()
            .Where(i => i.EventId == eventId && i.Kind == InvitationKind.Link
                && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
            .Select(i => new { i.Token, i.ExpiresDate })
            .FirstOrDefaultAsync(ct);

        if (link is null)
        {
            return new EventInviteLinkResult(EventAdminGate.Ok, null);
        }

        var url = EventEmailService.BuildInviteLink(_emailOptions.FrontendBaseUrl, link.Token);
        return new EventInviteLinkResult(EventAdminGate.Ok, new EventInviteLinkDto(url, link.Token, link.ExpiresDate));
    }

    public async Task<EventInviteLinkResult> CreateOrRotateLinkAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(eventId, actorUserId, ct);
        if (gate != EventAdminGate.Ok)
        {
            return new EventInviteLinkResult(gate, null);
        }

        var now = DateTime.UtcNow;

        // Revoke any current active link (ExecuteUpdate bypasses the interceptor — set ModifiedDate).
        await _db.EventAdminInvitations
            .Where(i => i.EventId == eventId && i.Kind == InvitationKind.Link && i.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, InvitationStatus.Revoked)
                .SetProperty(i => i.ModifiedDate, now), ct);

        var invite = new EventAdminInvitation
        {
            EventId = eventId,
            Kind = InvitationKind.Link,
            Token = NewToken(),
            Status = InvitationStatus.Pending,
            ExpiresDate = now.AddDays(_eventOptions.InviteLinkTtlDays),
            CreatedByUserId = actorUserId,
            TargetUserId = null,
        };
        _db.EventAdminInvitations.Add(invite);
        await _db.SaveChangesAsync(ct);

        var url = EventEmailService.BuildInviteLink(_emailOptions.FrontendBaseUrl, invite.Token);
        return new EventInviteLinkResult(EventAdminGate.Ok, new EventInviteLinkDto(url, invite.Token, invite.ExpiresDate));
    }

    public async Task<RevokeOutcome> RevokeAsync(Guid eventId, Guid actorUserId, Guid invitationId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(eventId, actorUserId, ct);
        if (gate == EventAdminGate.NotFound)
        {
            return RevokeOutcome.NotFound;
        }

        if (gate == EventAdminGate.Forbidden)
        {
            return RevokeOutcome.Forbidden;
        }

        var affected = await _db.EventAdminInvitations
            .Where(i => i.Id == invitationId && i.EventId == eventId && i.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, InvitationStatus.Revoked)
                .SetProperty(i => i.ModifiedDate, DateTime.UtcNow), ct);

        return affected > 0 ? RevokeOutcome.Revoked : RevokeOutcome.InviteNotFound;
    }

    // --- Targeted -------------------------------------------------------------

    public async Task<EventTargetedInviteResult> CreateTargetedAsync(Guid eventId, Guid actorUserId, Guid targetUserId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(eventId, actorUserId, ct);
        if (gate == EventAdminGate.NotFound)
        {
            return new EventTargetedInviteResult(TargetedInviteOutcome.NotFound, null);
        }

        if (gate == EventAdminGate.Forbidden)
        {
            return new EventTargetedInviteResult(TargetedInviteOutcome.Forbidden, null);
        }

        var eventName = await _db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.Name).FirstAsync(ct);

        var target = await _db.Users.AsNoTracking()
            .Where(us => us.Id == targetUserId)
            .Select(us => new { us.Email, DisplayName = us.Profile!.DisplayName })
            .FirstOrDefaultAsync(ct);
        if (target is null || string.IsNullOrEmpty(target.Email))
        {
            return new EventTargetedInviteResult(TargetedInviteOutcome.TargetNotFound, null);
        }

        if (await _db.EventAdmins.AnyAsync(a => a.EventId == eventId && a.UserId == targetUserId, ct))
        {
            return new EventTargetedInviteResult(TargetedInviteOutcome.AlreadyAdmin, null);
        }

        var now = DateTime.UtcNow;
        var existing = await _db.EventAdminInvitations.AsNoTracking()
            .Where(i => i.EventId == eventId && i.Kind == InvitationKind.Targeted
                && i.TargetUserId == targetUserId && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
            .Select(i => new { i.Id, i.CreatedDate, i.ExpiresDate })
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return new EventTargetedInviteResult(TargetedInviteOutcome.AlreadyInvited,
                new EventInvitationDto(existing.Id, InvitationKind.Targeted, target.DisplayName, existing.CreatedDate, existing.ExpiresDate, InvitationStatus.Pending));
        }

        var inviterName = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == actorUserId)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(ct) ?? "An organiser";

        var invite = new EventAdminInvitation
        {
            EventId = eventId,
            Kind = InvitationKind.Targeted,
            Token = NewToken(),
            Status = InvitationStatus.Pending,
            ExpiresDate = now.AddDays(_eventOptions.InviteLinkTtlDays),
            CreatedByUserId = actorUserId,
            TargetUserId = targetUserId,
        };
        _db.EventAdminInvitations.Add(invite);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return new EventTargetedInviteResult(TargetedInviteOutcome.AlreadyInvited, null);
        }

        await _email.SendCoAdminInviteEmailAsync(
            target.Email, target.DisplayName, eventName, inviterName, invite.Token, invite.ExpiresDate, ct);

        return new EventTargetedInviteResult(TargetedInviteOutcome.Created,
            new EventInvitationDto(invite.Id, InvitationKind.Targeted, target.DisplayName, invite.CreatedDate, invite.ExpiresDate, InvitationStatus.Pending));
    }

    // --- Admin lists ----------------------------------------------------------

    public async Task<EventInviteListResult> ListPendingAsync(Guid eventId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(eventId, actorUserId, ct);
        if (gate != EventAdminGate.Ok)
        {
            return new EventInviteListResult(gate, null);
        }

        var now = DateTime.UtcNow;
        var query = _db.EventAdminInvitations.AsNoTracking()
            .Where(i => i.EventId == eventId && i.Status == InvitationStatus.Pending && i.ExpiresDate > now);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(i => new EventInvitationDto(
                i.Id,
                i.Kind,
                i.TargetUser != null ? i.TargetUser.Profile!.DisplayName : null,
                i.CreatedDate,
                i.ExpiresDate,
                i.Status))
            .ToListAsync(ct);

        return new EventInviteListResult(EventAdminGate.Ok,
            new PagedResult<EventInvitationDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    public async Task<EventUserSearchResult> SearchUsersAsync(Guid eventId, Guid actorUserId, string query, PaginationRequest pagination, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(eventId, actorUserId, ct);
        if (gate != EventAdminGate.Ok)
        {
            return new EventUserSearchResult(gate, null);
        }

        var term = (query ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return new EventUserSearchResult(EventAdminGate.Ok,
                new PagedResult<EventInvitableUserDto>([], 0, pagination.NormalizedSkip, pagination.NormalizedTake));
        }

        var now = DateTime.UtcNow;
        var pattern = $"%{term}%";
        var candidates = _db.PlayerProfiles.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.DisplayName, pattern) || EF.Functions.ILike(p.Handle, pattern));

        var total = await candidates.CountAsync(ct);
        var items = await candidates
            .OrderBy(p => p.DisplayName)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new EventInvitableUserDto(
                p.UserId,
                p.Handle,
                p.DisplayName,
                p.Hometown,
                _db.EventAdmins.Any(a => a.EventId == eventId && a.UserId == p.UserId)
                    ? UserRelation.Member // already an admin
                    : _db.EventAdminInvitations.Any(i => i.EventId == eventId && i.Kind == InvitationKind.Targeted
                        && i.TargetUserId == p.UserId && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
                        ? UserRelation.Invited
                        : UserRelation.Invitable))
            .ToListAsync(ct);

        return new EventUserSearchResult(EventAdminGate.Ok,
            new PagedResult<EventInvitableUserDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    // --- Invitee token flow ---------------------------------------------------

    public async Task<EventInvitePreviewDto?> GetPreviewAsync(string token, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var invite = await _db.EventAdminInvitations.AsNoTracking()
            .Where(i => i.Token == token)
            .Select(i => new
            {
                i.Status,
                i.ExpiresDate,
                EventId = i.Event.Id,
                EventName = i.Event.Name,
                i.Event.StartsAt,
                InviterName = i.CreatedBy.Profile != null ? i.CreatedBy.Profile.DisplayName : "An organiser",
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

        return new EventInvitePreviewDto(invite.EventId, invite.EventName, invite.StartsAt, invite.InviterName, state);
    }

    public async Task<EventAcceptResult> AcceptAsync(string token, Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var invite = await _db.EventAdminInvitations.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null)
        {
            return new EventAcceptResult(AcceptOutcome.NotFound, null);
        }

        var eventId = invite.EventId;

        if (!(invite.Status == InvitationStatus.Pending && invite.ExpiresDate > now))
        {
            return new EventAcceptResult(AcceptOutcome.NotUsable, eventId);
        }

        if (await _db.EventAdmins.AnyAsync(a => a.EventId == eventId && a.UserId == userId, ct))
        {
            if (invite.Kind == InvitationKind.Targeted)
            {
                invite.Status = InvitationStatus.Accepted;
                await _db.SaveChangesAsync(ct);
            }

            return new EventAcceptResult(AcceptOutcome.AlreadyAdmin, eventId);
        }

        _db.EventAdmins.Add(new EventAdmin { EventId = eventId, UserId = userId, AddedDate = now });
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
            return new EventAcceptResult(AcceptOutcome.AlreadyAdmin, eventId);
        }

        return new EventAcceptResult(AcceptOutcome.Granted, eventId);
    }

    public async Task<DeclineOutcome> DeclineAsync(string token, Guid userId, CancellationToken ct = default)
    {
        var invite = await _db.EventAdminInvitations.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null)
        {
            return DeclineOutcome.NotFound;
        }

        if (invite.Kind == InvitationKind.Targeted && invite.Status == InvitationStatus.Pending)
        {
            invite.Status = InvitationStatus.Declined;
            await _db.SaveChangesAsync(ct);
        }

        return DeclineOutcome.Declined;
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<EventAdminGate> GateAdminAsync(Guid eventId, Guid userId, CancellationToken ct)
    {
        var access = await _guard.ResolveAsync(eventId, userId, ct);
        if (access is null)
        {
            return EventAdminGate.NotFound;
        }

        return access.Value.IsAdmin ? EventAdminGate.Ok : EventAdminGate.Forbidden;
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
