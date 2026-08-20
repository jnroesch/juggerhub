using JuggerHub.Services.Media;

namespace JuggerHub.Entities;

/// <summary>
/// One picture in a team's showcase gallery (feature 046 / #99). Same shape as
/// <see cref="ProfileShowcaseImage"/>, owned by a <see cref="Entities.Team"/> instead of a profile,
/// bounded to five per team and managed by that team's admins.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two deliberate differences from the profile gallery.</b>
/// </para>
/// <para>
/// 1. <b>No banned-account query filter.</b> The profile gallery inherits its owner's account
/// standing because it belongs to a person. A team's gallery belongs to the team: hiding it because
/// a member was later banned would punish the team for someone else's conduct. There is
/// deliberately nothing here to filter on.
/// </para>
/// <para>
/// 2. <b>No uploader column.</b> Nothing reads who uploaded a team picture — there is no
/// per-uploader permission, no attribution shown, and no moderation surface anywhere in the product.
/// A <c>UserId</c>-keyed column would enter account deletion's inventory for data nobody consumes;
/// <c>CreatedDate</c> already answers "when". Do not add one until a feature actually reads it.
/// </para>
/// <para>
/// <b>Deleting this row does not delete the object.</b> <c>TeamService.DeleteAsync</c> hands the
/// delete to PostgreSQL's cascade, so no application code runs for these rows — the object keys must
/// be harvested before the delete and the objects removed after it. Anything missed is reclaimed
/// only by the operator-triggered reconciliation sweep.
/// </para>
/// </remarks>
public sealed class TeamShowcaseImage : BaseEntity, IShowcaseImage
{
    public Guid TeamId { get; set; }

    /// <summary>Position within the team's gallery: dense, 0-based. See
    /// <see cref="ProfileShowcaseImage.Position"/>.</summary>
    public int Position { get; set; }

    /// <summary>Optional caption, at most 120 characters. Plain text, treated as untrusted.</summary>
    public string? Caption { get; set; }

    /// <summary>Content type of the stored object; always image/webp after processing (034).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Location of the bytes in the media store. <b>Never</b> disclosed to a client — not in a DTO,
    /// not in a header, not as a link.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Size of the stored object in bytes.</summary>
    public int SizeBytes { get; set; }

    public Team Team { get; set; } = null!;
}
