using System.Linq.Expressions;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Events;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuggerHub.Services.Events;

/// <summary>
/// EF-Core-direct implementation of <see cref="IEventSignupService"/>. Sign-up routing runs inside
/// a transaction that pessimistically locks the event row (<c>SELECT … FOR UPDATE</c>) so occupied
/// spots (Joined + AwaitingApproval) can never exceed the limit under concurrency — mirroring the
/// team last-admin guard (research §4). Nothing is auto-promoted.
/// </summary>
public sealed class EventSignupService : IEventSignupService
{
    private readonly AppDbContext _db;
    private readonly EventCapacity _capacity;
    private readonly EventAdminGuard _guard;

    public EventSignupService(AppDbContext db, EventCapacity capacity, EventAdminGuard guard)
    {
        _db = db;
        _capacity = capacity;
        _guard = guard;
    }

    public async Task<PagedResult<SignupDto>?> ListGroupAsync(
        Guid eventId, SignupStatus group, PaginationRequest pagination, CancellationToken ct = default)
    {
        var exists = await _db.Events.AsNoTracking().AnyAsync(e => e.Id == eventId, ct);
        if (!exists)
        {
            return null;
        }

        var query = _db.EventSignups.AsNoTracking()
            .Where(s => s.EventId == eventId && s.Status == group);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.CreatedDate) // arrival order (also the suggested promotion order)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(Projection)
            .ToListAsync(ct);

        return new PagedResult<SignupDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<SignupResult> SignupAsync(Guid eventId, Guid userId, Guid? teamId, CancellationToken ct = default)
    {
        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.Status, e.ParticipantMode, e.IsPaid, e.ParticipationLimit, e.EndsAt })
            .FirstOrDefaultAsync(ct);
        if (ev is null)
        {
            return SignupResult.Fail(SignupOutcome.NotFound);
        }

        if (ev.Status == EventStatus.Cancelled || ev.EndsAt < DateTime.UtcNow)
        {
            return SignupResult.Fail(SignupOutcome.Closed, "This event isn't accepting sign-ups.");
        }

        Guid? subjectUser = null;
        Guid? subjectTeam = null;
        if (ev.ParticipantMode == ParticipantMode.Individuals)
        {
            if (teamId is not null)
            {
                return SignupResult.Fail(SignupOutcome.ModeMismatch, "This event is for individuals; join as yourself.");
            }

            subjectUser = userId;
        }
        else
        {
            // Feature 016: teams enter via a party, not a direct team sign-up. The party's
            // "apply" step creates the team's EventSignup on its own; this public path refuses.
            return SignupResult.Fail(SignupOutcome.ModeMismatch,
                "This event is entered via a party — form one from your team page.");
        }

        // Duplicate pre-check (the partial-unique indexes are the race-safe backstop below).
        var alreadyIn = subjectUser is Guid u
            ? await _db.EventSignups.AnyAsync(s => s.EventId == eventId && s.UserId == u, ct)
            : await _db.EventSignups.AnyAsync(s => s.EventId == eventId && s.TeamId == subjectTeam, ct);
        if (alreadyIn)
        {
            return SignupResult.Fail(SignupOutcome.Duplicate, "You're already taking part in this event.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Serialize concurrent sign-ups for this event on the event row.
        await _capacity.LockEventRowAsync(eventId, ct);
        var occupied = await _capacity.OccupiedCountAsync(eventId, ct);
        var status = occupied < ev.ParticipationLimit
            ? (ev.IsPaid ? SignupStatus.AwaitingApproval : SignupStatus.Joined)
            : SignupStatus.Waitlisted;

        var signup = new EventSignup
        {
            EventId = eventId,
            UserId = subjectUser,
            TeamId = subjectTeam,
            Status = status,
        };
        _db.EventSignups.Add(signup);

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost the partial-unique race to a concurrent sign-up by the same subject.
            return SignupResult.Fail(SignupOutcome.Duplicate, "You're already taking part in this event.");
        }

        var dto = await _db.EventSignups.AsNoTracking()
            .Where(s => s.Id == signup.Id)
            .Select(Projection)
            .FirstAsync(ct);
        return SignupResult.Ok(dto);
    }

    public async Task<WithdrawStatus> WithdrawAsync(Guid eventId, Guid signupId, Guid actorUserId, CancellationToken ct = default)
    {
        var signup = await _db.EventSignups.FirstOrDefaultAsync(s => s.Id == signupId && s.EventId == eventId, ct);
        if (signup is null)
        {
            return WithdrawStatus.NotFound;
        }

        var allowed = signup.UserId == actorUserId;
        if (!allowed && signup.TeamId is Guid tid)
        {
            allowed = await _db.TeamMemberships
                .AnyAsync(m => m.TeamId == tid && m.UserId == actorUserId && m.Role == TeamRole.Admin, ct);
        }

        if (!allowed)
        {
            var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
            allowed = access is { IsAdmin: true };
        }

        if (!allowed)
        {
            return WithdrawStatus.Forbidden;
        }

        // Removal releases the spot; nothing is auto-promoted (an admin promotes manually).
        _db.EventSignups.Remove(signup);
        await _db.SaveChangesAsync(ct);
        return WithdrawStatus.Removed;
    }

    public async Task<AdmitResult> ApproveAsync(Guid eventId, Guid signupId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return AdmitResult.Fail(AdmitOutcome.NotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return AdmitResult.Fail(AdmitOutcome.Forbidden, "Only an event admin can approve sign-ups.");
        }

        if (access.Value.IsCancelled)
        {
            return AdmitResult.Fail(AdmitOutcome.EventClosed, "This event has been cancelled.");
        }

        var signup = await _db.EventSignups.FirstOrDefaultAsync(s => s.Id == signupId && s.EventId == eventId, ct);
        if (signup is null)
        {
            return AdmitResult.Fail(AdmitOutcome.NotFound);
        }

        if (signup.Status != SignupStatus.AwaitingApproval)
        {
            return AdmitResult.Fail(AdmitOutcome.NotApplicable, "Only an awaiting-approval sign-up can be approved.");
        }

        // Approving keeps the already-held spot; just confirm payment and admit.
        signup.Status = SignupStatus.Joined;
        signup.PaymentConfirmedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return AdmitResult.Ok(await LoadDtoAsync(signup.Id, ct));
    }

    public async Task<AdmitResult> PromoteAsync(Guid eventId, Guid signupId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return AdmitResult.Fail(AdmitOutcome.NotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return AdmitResult.Fail(AdmitOutcome.Forbidden, "Only an event admin can promote from the waiting list.");
        }

        if (access.Value.IsCancelled)
        {
            return AdmitResult.Fail(AdmitOutcome.EventClosed, "This event has been cancelled.");
        }

        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new { e.IsPaid, e.ParticipationLimit })
            .FirstAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _capacity.LockEventRowAsync(eventId, ct);

        var signup = await _db.EventSignups.FirstOrDefaultAsync(s => s.Id == signupId && s.EventId == eventId, ct);
        if (signup is null)
        {
            return AdmitResult.Fail(AdmitOutcome.NotFound);
        }

        if (signup.Status != SignupStatus.Waitlisted)
        {
            return AdmitResult.Fail(AdmitOutcome.NotApplicable, "Only a waiting-list entry can be promoted.");
        }

        var occupied = await _capacity.OccupiedCountAsync(eventId, ct);
        if (occupied >= ev.ParticipationLimit)
        {
            return AdmitResult.Fail(AdmitOutcome.CapacityExceeded, "There's no open spot to promote into.");
        }

        signup.Status = ev.IsPaid ? SignupStatus.AwaitingApproval : SignupStatus.Joined;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return AdmitResult.Ok(await LoadDtoAsync(signup.Id, ct));
    }

    // --- Helpers --------------------------------------------------------------

    private Task<SignupDto> LoadDtoAsync(Guid signupId, CancellationToken ct) =>
        _db.EventSignups.AsNoTracking().Where(s => s.Id == signupId).Select(Projection).FirstAsync(ct);

    private static readonly Expression<Func<EventSignup, SignupDto>> Projection = s => new SignupDto(
        s.Id,
        s.Status,
        s.CreatedDate,
        s.User != null ? s.User.Profile!.Handle : null,
        s.User != null ? s.User.Profile!.DisplayName : null,
        s.Team != null ? s.Team.Slug : null,
        s.Team != null ? s.Team.Name : null);

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
