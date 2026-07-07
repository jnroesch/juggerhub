namespace JuggerHub.Common;

/// <summary>
/// Configuration for the discovery/browse feature (007). Bound from the <c>Search</c>
/// config section with safe defaults so browse works with zero configuration. No secrets.
/// </summary>
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>
    /// A team counts as "active" if it has participated in an event whose start is within
    /// this many months of now. Deliberately a single simple rule for v1 (spec clarification).
    /// </summary>
    public int ActiveTeamWindowMonths { get; set; } = 12;

    /// <summary>
    /// Minimum trimmed free-text query length. Shorter queries are ignored (treated as
    /// "browse all") rather than rejected, so typing never errors.
    /// </summary>
    public int MinQueryLength { get; set; } = 1;
}
