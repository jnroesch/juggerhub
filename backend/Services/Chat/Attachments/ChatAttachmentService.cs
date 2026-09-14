using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Chat.Encryption;
using JuggerHub.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Chat.Attachments;

/// <summary>
/// One file as it arrived, before anything has been decided about it.
/// </summary>
/// <remarks>
/// Bytes rather than a stream: every file is bounded by
/// <see cref="ChatConstants.MaxAttachmentBytes"/>, and both of the things that happen next —
/// authenticated encryption and image decoding — need the whole content anyway. It also keeps the
/// service testable without an HTTP request.
/// </remarks>
public sealed record ChatUploadFile(string? FileName, byte[] Content);

/// <summary>Why a file was refused. Maps to a stable code the client localizes (spec FR-040).</summary>
public enum AttachmentRejection
{
    None = 0,
    TooManyFiles,
    FileTooLarge,
    UnsupportedType,
    UnreadableFile,
}

/// <summary>
/// The outcome of accepting a set of uploads: either every file was stored, or none was.
/// </summary>
/// <param name="Rejection">Why it failed; <see cref="AttachmentRejection.None"/> on success.</param>
/// <param name="FileName">Which file failed, for a message that names it. Null for whole-set failures.</param>
/// <param name="Attachments">The rows to commit, on success — already carrying their object keys.</param>
public readonly record struct AttachmentAcceptResult(
    AttachmentRejection Rejection,
    string? FileName,
    IReadOnlyList<ChatAttachment> Attachments)
{
    public bool IsOk => Rejection == AttachmentRejection.None;

    public static AttachmentAcceptResult Ok(IReadOnlyList<ChatAttachment> attachments) =>
        new(AttachmentRejection.None, null, attachments);

    public static AttachmentAcceptResult Fail(AttachmentRejection rejection, string? fileName = null) =>
        new(rejection, fileName, []);
}

/// <summary>Decrypted bytes of one attachment, ready to serve.</summary>
public readonly record struct ChatAttachmentContent(
    byte[] Content,
    string ContentType,
    string FileName,
    string ObjectKey);

public interface IChatAttachmentService
{
    /// <summary>
    /// Validate, normalize, encrypt and store every file, returning rows for the caller to commit.
    /// </summary>
    /// <remarks>
    /// <b>This method never saves.</b> It hands back unattached <see cref="ChatAttachment"/>
    /// entities so the send path can commit them in the same <c>SaveChangesAsync</c> as the
    /// message itself — which is what makes "a message is never posted holding half its
    /// attachments" a property of the transaction rather than a cleanup path (spec FR-008).
    /// </remarks>
    Task<AttachmentAcceptResult> AcceptAsync(IReadOnlyList<ChatUploadFile> files, CancellationToken ct = default);

    /// <summary>
    /// The decrypted bytes of one attachment, or <c>null</c> when the caller may not have them.
    /// </summary>
    /// <remarks>
    /// <b>Null covers four different things, deliberately</b>: no such attachment, the caller is
    /// not a member of its conversation, the message was withdrawn, and the object could not be
    /// read or decrypted. The controller answers 404 to all of them, so the endpoint never becomes
    /// a way to find out which attachments exist (spec FR-025).
    /// </remarks>
    Task<ChatAttachmentContent?> OpenAsync(Guid attachmentId, Guid callerId, CancellationToken ct = default);

    /// <summary>Delete stored objects, for a caller that has decided they are not wanted.</summary>
    Task ReclaimAsync(IEnumerable<string> objectKeys, CancellationToken ct = default);
}

/// <inheritdoc cref="IChatAttachmentService"/>
public sealed class ChatAttachmentService : IChatAttachmentService
{
    private readonly AppDbContext _db;
    private readonly ChatGuard _guard;
    private readonly IMediaStore _store;
    private readonly IImageProcessor _images;
    private readonly IChatBlobCipher _cipher;
    private readonly ImageProcessingOptions _imageOptions;
    private readonly ILogger<ChatAttachmentService> _logger;

    public ChatAttachmentService(
        AppDbContext db,
        ChatGuard guard,
        IMediaStore store,
        IImageProcessor images,
        IChatBlobCipher cipher,
        IOptions<ImageProcessingOptions> imageOptions,
        ILogger<ChatAttachmentService> logger)
    {
        _db = db;
        _guard = guard;
        _store = store;
        _images = images;
        _cipher = cipher;
        _imageOptions = imageOptions.Value;
        _logger = logger;
    }

    public async Task<AttachmentAcceptResult> AcceptAsync(
        IReadOnlyList<ChatUploadFile> files,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count > ChatConstants.MaxAttachmentsPerMessage)
        {
            return AttachmentAcceptResult.Fail(AttachmentRejection.TooManyFiles);
        }

        // Validate and normalize EVERYTHING before storing ANYTHING. A refusal on the last file
        // must not leave the first four already written: the sweep would reclaim them eventually,
        // but "eventually" is not the same as a refused send costing nothing (spec FR-008, SC-006).
        var prepared = new List<PreparedAttachment>(files.Count);

        for (var i = 0; i < files.Count; i++)
        {
            var prepare = Prepare(files[i], i);
            if (prepare.Rejection != AttachmentRejection.None)
            {
                return AttachmentAcceptResult.Fail(
                    prepare.Rejection,
                    AttachmentFileName.Sanitize(files[i].FileName));
            }

            prepared.Add(prepare.Value!);
        }

        // Only now does anything reach the store. Keys are minted per file before the first write
        // so a retried put overwrites the same object rather than leaving a second one behind
        // (feature 035).
        var written = new List<ChatAttachment>(prepared.Count);

        try
        {
            foreach (var item in prepared)
            {
                using var content = new MemoryStream(_cipher.Protect(item.Content, item.Attachment.Id));
                await _store.PutAsync(item.Attachment.ObjectKey, content, item.Attachment.ContentType, ct);
                written.Add(item.Attachment);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Objects already written have no referent and nothing will ever point at them. Try to
            // clear them now; the reconciliation sweep is the backstop if this fails too.
            _logger.LogWarning(ex, "Storing a chat attachment failed; reclaiming {Count} object(s) already written.", written.Count);
            await ReclaimAsync(written.Select(a => a.ObjectKey), CancellationToken.None);
            throw;
        }

        return AttachmentAcceptResult.Ok(written);
    }

    public async Task<ChatAttachmentContent?> OpenAsync(
        Guid attachmentId,
        Guid callerId,
        CancellationToken ct = default)
    {
        // Step 1 — the DESCRIPTOR only. Nothing about the stored object is touched yet, and a
        // withdrawn message's attachment rows are gone, so this is also where "deleted" becomes
        // unreachable rather than merely unlisted.
        var row = await _db.ChatAttachments.AsNoTracking()
            .Where(a => a.Id == attachmentId)
            .Select(a => new
            {
                a.ObjectKey,
                a.ContentType,
                a.FileName,
                a.Message.ConversationId,
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        // Step 2 — membership, against relational data, through the same guard every other chat
        // read uses. A non-member gets exactly what a stranger gets for an id that never existed.
        var access = await _guard.ResolveAsync(row.ConversationId, callerId, ct);
        if (access is null)
        {
            return null;
        }

        // Step 3 — and ONLY now the store. This ordering is the security contract inherited from
        // feature 035: authorization is decided before any byte is fetched, so the store is never
        // the place where visibility is determined.
        var stored = await _store.OpenReadAsync(row.ObjectKey, ct);
        if (stored is null)
        {
            return null;
        }

        byte[] envelope;
        await using (stored)
        {
            using var buffer = new MemoryStream();
            await stored.CopyToAsync(buffer, ct);
            envelope = buffer.ToArray();
        }

        if (!_cipher.TryUnprotect(envelope, attachmentId, out var plaintext))
        {
            // Identifiers and the key version only — never the ciphertext, never the key. Mirrors
            // how an undecryptable message body is reported (feature 047).
            _logger.LogWarning(
                "Chat attachment {AttachmentId} could not be decrypted (key version {KeyVersion}).",
                attachmentId,
                _cipher.VersionOf(envelope));

            return null;
        }

        return new ChatAttachmentContent(plaintext, row.ContentType, row.FileName, row.ObjectKey);
    }

    public async Task ReclaimAsync(IEnumerable<string> objectKeys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(objectKeys);

        foreach (var key in objectKeys)
        {
            try
            {
                await _store.DeleteAsync(key, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Never fail the caller over a reclaim: an object nobody references is inert, and
                // MediaReconciliationService exists precisely to collect the ones that slip
                // through. The key is safe to log — it is not a secret, only undisclosed.
                _logger.LogWarning(ex, "Chat attachment object {ObjectKey} could not be reclaimed; left for reconciliation.", key);
            }
        }
    }

    /// <summary>
    /// Validate one file and turn it into a row plus the bytes to store — without touching the
    /// store, so a later refusal costs nothing.
    /// </summary>
    private (AttachmentRejection Rejection, PreparedAttachment? Value) Prepare(ChatUploadFile file, int ordinal)
    {
        // Size first, against the INPUT. An image shrinks a great deal in normalization, so
        // checking what it became would admit a 40 MB upload on the strength of what it compressed
        // to — after it had already been decoded, which is the expensive part.
        if (file.Content.Length > ChatConstants.MaxAttachmentBytes)
        {
            return (AttachmentRejection.FileTooLarge, null);
        }

        var attachment = new ChatAttachment
        {
            FileName = AttachmentFileName.Sanitize(file.FileName),
            Ordinal = ordinal,
        };

        // Route on the signature, then validate with the right validator. Asking IImageProcessor
        // first would not work: it reports "not an image at all" and "damaged image" identically,
        // so every document would be refused as unreadable (see AttachmentContentType's remarks).
        var (family, documentType) = AttachmentContentType.Detect(file.Content);

        switch (family)
        {
            case AttachmentContentType.Family.Image:
            {
                // The processor is the authority on whether this really is a usable image, and it
                // normalizes in the same step: metadata stripped, orientation baked into the
                // pixels, animation flattened, re-encoded to WebP.
                var processed = _images.Process(file.Content, _imageOptions.ChatImage);
                if (processed.Status != ImageProcessingStatus.Success)
                {
                    // It claimed to be an image and is not a usable one. It must NOT fall through
                    // to document detection, where a truncated PNG would be refused as an
                    // unsupported type rather than as the damaged image it is.
                    return (ImageFailure(processed.Status), null);
                }

                attachment.ContentType = processed.ContentType!;
                attachment.SizeBytes = processed.Bytes!.Length;
                attachment.Width = processed.Width;
                attachment.Height = processed.Height;
                // The name's extension follows what we detected, never what the sender claimed —
                // see AttachmentFileName.WithExtensionFor. For an image it is also simply true:
                // normalization re-encoded the bytes, so a `.jpg` really is a `.webp` now.
                attachment.FileName = AttachmentFileName.WithExtensionFor(attachment.FileName, attachment.ContentType);
                attachment.ObjectKey = MediaObjectKey.Create(
                    MediaKind.ChatAttachment,
                    AttachmentContentType.ExtensionFor(attachment.ContentType));

                return (AttachmentRejection.None, new PreparedAttachment(attachment, processed.Bytes!));
            }

            case AttachmentContentType.Family.Document:
            {
                attachment.ContentType = documentType!;
                attachment.SizeBytes = file.Content.Length;
                attachment.FileName = AttachmentFileName.WithExtensionFor(attachment.FileName, documentType!);
                attachment.ObjectKey = MediaObjectKey.Create(
                    MediaKind.ChatAttachment,
                    AttachmentContentType.ExtensionFor(documentType!));

                return (AttachmentRejection.None, new PreparedAttachment(attachment, file.Content));
            }

            default:
                return (AttachmentRejection.UnsupportedType, null);
        }
    }

    private static AttachmentRejection ImageFailure(ImageProcessingStatus status) => status switch
    {
        // An image whose processed form is still over the ceiling is, to the sender, a file that is
        // too big — which is the truth they can act on, rather than a fact about our encoder.
        ImageProcessingStatus.OutputTooLarge => AttachmentRejection.FileTooLarge,
        ImageProcessingStatus.InputTooLarge => AttachmentRejection.FileTooLarge,
        ImageProcessingStatus.DimensionsTooLarge => AttachmentRejection.FileTooLarge,
        _ => AttachmentRejection.UnreadableFile,
    };

    /// <summary>A row and the plaintext bytes destined for its object, before either is written.</summary>
    private sealed record PreparedAttachment(ChatAttachment Attachment, byte[] Content);
}
