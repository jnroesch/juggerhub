using System.ComponentModel.DataAnnotations;
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Profile;

// Validation attributes go on record constructor parameters (MVC reads parameter-level
// metadata for positional records) — matching the Dtos/Auth convention.

/// <summary>
/// Owner update to their own profile. The handle is IMMUTABLE and intentionally
/// absent — it can never be changed. <see cref="Pompfen"/> is the full desired
/// selection set; the server replaces existing selections with it.
/// </summary>
public sealed record UpdateProfileRequest(
    [Required, MaxLength(50)] string DisplayName,
    [MaxLength(80)] string? Hometown,
    [MaxLength(280)] string? Description,
    Pompfe[]? Pompfen,
    // Feature 007 — opt-in to appear in player search. Trailing optional so existing
    // callers/payloads stay valid; absent ⇒ false (privacy-safe default).
    bool AppearInSearch = false);

/// <summary>One recent-activity item: an event the player took part in, with team.</summary>
public sealed record ActivityItemDto(string EventName, DateOnly Date, string Location, string TeamLabel);

/// <summary>A team the player belongs to, shown on the profile (public + owner). Feature 005.</summary>
public sealed record ProfileTeamDto(string Slug, string Name, TeamType Type, string? City, TeamRole Role);

/// <summary>
/// The authenticated owner's own profile (editable fields + selections + recent
/// activity). Never contains token material.
/// </summary>
public sealed record OwnerProfileDto(
    string Handle,
    string DisplayName,
    string? Hometown,
    string? Description,
    bool HasAvatar,
    IReadOnlyList<Pompfe> Pompfen,
    IReadOnlyList<ActivityItemDto> RecentActivity,
    IReadOnlyList<ProfileTeamDto> Teams,
    // Feature 007 — whether the owner opted into appearing in player search.
    bool AppearInSearch = false);

/// <summary>
/// The public profile served anonymously at <c>/u/&lt;handle&gt;</c>. MUST NOT
/// carry email, account status, security data, or a raw account id — only the
/// public field set (specs/003-profile/research.md §7).
/// </summary>
public sealed record PublicProfileDto(
    string Handle,
    string DisplayName,
    string? Hometown,
    string? Description,
    bool HasAvatar,
    IReadOnlyList<Pompfe> SelectedPompfen,
    IReadOnlyList<ActivityItemDto> RecentActivity,
    IReadOnlyList<ProfileTeamDto> Teams);

/// <summary>Result of a live handle availability/format check (UX aid; not a security boundary).</summary>
public sealed record HandleAvailabilityDto(string Handle, string Normalized, bool Available, string? Reason);
