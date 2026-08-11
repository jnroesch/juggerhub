using JuggerHub.Data;
using JuggerHub.Dtos.Teams;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Teams;

/// <summary>EF-Core-direct implementation of <see cref="ITeamHappeningService"/>.</summary>
public sealed class TeamHappeningService : ITeamHappeningService
{
    /// <summary>
    /// How far back the feed looks. A catch-up card answers "what did I miss?", which is inherently
    /// recent — and the team's older history is not lost, it lives in the "Recent events" card on the
    /// same page (feature 044 decision D5).
    /// </summary>
    private const int WindowDays = 30;

    /// <summary>
    /// The hard cap on rendered entries. Applied per source query <em>and</em> to the merged list, so
    /// the in-memory set stays bounded regardless of how the happenings are distributed.
    /// </summary>
    private const int MaxEntries = 10;

    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;

    public TeamHappeningService(AppDbContext db, TeamMembershipGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    public async Task<IReadOnlyList<TeamHappeningDto>?> GetForTeamAsync(
        string slug, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            // Unknown team and non-member collapse to the same null: a non-member must not be able
            // to confirm the team exists (TeamMembershipGuard's contract).
            return null;
        }

        var cutoff = DateTime.UtcNow.AddDays(-WindowDays);

        // Each source filters on ITS OWN domain moment, not on CreatedDate. A cancellation dated by
        // the session row's CreatedDate would carry the date the series generated it — possibly years
        // ago — and brand-new information would fall outside the window (research R5).

        // Players who joined. The actor's name is sub-projected rather than navigated: PlayerProfiles
        // carries HasQueryFilter(p => p.User.Status != Banned), so `m.User.Profile!.DisplayName` does
        // not degrade predictably for a banned member. This yields null, which the client renders as
        // a *translated* stand-in — never MemberPlaceholder, which would guess the language (R2).
        var joins = await _db.TeamMemberships.AsNoTracking()
            .Where(m => m.TeamId == a.TeamId && m.JoinedDate >= cutoff)
            .OrderByDescending(m => m.JoinedDate)
            .Take(MaxEntries)
            .Select(m => new TeamHappeningDto(
                TeamHappeningKind.MemberJoined,
                new TeamHappeningParamsDto
                {
                    ActorName = _db.PlayerProfiles.Where(p => p.UserId == m.UserId).Select(p => p.DisplayName).FirstOrDefault(),
                },
                _db.PlayerProfiles.Where(p => p.UserId == m.UserId).Select(p => p.Handle).FirstOrDefault(),
                m.JoinedDate))
            .ToListAsync(ct);

        // Badges awarded to the TEAM (BadgeAward is polymorphic: PlayerProfileId XOR TeamId).
        // Status must be Active — revoked rows are retained for audit, and an unfiltered read would
        // keep announcing an award the team no longer holds (R4).
        var badges = await _db.BadgeAwards.AsNoTracking()
            .Where(b => b.TeamId == a.TeamId && b.Status == AwardStatus.Active && b.EarnedAt >= cutoff)
            .OrderByDescending(b => b.EarnedAt)
            .Take(MaxEntries)
            .Select(b => new TeamHappeningDto(
                TeamHappeningKind.RecognitionAwarded,
                new TeamHappeningParamsDto { RecognitionName = b.Definition.Name },
                null,
                b.EarnedAt))
            .ToListAsync(ct);

        var achievements = await _db.AchievementAwards.AsNoTracking()
            .Where(x => x.TeamId == a.TeamId && x.Status == AwardStatus.Active && x.EarnedAt >= cutoff)
            .OrderByDescending(x => x.EarnedAt)
            .Take(MaxEntries)
            .Select(x => new TeamHappeningDto(
                TeamHappeningKind.RecognitionAwarded,
                new TeamHappeningParamsDto { RecognitionName = x.Definition.Name },
                null,
                x.EarnedAt))
            .ToListAsync(ct);

        // Training series added. THE SERIES, NOT ITS SESSIONS: RecurrenceExpander.MaxSessions is 520
        // and TrainingSeriesService materializes the whole expansion in one save, so a per-session
        // "scheduled" entry would emit up to 520 rows sharing a single timestamp and bury every other
        // kind (feature 044 decision D3, research R3). There is no per-series route, so LinkTarget
        // stays null and the client navigates to the team's trainings tab from the slug it already has.
        var series = await _db.Trainings.AsNoTracking()
            .Where(t => t.TeamId == a.TeamId && t.CreatedDate >= cutoff)
            .OrderByDescending(t => t.CreatedDate)
            .Take(MaxEntries)
            .Select(t => new TeamHappeningDto(
                TeamHappeningKind.TrainingSeriesCreated,
                new TeamHappeningParamsDto { TrainingName = t.Name },
                null,
                t.CreatedDate))
            .ToListAsync(ct);

        // Sessions called off. TeamId is denormalized onto the session, so no join through Training is
        // needed for the filter. CancelledDate is non-null whenever Status is Cancelled
        // (TrainingSessionService.CancelAsync sets both together).
        var cancellations = await _db.TrainingSessions.AsNoTracking()
            .Where(s => s.TeamId == a.TeamId
                && s.Status == TrainingSessionStatus.Cancelled
                && s.CancelledDate != null
                && s.CancelledDate >= cutoff)
            .OrderByDescending(s => s.CancelledDate)
            .Take(MaxEntries)
            .Select(s => new TeamHappeningDto(
                TeamHappeningKind.TrainingSessionCancelled,
                new TeamHappeningParamsDto
                {
                    TrainingName = s.Training.Name,
                    SessionDate = s.SessionDate,
                },
                s.Id.ToString(),
                s.CancelledDate!.Value))
            .ToListAsync(ct);

        // Total, repeatable order (FR-015). OccurredAt alone is not enough: creating a series and
        // cancelling one of its sessions in the same batch can share a timestamp to the tick, and the
        // residual order would then be whatever the concatenation happened to produce.
        return joins
            .Concat(badges)
            .Concat(achievements)
            .Concat(series)
            .Concat(cancellations)
            .OrderByDescending(h => h.OccurredAt)
            .ThenBy(h => h.Kind)
            .ThenBy(h => h.LinkTarget ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(h => TieBreakName(h), StringComparer.Ordinal)
            .Take(MaxEntries)
            .ToList();
    }

    /// <summary>
    /// Last-resort sort key for entries a kind cannot otherwise distinguish — two recognitions
    /// granted to the same team in one batch share a timestamp, a kind, and a null link target.
    /// </summary>
    private static string TieBreakName(TeamHappeningDto h) =>
        h.Params.RecognitionName ?? h.Params.TrainingName ?? h.Params.ActorName ?? string.Empty;
}
