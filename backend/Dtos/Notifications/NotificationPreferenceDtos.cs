using System.ComponentModel.DataAnnotations;
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Notifications;

/// <summary>The signed-in user's effective preference matrix (feature 011): togglable categories with
/// their per-channel state, plus the read-only always-on group. Labels/descriptions are server-owned
/// so the desktop matrix and mobile stack share one source.</summary>
public sealed record NotificationPreferenceMatrixDto(
    IReadOnlyList<PreferenceCategoryDto> Categories,
    IReadOnlyList<AlwaysOnGroupDto> AlwaysOn);

/// <summary>One togglable category row.</summary>
public sealed record PreferenceCategoryDto(
    NotificationCategory Category,
    string Label,
    string Description,
    PreferenceChannelsDto Channels);

/// <summary>The per-channel enabled state for a category (defaults applied for unset cells).</summary>
public sealed record PreferenceChannelsDto(bool InApp, bool Email);

/// <summary>A read-only "always on" group shown in settings but never togglable (e.g. security and sign-in).</summary>
public sealed record AlwaysOnGroupDto(string Label, string Description);

/// <summary>Upsert one cell (the category + channel are route parameters).</summary>
public sealed record SetPreferenceRequest([Required] bool Enabled);
