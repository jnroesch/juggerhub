using JuggerHub.Services.Media;

namespace JuggerHub.Services.Teams;

/// <summary>Outcome of setting or removing a team logo (feature 051 / #305).</summary>
public enum TeamLogoStatus
{
    Success,

    /// <summary>No team has that slug, OR the caller is not a member of it. Deliberately one
    /// value: the controller maps it to 404 so membership is never disclosed (spec FR-003).</summary>
    NotFoundOrNotMember,

    /// <summary>The caller is a member but not an admin of the team.</summary>
    Forbidden,

    /// <summary>Nothing was uploaded.</summary>
    Empty,

    /// <summary>The bytes are not an image type the platform accepts.</summary>
    InvalidType,

    /// <summary>The upload, or the image it re-encodes to, exceeds the configured ceiling.</summary>
    TooLarge,

    /// <summary>The image's pixel dimensions exceed the decompression guard.</summary>
    DimensionsTooLarge,

    /// <summary>The bytes claim to be an image but cannot be decoded.</summary>
    Unreadable,
}

/// <summary>Result of a logo write — a status plus, on rejection, a reason safe to show a user.</summary>
public sealed record TeamLogoResult(TeamLogoStatus Status, string? Reason)
{
    public static TeamLogoResult Ok() => new(TeamLogoStatus.Success, null);

    public static TeamLogoResult Fail(TeamLogoStatus status, string? reason = null) => new(status, reason);
}

/// <summary>
/// A team's identity logo (feature 051 / #305): set, remove, and read the stored image.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="ITeamService"/> on purpose. That service is the team's membership and
/// settings authority and is already long; a logo is media, and keeping it here means the
/// media-specific concerns — the processing profile, the object-store write ordering, the
/// superseded-object delete — live in one file that reads like its three siblings
/// (<c>ProfileService</c>'s avatar methods, <c>BadgeService</c>'s and
/// <c>AchievementService</c>'s icon methods) rather than being buried among role changes.
/// </para>
/// <para>
/// <b>Authorization is this service's job, not the controller's</b> (Principle I): every method
/// resolves the caller's membership through <see cref="TeamMembershipGuard"/>, exactly as the
/// other team mutations do, so the rules cannot drift between what the interface offers and what
/// the server allows.
/// </para>
/// </remarks>
public interface ITeamLogoService
{
    /// <summary>
    /// Store <paramref name="content"/> as the team's logo, replacing any existing one. Admin-only.
    /// The declared content type is never trusted; the image is normalized before storage and the
    /// original is discarded.
    /// </summary>
    Task<TeamLogoResult> SetAsync(string slug, Guid actorUserId, byte[] content, CancellationToken ct = default);

    /// <summary>
    /// Remove the team's logo and delete the stored object. Admin-only. Idempotent: removing a
    /// logo that is not there succeeds and changes nothing (spec FR-017).
    /// </summary>
    Task<TeamLogoResult> RemoveAsync(string slug, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// The logo image for a slug, or null when there is none, the team is unknown, or the stored
    /// object cannot be read.
    /// </summary>
    /// <remarks>
    /// <b>Not member-gated, and that is deliberate.</b> Every other read on the teams controller
    /// requires membership; this one requires only a session, because browse lists teams to
    /// non-members and spec FR-009 puts logos on those rows. A team's mark is no more private than
    /// the name, city and size browse already shows. The caller still has to be signed in — that
    /// comes from the controller's class-level policy.
    /// </remarks>
    Task<MediaContent?> GetAsync(string slug, CancellationToken ct = default);
}
