using JuggerHub.Services.Media;

namespace JuggerHub.Entities;

/// <summary>
/// One picture in a player's showcase gallery (feature 046 / #99) — the descriptor row that points
/// at the stored image. A bounded 1:N collection of at most five per profile, deliberately separate
/// from the 1:1 identity picture in <see cref="ProfileAvatar"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not the avatar, and the two never affect each other.</b> The avatar is an identity
/// picture rendered in a circle; a showcase image is content, fitted rather than square-cropped
/// (see <c>ImageProcessingOptions.Showcase</c>). Adding, reordering, or removing a showcase image
/// must never touch the avatar row, and replacing an avatar must never touch these.
/// </para>
/// <para>
/// <b>Why this is its own table rather than a row in a shared media table.</b> The banned-account
/// rule below is expressed as an EF query filter through <see cref="Profile"/>. A polymorphic media
/// row has no single owner navigation, so that expression could not exist and the ban gate would
/// have to be re-checked by hand at every call site — see the equivalent note on
/// <see cref="ProfileAvatar"/> in <c>AppDbContext</c>, which asks that these tables not be merged.
/// </para>
/// <para>
/// <b>Deleting this row does not delete the object.</b> The relationship cascades in PostgreSQL, so
/// a cascade removes this row with no application code running and leaves the stored object
/// unreferenced. Application code that deletes showcase media must delete the object explicitly —
/// <c>ProfileShowcaseService</c> on the ordinary path, and <c>AccountDeletionService</c> for the
/// cascade. Anything missed is reclaimed only by the operator-triggered reconciliation sweep.
/// </para>
/// </remarks>
public sealed class ProfileShowcaseImage : BaseEntity, IShowcaseImage
{
    public Guid ProfileId { get; set; }

    /// <summary>
    /// Position within the owner's gallery: dense and 0-based, so a gallery of four occupies 0–3
    /// with no gaps. Reads order by <c>(Position, Id)</c> so the order is total and identical for
    /// every viewer even if two rows ever shared a position.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Optional member-supplied caption, at most 120 characters. Null means no caption — a complete
    /// picture, not an incomplete one. Plain text: never rendered as markup, and treated as
    /// untrusted wherever it is shown.
    /// </summary>
    public string? Caption { get; set; }

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

    public PlayerProfile Profile { get; set; } = null!;
}
