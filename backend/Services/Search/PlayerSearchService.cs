using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Search;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Search;

/// <summary>
/// Anonymous player browse/search (feature 007). PRIVACY INVARIANT — only players with
/// <see cref="Entities.PlayerProfile.AppearInSearch"/> = true are ever returned, for every
/// query/filter/sort and regardless of the caller's auth. The gate is applied first and
/// cannot be bypassed (spec FR-042 / SC-003).
/// </summary>
public interface IPlayerSearchService
{
    Task<PagedResult<PlayerCardDto>> BrowseAsync(
        PlayerBrowseQuery query, PaginationRequest pagination, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class PlayerSearchService : IPlayerSearchService
{
    private readonly AppDbContext _db;
    private readonly SearchOptions _options;

    public PlayerSearchService(AppDbContext db, IOptions<SearchOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PagedResult<PlayerCardDto>> BrowseAsync(
        PlayerBrowseQuery query, PaginationRequest pagination, CancellationToken ct = default)
    {
        // OPT-IN GATE — applied unconditionally, before any query/filter/sort. Non-negotiable.
        var q = _db.PlayerProfiles.AsNoTracking().Where(p => p.AppearInSearch);

        if (query.Positions is { Count: > 0 } positions)
        {
            q = q.Where(p => p.Pompfen.Any(pp => positions.Contains(pp.Pompfe)));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            q = q.Where(p => p.Hometown != null
                && EF.Functions.ILike(AppDbContext.Unaccent(p.Hometown), AppDbContext.Unaccent(city)));
        }

        var term = SearchQuery.Normalize(query.Q, _options.MinQueryLength);
        if (term is not null)
        {
            var pattern = SearchQuery.ContainsPattern(term);
            q = q.Where(p =>
                EF.Functions.ILike(AppDbContext.Unaccent(p.DisplayName), AppDbContext.Unaccent(pattern)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(p => p.DisplayName)
            .ThenBy(p => p.Id) // stable tiebreaker
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new PlayerCardDto(
                p.Handle,
                p.DisplayName,
                p.Hometown,
                p.Pompfen.OrderBy(pp => pp.Pompfe).Select(pp => pp.Pompfe).ToList(),
                p.Avatar != null))
            .ToListAsync(ct);

        return new PagedResult<PlayerCardDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }
}
