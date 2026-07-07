using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Search;

/// <summary>Anonymous team browse/search (feature 007). Public card fields only.</summary>
public interface ITeamSearchService
{
    Task<PagedResult<TeamCardDto>> BrowseAsync(
        TeamBrowseQuery query, PaginationRequest pagination, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class TeamSearchService : ITeamSearchService
{
    private readonly AppDbContext _db;
    private readonly SearchOptions _options;

    public TeamSearchService(AppDbContext db, IOptions<SearchOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PagedResult<TeamCardDto>> BrowseAsync(
        TeamBrowseQuery query, PaginationRequest pagination, CancellationToken ct = default)
    {
        var q = _db.Teams.AsNoTracking();

        if (query.ActiveOnly)
        {
            // Active = participated in an event whose start is within the window (spec FR-022).
            var cutoff = DateTime.UtcNow.AddMonths(-_options.ActiveTeamWindowMonths);
            q = q.Where(t => _db.EventParticipations
                .Any(ep => ep.TeamId == t.Id && ep.Event.StartsAt >= cutoff));
        }

        if (query.BeginnersWelcome)
        {
            q = q.Where(t => t.BeginnersWelcome);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim();
            q = q.Where(t => t.City != null
                && EF.Functions.ILike(AppDbContext.Unaccent(t.City), AppDbContext.Unaccent(city)));
        }

        var term = SearchQuery.Normalize(query.Q, _options.MinQueryLength);
        if (term is not null)
        {
            var pattern = SearchQuery.ContainsPattern(term);
            q = q.Where(t =>
                EF.Functions.ILike(AppDbContext.Unaccent(t.Name), AppDbContext.Unaccent(pattern))
                || (t.City != null
                    && EF.Functions.ILike(AppDbContext.Unaccent(t.City), AppDbContext.Unaccent(pattern))));
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id) // stable tiebreaker (research §5)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(t => new
            {
                t.Slug,
                t.Name,
                t.City,
                PlayerCount = t.Memberships.Count,
                t.BeginnersWelcome,
            })
            .ToListAsync(ct);

        // Derive the logo initial in memory (avoids relying on Substring SQL translation).
        var items = rows
            .Select(r => new TeamCardDto(
                r.Slug, r.Name, r.City, r.PlayerCount, r.BeginnersWelcome, LogoInitial(r.Name)))
            .ToList();

        return new PagedResult<TeamCardDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    private static string LogoInitial(string name)
    {
        var trimmed = name.TrimStart();
        return trimmed.Length == 0 ? "?" : trimmed[..1].ToUpperInvariant();
    }
}

