using JuggerHub.Entities;
using JuggerHub.Services.Search;

namespace JuggerHub.Dtos.Search;

// Feature 007 — browse/search read models (public fields only) + query bindings.
// Query records use { get; init; } with defaults so [FromQuery] binding matches the
// PaginationRequest convention; unknown/missing values fall back to defaults (never error).

// ---- Team browse ---------------------------------------------------------------

/// <summary>Public team browse card. No roster, invitations, or internal data.</summary>
public sealed record TeamCardDto(
    string Slug,
    string Name,
    string? City,
    int PlayerCount,
    bool BeginnersWelcome,
    string LogoInitial);

/// <summary>Team browse filters + sort (bound from the query string).</summary>
public sealed record TeamBrowseQuery
{
    /// <summary>Free-text substring over team name OR city. Blank ⇒ browse all.</summary>
    public string? Q { get; init; }

    /// <summary>Hide dormant teams (no event participation in the active window). Default on.</summary>
    public bool ActiveOnly { get; init; } = true;

    /// <summary>When true, only teams flagged beginners-welcome.</summary>
    public bool BeginnersWelcome { get; init; }

    /// <summary>Exact-ish city match (accent/case-insensitive). Null ⇒ any city.</summary>
    public string? City { get; init; }

    public TeamSort Sort { get; init; } = TeamSort.NameAsc;
}

// ---- Event browse --------------------------------------------------------------

/// <summary>Public event browse card. No fee/IBAN, signups, or admin data.</summary>
public sealed record EventCardDto(
    Guid Id,
    string Name,
    EventType Type,
    string? CustomTypeLabel,
    DateTime StartsAt,
    DateTime EndsAt,
    LocationKind LocationKind,
    string? City,
    string LocationLabel);

/// <summary>Event browse filters + sort.</summary>
public sealed record EventBrowseQuery
{
    /// <summary>Free-text substring over event name. Blank ⇒ browse all.</summary>
    public string? Q { get; init; }

    /// <summary>Hide events that have already ended. Default on. (Cancelled always excluded.)</summary>
    public bool HidePast { get; init; } = true;

    /// <summary>Start of the date range (StartsAt &gt;= From), inclusive.</summary>
    public DateOnly? From { get; init; }

    /// <summary>End of the date range (StartsAt &lt;= To end-of-day), inclusive.</summary>
    public DateOnly? To { get; init; }

    public EventType? Type { get; init; }

    public string? City { get; init; }

    public EventSort Sort { get; init; } = EventSort.StartsAtAsc;
}

// ---- Player browse -------------------------------------------------------------

/// <summary>Public player browse card. No email, account id, or internal fields; opted-in only.</summary>
public sealed record PlayerCardDto(
    string Handle,
    string DisplayName,
    string? Hometown,
    IReadOnlyList<Pompfe> Positions,
    bool HasAvatar);

/// <summary>Player browse filters + sort. The opt-in gate is NOT here — it is enforced
/// unconditionally server-side.</summary>
public sealed record PlayerBrowseQuery
{
    /// <summary>Free-text substring over display name. Blank ⇒ browse all opted-in players.</summary>
    public string? Q { get; init; }

    /// <summary>Match players whose declared pompfen include ANY of these. Empty ⇒ any position.</summary>
    public List<Pompfe>? Positions { get; init; }

    public string? City { get; init; }

    public PlayerSort Sort { get; init; } = PlayerSort.DisplayNameAsc;
}
