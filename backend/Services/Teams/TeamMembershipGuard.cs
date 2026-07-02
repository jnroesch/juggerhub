using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Teams;

/// <summary>A caller's resolved access to a team.</summary>
public readonly record struct TeamAccess(Guid TeamId, TeamRole? Role)
{
    public bool IsMember => Role is not null;

    public bool IsAdmin => Role == TeamRole.Admin;
}

/// <summary>
/// Resolves a caller's membership/role for a team slug in a single query. Every team
/// service uses it so authorization is uniform and enforced server-side (constitution
/// Principle I). Non-members are indistinguishable from unknown teams to callers — a
/// non-member yields a resolved team with a null role, which member-gated reads map to 404.
/// </summary>
public sealed class TeamMembershipGuard
{
    private readonly AppDbContext _db;

    public TeamMembershipGuard(AppDbContext db) => _db = db;

    /// <summary>
    /// Resolve the team id + the caller's role for a slug. Returns null when no team has that
    /// slug; a non-member yields a non-null result whose <see cref="TeamAccess.Role"/> is null.
    /// </summary>
    public async Task<TeamAccess?> ResolveAsync(string slug, Guid userId, CancellationToken ct = default)
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

        return match is null ? null : new TeamAccess(match.Id, match.Role);
    }
}
