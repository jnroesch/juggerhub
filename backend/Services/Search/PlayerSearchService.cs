using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Search;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Search;

/// <summary>
/// Anonymous player browse/search (feature 007). Returns every non-banned player matching the
/// query. Banned accounts are excluded globally by the <see cref="Entities.PlayerProfile"/> query
/// filter (feature 013); suspended accounts stay visible. The former per-player opt-in gate
/// (AppearInSearch) was removed in feature 020 — see specs/020-remove-search-optout.
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
        // All non-banned players are browseable (the AppearInSearch opt-in was removed in feature
        // 020). Banned accounts are still excluded by the global PlayerProfile query filter.
        var q = _db.PlayerProfiles.AsNoTracking();

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
