namespace JuggerHub.Entities;

/// <summary>
/// One file a member attached to a <see cref="ChatMessage"/> (feature 049 / #282) — a photo, a
/// PDF, a document. The descriptor row; the bytes live in the media store.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bytes are encrypted, and this row's <see cref="BaseEntity.Id"/> is what they are bound
/// to.</b> The stored object is an AES-256-GCM envelope produced by <c>IChatBlobCipher</c> with
/// this id as associated data, so an object moved to a different attachment — or into a message
/// body — fails its authentication tag instead of decrypting as someone else's file. It also means
/// the database and the store are not independently useful: an object without its row cannot be
/// attributed, and a row without the key cannot be read.
/// </para>
/// <para>
/// <b>Lifetime is the message's lifetime.</b> The foreign key cascades, so no attachment row can
/// outlive the message it belongs to. That covers the rows and nothing else: a stored object is
/// not in the transaction, so the delete path removes objects explicitly and the reconciliation
/// sweep is the backstop for whatever a crash leaves behind. The cascade makes the row side look
/// finished and it is worth saying plainly that the object side is not.
/// </para>
/// <para>
/// <b>There is no <c>IsImage</c> flag, deliberately.</b> Every image is normalized to WebP before
/// storage and nothing else is, so "is this an image?" is a question about
/// <see cref="ContentType"/>. A second column would be a second source of truth, and a projection
/// that set one without the other would produce a PDF rendering as a broken picture with nothing
/// in the row to show which half was wrong.
/// </para>
/// </remarks>
public sealed class ChatAttachment : BaseEntity
{
    public Guid ChatMessageId { get; set; }

    /// <summary>
    /// Where the encrypted bytes live in the media store. <b>Never disclosed to a client</b> — not
    /// in a DTO, not in a header, not in a link (spec FR-023). Minted from a UUIDv4 and unrelated
    /// to anything the sender supplied.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// The type of the <em>stored</em> object, which is what a download is served as. Always
    /// <c>image/webp</c> for an image, whatever arrived; the original type is not retained because
    /// the original bytes are not either.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Size of the stored object in bytes, after normalization. This is what a download costs, so
    /// it is what the file row shows — deliberately not the size of the upload, which for an image
    /// is usually much larger and would misstate the cost to the recipient. The 10 MB limit is
    /// enforced against the <em>input</em>, before any processing (spec FR-012).
    /// </summary>
    public int SizeBytes { get; set; }

    /// <summary>
    /// The name the file had on the sender's device.
    /// </summary>
    /// <remarks>
    /// <b>Display data, and only ever that.</b> It fills the file row and the download's
    /// <c>Content-Disposition</c>; it takes no part in building <see cref="ObjectKey"/> and never
    /// reaches a path. Sanitised on the way in — trimmed, bounded, control characters and path
    /// separators removed — so a name like <c>../../etc/passwd</c> is inert as the text it is, and
    /// header-encoded on the way out so a name containing a quote cannot inject a header.
    /// </remarks>
    public string FileName { get; set; } = string.Empty;

    /// <summary>The sender's chosen order within the message, 0-based (spec FR-006).</summary>
    public int Ordinal { get; set; }

    /// <summary>Pixel width of the stored image; null for anything that is not an image.</summary>
    /// <remarks>
    /// Present so a thread can reserve the right space before an image arrives and not jump as it
    /// loads. Stored rather than derived because deriving it would mean decoding the object on
    /// every read of the conversation.
    /// </remarks>
    public int? Width { get; set; }

    /// <summary>Pixel height of the stored image; null for anything that is not an image.</summary>
    public int? Height { get; set; }

    public ChatMessage Message { get; set; } = null!;
}
