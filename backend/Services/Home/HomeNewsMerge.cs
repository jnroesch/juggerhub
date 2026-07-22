using JuggerHub.Dtos.Home;

namespace JuggerHub.Services.Home;

/// <summary>
/// Merges the authored news sources (team + event + party, feature 025) already read as bounded
/// newest-first windows into one newest-first list (feature 008 research §3). A future denormalized
/// feed / "league" source slots in here without changing the <see cref="HomeNewsDto"/> contract.
/// </summary>
internal static class HomeNewsMerge
{
    /// <summary>Interleave the per-source windows newest-first (CreatedDate desc, then Id desc for stability).</summary>
    internal static List<HomeNewsDto> Merge(params IEnumerable<HomeProjections.NewsRaw>[] sources) =>
        sources.SelectMany(s => s)
            .OrderByDescending(n => n.CreatedDate)
            .ThenByDescending(n => n.Id)
            .Select(n => new HomeNewsDto(n.Source, n.SourceName, n.SourceSlugOrId, n.Body, n.CreatedDate))
            .ToList();
}
