namespace JuggerHub.Common;

/// <summary>
/// Home dashboard tuning (feature 008): per-module caps for the composite read and the
/// news-aggregation window. Plain config with safe defaults; no secrets.
/// </summary>
public sealed class HomeOptions
{
    public const string SectionName = "Home";

    /// <summary>Max "Up next" items in the composite read.</summary>
    public int UpNextCap { get; set; } = 5;

    /// <summary>Max news items in the composite read.</summary>
    public int NewsCap { get; set; } = 5;

    /// <summary>Max team-activity items in the composite read.</summary>
    public int ActivityCap { get; set; } = 6;

    /// <summary>Max tournament cards in the composite read.</summary>
    public int TournamentCap { get; set; } = 3;

    /// <summary>Max teams surfaced (snapshots + membership aggregate).</summary>
    public int TeamsCap { get; set; } = 12;

    /// <summary>Max "Open to everyone" items for the new-player variant.</summary>
    public int OpenCap { get; set; } = 5;

    /// <summary>Newest-N read from each news source table before the in-memory merge.</summary>
    public int NewsWindow { get; set; } = 50;
}
