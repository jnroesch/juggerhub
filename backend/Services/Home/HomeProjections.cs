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

    /// <summary>Build a unified agenda item (Kind=Event) from a raw event row (feature 025).</summary>
    internal static AgendaItemDto ToItem(UpNextRaw r) => new(
        AgendaKind.Event,
        r.EventId,
        r.Name,
        r.StartsAt,
        r.EndsAt,
        LocationLabel(r.City, r.VenueName, r.Location),
        TypeLabel(r.Type, r.CustomTypeLabel),
        Math.Max(r.ParticipationLimit - r.Occupied, 0),
        r.ParticipationLimit,
        r.Mode,
        r.SignupId,
        r.Status,
        r.TeamSlug is null ? null : new TeamGoingDto(r.TeamSlug, r.TeamName ?? r.TeamSlug),
        null,
        null,
        null,
        null);

    /// <summary>Build a unified agenda item (Kind=Training) from a training agenda row (feature 025).</summary>
    internal static AgendaItemDto ToItem(Dtos.Trainings.AgendaSessionDto s) => new(
        AgendaKind.Training,
        s.SessionId,
        s.Name,
        s.SessionDate.ToDateTime(s.StartTime),
        s.SessionDate.ToDateTime(s.EndTime),
        s.LocationKind == LocationKind.Virtual ? "Online" : (s.Location ?? string.Empty),
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        s.Name,
        s.StartTime.ToString("HH\\:mm"),
        s.IsPublicGuest,
        s.MyAnswer);
}
