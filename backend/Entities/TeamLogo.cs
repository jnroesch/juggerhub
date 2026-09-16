namespace JuggerHub.Entities;

/// <summary>
/// A team's logo: the descriptor row that points at the stored image (feature 051 / #305).
/// Kept in a separate table (1:1 optional with <see cref="Team"/>) so team and browse projections
/// never touch it at all — they read only whether one exists.
/// </summary>
/// <remarks>
/// <para>
/// This is the fourth media descriptor, after <see cref="ProfileAvatar"/>, <see cref="BadgeIcon"/>
/// and <see cref="AchievementIcon"/>, and it stays separate from them for the reason recorded on
/// <see cref="ProfileAvatar"/>: a polymorphic media table has no single owner navigation, so an
/// owner-scoped query filter could not be expressed and every gate would have to be re-checked by
/// hand at each call site. This table carries no such filter — a team has no account standing to
/// be hidden by — but it joins the same family so the one mechanism keeps one shape.
/// </para>
/// <para>
/// <b>Deleting this row does not delete the object.</b> The relationship cascades in PostgreSQL,
/// so removing the team removes this row with no application code running and leaves the stored
/// object unreferenced. <c>TeamService.DeleteAsync</c> therefore deletes the object explicitly;
/// the reconciliation sweep is the backstop, not the plan.
/// </para>
/// </remarks>
public sealed class TeamLogo : BaseEntity
{
    public Guid TeamId { get; set; }

    /// <summary>Content type of the stored object; always image/webp after processing (034).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Location of the bytes in the media store. Unguessable by construction and <b>never</b>
    /// disclosed to a client — not in a DTO, not in a header, not as a link.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Size of the stored object in bytes; lets the descriptor describe a fetch without
    /// reaching the media store.</summary>
    public int SizeBytes { get; set; }

    public Team Team { get; set; } = null!;
}
