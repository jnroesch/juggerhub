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
/// <param name="Category">The category this row governs.</param>
/// <param name="Label">Server-owned display name, in the caller's language.</param>
/// <param name="Description">Server-owned one-liner explaining what falls under it.</param>
/// <param name="Channels">The effective on/off state of each channel, with defaults applied.</param>
/// <param name="AvailableChannels">
/// Which cells this category actually has (feature 056). Every category offers all three except
/// <see cref="NotificationCategory.Chat"/>, which is Push-only: chat has no Alerts row by design
/// (feature 019, FR-051) and no email producer.
/// </param>
/// <remarks>
/// <b><see cref="Channels"/> is deliberately NOT narrowed for a category with unavailable cells,</b>
/// and the client must branch on <see cref="AvailableChannels"/> rather than on its values. Sending
/// <c>false</c> for a cell that does not exist would be indistinguishable from a member having
/// switched it off, and making the record nullable would push a three-state decision onto four
/// categories that use it correctly today. This says the thing that is actually true — these are
/// the cells that exist — and leaves the value semantics alone.
/// </remarks>
public sealed record PreferenceCategoryDto(
    NotificationCategory Category,
    string Label,
    string Description,
    PreferenceChannelsDto Channels,
    IReadOnlyList<NotificationChannel> AvailableChannels);

/// <summary>
/// The per-channel enabled state for a category (defaults applied for unset cells). The three
/// channels are independent: <see cref="Push"/> being on does not require <see cref="InApp"/> to be
/// on, and switching one off never silences another (feature 055).
/// </summary>
public sealed record PreferenceChannelsDto(bool InApp, bool Email, bool Push);

/// <summary>A read-only "always on" group shown in settings but never togglable (e.g. security and sign-in).</summary>
public sealed record AlwaysOnGroupDto(string Label, string Description);

/// <summary>Upsert one cell (the category + channel are route parameters).</summary>
public sealed record SetPreferenceRequest([Required] bool Enabled);
