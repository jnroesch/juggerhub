using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Teams;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Trainings;

/// <summary>A caller's resolved access to a team's trainings, by slug (feature 018).</summary>
public readonly record struct TrainingTeamAccess(Guid TeamId, TeamRole? Role)
{
    public bool IsMember => Role is not null;

    public bool IsAdmin => Role == TeamRole.Admin;
}

/// <summary>A caller's resolved access to a single training session (feature 018).</summary>
public readonly record struct TrainingSessionAccess(
    Guid SessionId,
    Guid TrainingId,
    Guid TeamId,
    string TeamSlug,
    TrainingVisibility EffectiveVisibility,
    TrainingSessionStatus Status,
    DateTime StartsAtUtc,
    TeamRole? Role,
    bool IsTeamMember)
{
    public bool IsAdmin => Role == TeamRole.Admin;

    public bool IsPublic => EffectiveVisibility == TrainingVisibility.Public;

    public bool IsPast => StartsAtUtc < DateTime.UtcNow;

    /// <summary>Can the caller view/RSVP this session — a team member, or anyone on a public session.</summary>
    public bool CanView => IsTeamMember || IsPublic;

    /// <summary>The caller is an accessing outsider (would be recorded as a guest on RSVP).</summary>
    public bool IsGuest => !IsTeamMember && IsPublic;
}

/// <summary>
/// Resolves a caller's role/state for a team slug or a session id in a single query so every training
/// service authorizes uniformly and server-side (constitution Principle I). Mirrors
/// <see cref="TeamMembershipGuard"/>/<c>PartyGuard</c>: a non-member is indistinguishable from an unknown
/// team on team-only surfaces (services map that to 404). For a session, the guard computes the effective
/// visibility (per-session override falling back to the series) and the UTC start instant so the service
/// can decide member-vs-outsider access and past-read-only without a second round trip.
/// </summary>
public sealed class TrainingGuard
{
    private readonly AppDbContext _db;

    public TrainingGuard(AppDbContext db) => _db = db;

    /// <summary>Resolve the team id + caller role for a slug. Null when no team has that slug.</summary>
    public async Task<TrainingTeamAccess?> ResolveTeamAsync(string slug, Guid userId, CancellationToken ct = default)
    {
        var normalized = TeamSlugPolicy.Normalize(slug);
        var match = await _db.Teams.AsNoTracking()
            .Where(t => t.Slug == normalized)
            .Select(t => new
            {
                t.Id,
                Role = t.Memberships
                    .Where(m => m.UserId == userId)
                    .Select(m => (TeamRole?)m.Role)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        return match is null ? null : new TrainingTeamAccess(match.Id, match.Role);
    }

    /// <summary>Resolve a session's team/training/effective-visibility/status/start plus the caller's role. Null when no session has that id.</summary>
    public async Task<TrainingSessionAccess?> ResolveSessionAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var match = await _db.TrainingSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new
            {
                s.TrainingId,
                s.TeamId,
                TeamSlug = s.Training.Team.Slug,
                EffectiveVisibility = s.VisibilityOverride ?? s.Training.Visibility,
                s.Status,
                s.SessionDate,
                StartTime = s.StartTimeOverride ?? s.Training.StartTime,
                Role = s.Training.Team.Memberships
                    .Where(m => m.UserId == userId)
                    .Select(m => (TeamRole?)m.Role)
                    .FirstOrDefault(),
                IsTeamMember = s.Training.Team.Memberships.Any(m => m.UserId == userId),
            })
            .FirstOrDefaultAsync(ct);

        if (match is null)
        {
            return null;
        }

        var startsAtUtc = DateTime.SpecifyKind(match.SessionDate.ToDateTime(match.StartTime), DateTimeKind.Utc);
        return new TrainingSessionAccess(
            sessionId, match.TrainingId, match.TeamId, match.TeamSlug, match.EffectiveVisibility,
            match.Status, startsAtUtc, match.Role, match.IsTeamMember);
    }
}
