using JuggerHub.Data;
using JuggerHub.Dtos.Results;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Results;

/// <summary>What the result services may do, and why not (feature 050).</summary>
public enum ResultStatus
{
    Ok,
    NotFound,
    Forbidden,
    NotATournament,
    Cancelled,
    NotStarted,
    Invalid,
    TeamNotAllowed,
    NotLinked,
    TugenyNotFound,
    TugenyNotFinalized,
    TugenyUnavailable,
    AlreadyPlaced,
}

/// <summary>A result-service outcome: a status, an optional value, and a human detail.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Value">The result, when <paramref name="Status"/> is <see cref="ResultStatus.Ok"/>.</param>
/// <param name="Detail">A plain-words explanation for a failure; never an internal message.</param>
/// <param name="Row">The zero-based request row the problem is about, when there is one.</param>
public sealed record ResultOutcome<T>(ResultStatus Status, T? Value = default, string? Detail = null, int? Row = null)
{
    public static ResultOutcome<T> Ok(T value) => new(ResultStatus.Ok, value);

    public static ResultOutcome<T> Fail(ResultStatus status, string? detail = null, int? row = null) =>
        new(status, default, detail, row);
}

/// <summary>The facts about an event that every result action is gated on.</summary>
internal sealed record ResultEvent(
    Guid Id,
    string Name,
    EventType Type,
    EventStatus Status,
    ParticipantMode Mode,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsAdmin)
{
    public bool IsTournament => Type == EventType.Tournament;

    public bool IsCancelled => Status == EventStatus.Cancelled;

    public bool HasStarted(DateTime now) => StartsAt <= now;

    public bool HasEnded(DateTime now) => EndsAt < now;

    /// <summary>FR-001: a ranking may be recorded by an admin of a started, uncancelled tournament.</summary>
    public bool CanRecord(DateTime now) => IsAdmin && IsTournament && !IsCancelled && HasStarted(now);
}

/// <summary>
/// Shared reads behind the result services: the event facts they gate on, and the event's
/// signed-up teams — the only teams an event admin may connect a placement to (research R4).
/// </summary>
internal static class ResultEvents
{
    /// <summary>
    /// The event and whether <paramref name="userId"/> administers it, in one query. The admin test
    /// is <see cref="Events.EventAdminGuard"/>'s, inlined so the gate facts arrive together.
    /// </summary>
    public static Task<ResultEvent?> LoadAsync(AppDbContext db, Guid eventId, Guid userId, CancellationToken ct) =>
        db.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new ResultEvent(
                e.Id,
                e.Name,
                e.Type,
                e.Status,
                e.ParticipantMode,
                e.StartsAt,
                e.EndsAt,
                e.Admins.Any(a => a.UserId == userId)))
            .FirstOrDefaultAsync(ct);

    /// <summary>The status that stops a write, or null when the caller may record results.</summary>
    public static ResultStatus? RecordGate(ResultEvent ev, DateTime now)
    {
        if (!ev.IsAdmin)
        {
            return ResultStatus.Forbidden;
        }

        if (!ev.IsTournament)
        {
            return ResultStatus.NotATournament;
        }

        if (ev.IsCancelled)
        {
            return ResultStatus.Cancelled;
        }

        return ev.HasStarted(now) ? null : ResultStatus.NotStarted;
    }

    /// <summary>
    /// Teams with a confirmed JuggerHub sign-up for the event: an <see cref="EventSignup"/> with
    /// <see cref="SignupStatus.Joined"/>. Pending and waitlisted entries never count — their place
    /// was never confirmed. Sign-up order.
    /// </summary>
    public static Task<List<SignedUpTeamDto>> SignedUpTeamsAsync(AppDbContext db, Guid eventId, CancellationToken ct) =>
        db.EventSignups.AsNoTracking()
            .Where(s => s.EventId == eventId && s.TeamId != null && s.Status == SignupStatus.Joined)
            .OrderBy(s => s.CreatedDate)
            .Select(s => new SignedUpTeamDto(s.TeamId!.Value, s.Team!.Slug, s.Team.Name))
            .ToListAsync(ct);
}
