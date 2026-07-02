using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Teams;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Teams;

/// <summary>EF-Core-direct implementation of <see cref="ITeamNewsService"/>.</summary>
public sealed class TeamNewsService : ITeamNewsService
{
    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;

    public TeamNewsService(AppDbContext db, TeamMembershipGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    public async Task<PagedResult<TeamNewsDto>?> GetFeedAsync(
        string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return null;
        }

        var query = _db.TeamNewsPosts.AsNoTracking().Where(n => n.TeamId == a.TeamId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(n => new TeamNewsDto(
                n.Author.Profile!.DisplayName,
                n.Author.Profile!.Handle,
                // Author's current role in this team (defaults to Member if they've left).
                _db.TeamMemberships
                    .Where(m => m.TeamId == n.TeamId && m.UserId == n.AuthorUserId)
                    .Select(m => m.Role)
                    .FirstOrDefault(),
                n.CreatedDate,
                n.Body))
            .ToListAsync(ct);

        return new PagedResult<TeamNewsDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }
}
