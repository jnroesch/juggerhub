namespace JuggerHub.Services.Search;

/// <summary>Sort options per browse entity (feature 007). v1 ships each entity's default only;
/// the enum leaves room to add alternates (e.g. newest, player-count) without contract churn.
/// Serialized as its name via the global JsonStringEnumConverter.</summary>
public enum TeamSort
{
    NameAsc = 0,

    /// <summary>Feature 030 — nearest-first relative to the caller's home city (opt-in). Requires a home city.</summary>
    Proximity = 1,
}

/// <summary>Event browse sort. Default is soonest-upcoming first.</summary>
public enum EventSort
{
    StartsAtAsc = 0,

    /// <summary>Feature 030 — nearest-first (located events only; virtual excluded). Requires a home city.</summary>
    Proximity = 1,
}

/// <summary>Player browse sort. Default is A–Z by display name.</summary>
public enum PlayerSort
{
    DisplayNameAsc = 0,

    /// <summary>
    /// Feature 030 pattern — nearest-first, measured from the caller's home city to the player's
    /// home city. <b>Requires a home city</b>: the controller returns 409 when the caller has none
    /// rather than silently answering with a different ordering. Players who have set no home city
    /// are excluded from this view entirely (the distance join has no row for them).
    /// <para>
    /// City-granular, exactly like the teams and events orderings: it ranks by the distance between
    /// two <em>cities</em> that are already shown on the cards and already filterable, so it
    /// discloses nothing a viewer could not read off the list.
    /// </para>
    /// </summary>
    Proximity = 1,
}

/// <summary>
/// Public-training browse sort (feature 043). Default is soonest-upcoming session first.
/// </summary>
public enum TrainingSort
{
    SessionDateAsc = 0,

    /// <summary>
    /// Feature 030 pattern — nearest-first, measured from the caller's home city to the session's
    /// city. <b>Requires a home city</b>: the controller returns 409 when the caller has none rather
    /// than silently answering with a different ordering. Sessions with no city (every virtual
    /// training, and any training predating feature 042) are excluded from this view entirely.
    /// </summary>
    Proximity = 1,
}

/// <summary>
/// Shared helpers for the browse/search services: free-text normalization and the
/// accent-insensitive <c>ILIKE</c> pattern. All filtering happens server-side; the client
/// never receives a non-matching row (constitution Principle I).
/// </summary>
public static class SearchQuery
{
    /// <summary>
    /// Normalize a raw query: trim, and treat whitespace-only or too-short input as absent
    /// (returns null ⇒ "browse all"). Never throws — typing must not error.
    /// </summary>
    public static string? Normalize(string? raw, int minLength)
    {
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < minLength)
        {
            return null;
        }

        return trimmed;
    }

    /// <summary>
    /// Build a case-insensitive substring pattern for an <c>ILIKE</c> match. Escapes the
    /// LIKE metacharacters (<c>% _ \</c>) in the user term so they are matched literally;
    /// accent folding is applied in SQL via <c>unaccent</c> on both operands.
    /// </summary>
    public static string ContainsPattern(string term)
    {
        var escaped = term
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        return $"%{escaped}%";
    }
}
