using JuggerHub.Dtos.Home;
using JuggerHub.Entities;

namespace JuggerHub.Services.Home;

/// <summary>
/// Small in-memory shaping helpers for Home reads. The raw rows are projected in SQL
/// (AsNoTracking) with only the columns needed; label derivation and merging happen in
/// memory (feature 008 research §2/§3).
/// </summary>
internal static class HomeProjections
{
    /// <summary>Raw columns for an Up-next / Open-to-everyone item, projected in SQL.</summary>
    internal sealed record UpNextRaw(
        Guid EventId,
        string Name,
        EventType Type,
        string? CustomTypeLabel,
        DateTime StartsAt,
        DateTime EndsAt,
        string? City,
        string? VenueName,
        string Location,
        int ParticipationLimit,
        int Occupied,
        ParticipantMode Mode,
        Guid? SignupId,
        SignupStatus? Status,
        string? TeamSlug,
        string? TeamName);

    /// <summary>Raw columns for a news item (either source), projected in SQL.</summary>
    internal sealed record NewsRaw(
        string Source, string SourceName, string SourceSlugOrId, string Body, DateTime CreatedDate, Guid Id);

    /// <summary>Best available human location: city, then venue, then the legacy free-text location.</summary>
    internal static string LocationLabel(string? city, string? venue, string location) =>
        !string.IsNullOrWhiteSpace(city) ? city!
        : !string.IsNullOrWhiteSpace(venue) ? venue!
        : location;

    /// <summary>Type name, or the free-text label for <see cref="EventType.Other"/>.</summary>
    internal static string TypeLabel(EventType type, string? customLabel) =>
        type == EventType.Other ? (string.IsNullOrWhiteSpace(customLabel) ? "Other" : customLabel!) : type.ToString();

    /// <summary>Build the client DTO from a raw row.</summary>
    internal static UpNextItemDto ToItem(UpNextRaw r) => new(
        r.EventId,
        r.Name,
        TypeLabel(r.Type, r.CustomTypeLabel),
        r.StartsAt,
        r.EndsAt,
        LocationLabel(r.City, r.VenueName, r.Location),
        Math.Max(r.ParticipationLimit - r.Occupied, 0),
        r.ParticipationLimit,
        r.Mode,
        r.SignupId,
        r.Status,
        r.TeamSlug is null ? null : new TeamGoingDto(r.TeamSlug, r.TeamName ?? r.TeamSlug));
}
