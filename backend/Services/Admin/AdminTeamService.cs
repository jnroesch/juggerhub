using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Admin;
using JuggerHub.Entities;
using JuggerHub.Services.Search;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Admin;

/// <summary>
/// Admin team browse for award assignment (feature 014). Reads use projections + <c>AsNoTracking</c>
/// and paginate; award counts are correlated COUNTs over active awards. Mirrors
/// <see cref="AdminUserService"/>'s search shape (name/city ILike, unaccented).
/// </summary>
public sealed class AdminTeamService : IAdminTeamService
{
    private readonly AppDbContext _db;

    public AdminTeamService(AppDbContext db) => _db = db;

    public async Task<PagedResult<AdminTeamListItemDto>> SearchAsync(
        string? q, PaginationRequest pagination, CancellationToken ct = default)
    {
        var query = _db.Teams.AsNoTracking();

        var term = SearchQuery.Normalize(q, minLength: 1);
        if (term is not null)
        {
            var pattern = SearchQuery.ContainsPattern(term);
            query = query.Where(t =>
                EF.Functions.ILike(AppDbContext.Unaccent(t.Name), AppDbContext.Unaccent(pattern))
                || (t.City != null && EF.Functions.ILike(AppDbContext.Unaccent(t.City), AppDbContext.Unaccent(pattern))));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Slug)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(t => new AdminTeamListItemDto(
                t.Slug,
                t.Name,
                t.City,
                t.Type,
                t.Memberships.Count,
                _db.BadgeAwards.Count(a => a.TeamId == t.Id && a.Status == AwardStatus.Active)
                    + _db.AchievementAwards.Count(a => a.TeamId == t.Id && a.Status == AwardStatus.Active)))
            .ToListAsync(ct);

        return new PagedResult<AdminTeamListItemDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<AdminTeamDetailDto?> GetDetailAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return await _db.Teams.AsNoTracking()
            .Where(t => t.Slug == normalized)
            .Select(t => new AdminTeamDetailDto(
                t.Id, t.Slug, t.Name, t.City, t.Type, t.Memberships.Count, t.CreatedDate))
            .FirstOrDefaultAsync(ct);
    }
}
