using JuggerHub.Dtos.Cities;
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
    LocationDto? Location,
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

    /// <summary>Feature 030 — filter to a single canonical city by NAME (accent/case-insensitive). Null ⇒ any city.</summary>
    public string? City { get; init; }

    /// <summary>Feature 030 — filter to a single country (ISO code or name). Null ⇒ any country.</summary>
    public string? Country { get; init; }

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
    LocationDto? Location,
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

    /// <summary>Feature 030 — filter to a single canonical city by NAME (accent/case-insensitive). Null ⇒ any city.</summary>
    public string? City { get; init; }

    /// <summary>Feature 030 — filter to a single country (ISO code or name). Null ⇒ any country.</summary>
    public string? Country { get; init; }

    public EventSort Sort { get; init; } = EventSort.StartsAtAsc;
}

// ---- Training browse (feature 043) ---------------------------------------------

/// <summary>
/// A public training session as a discovery card (feature 043). One card = one dated session.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="LocationLabel"/> is composed server-side by the same city → venue → legacy rule
/// events use, so a training and an event at the same address read character-for-character
/// identically (spec SC-003). A list never renders street or postal code, so they are deliberately
/// absent — as they are on <see cref="Dtos.Trainings.TrainingSessionRowDto"/>.
/// </para>
/// <para>
/// ⚠ RSVP counts, the visibility flag and the session status are deliberately NOT here. Every listed
/// row is public and scheduled <em>by construction</em> (the query's two unconditional gates), so
/// shipping those fields would invite a client-side re-check that is not the security boundary; and
/// "9 going" on a discovery card reads as capacity, which a training does not have. They live on the
/// session page, one tap away.
/// </para>
/// </remarks>
public sealed record TrainingCardDto(
    Guid SessionId,
    Guid TrainingId,
    string Name,
    string TeamSlug,
    string TeamName,
    bool IsOneOff,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    LocationKind LocationKind,
    LocationDto? Location,
    string LocationLabel);

/// <summary>Public-training browse filters + sort.</summary>
public sealed record TrainingBrowseQuery
{
    /// <summary>Free-text substring over the training name. Blank ⇒ browse all.</summary>
    public string? Q { get; init; }

    /// <summary>
    /// Hide sessions dated before today. Default on. Day-granular by design (feature 043 research
    /// R2): a session that ended earlier today still counts as upcoming, matching every other
    /// trainings query. Cancelled and skipped sessions are excluded regardless of this flag.
    /// </summary>
    public bool HidePast { get; init; } = true;

    /// <summary>Start of the date range (SessionDate &gt;= From), inclusive.</summary>
    public DateOnly? From { get; init; }

    /// <summary>End of the date range (SessionDate &lt;= To), inclusive.</summary>
    public DateOnly? To { get; init; }

    /// <summary>
    /// Filter to a single canonical city by NAME (accent/case-insensitive). Null ⇒ any city.
    /// Resolved through the session's address block, so a relocated session matches ITS city.
    /// </summary>
    public string? City { get; init; }

    /// <summary>Filter to a single country (ISO code or name). Null ⇒ any country.</summary>
    public string? Country { get; init; }

    public TrainingSort Sort { get; init; } = TrainingSort.SessionDateAsc;
}

// ---- Player browse -------------------------------------------------------------

/// <summary>Public player browse card. No email, account id, or internal fields; opted-in only.</summary>
public sealed record PlayerCardDto(
    string Handle,
    string DisplayName,
    LocationDto? Location,
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

    /// <summary>Feature 030 — filter to a single canonical city by NAME (accent/case-insensitive). Null ⇒ any city.</summary>
    public string? City { get; init; }

    /// <summary>Feature 030 — filter to a single country (ISO code or name). Null ⇒ any country.</summary>
    public string? Country { get; init; }

    public PlayerSort Sort { get; init; } = PlayerSort.DisplayNameAsc;
}
