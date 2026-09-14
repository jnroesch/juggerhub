namespace JuggerHub.Services.Chat.Attachments;

/// <summary>
/// Normalises the name a file had on the sender's device into something safe to store and show
/// (feature 049 / #282).
/// </summary>
/// <remarks>
/// <para>
/// <b>A file name here is display data and nothing else.</b> It fills the file row and the
/// download's <c>Content-Disposition</c>. It takes no part in building an object key — those are
/// minted from a UUIDv4 — and it never reaches a path, on the server or in the store. That is the
/// property that makes a name like <c>../../etc/passwd</c> a slightly odd label rather than a
/// traversal: there is nowhere for it to traverse.
/// </para>
/// <para>
/// Stripping the separators anyway is defence in depth, and cheap. What it really buys is that the
/// name shown to a recipient is the name of a file rather than a path into the sender's machine,
/// which is a small privacy win on its own — people share things out of folders called their
/// employer's name.
/// </para>
/// </remarks>
public static class AttachmentFileName
{
    /// <summary>
    /// Trim, strip anything that is not a file name, and bound the length. Returns
    /// <paramref name="fallback"/> when nothing usable survives, so an unnamed upload is still
    /// something a recipient can click.
    /// </summary>
    public static string Sanitize(string? supplied, string fallback = "file")
    {
        if (string.IsNullOrWhiteSpace(supplied))
        {
            return fallback;
        }

        // Some browsers send a full path rather than a bare name; keep only the last segment.
        var name = supplied.Replace('\\', '/');
        var lastSeparator = name.LastIndexOf('/');
        if (lastSeparator >= 0)
        {
            name = name[(lastSeparator + 1)..];
        }

        // Control characters would break the Content-Disposition header and render as nothing;
        // both are reasons to drop them rather than to refuse the file.
        var cleaned = new string([.. name.Where(c => !char.IsControl(c))]).Trim();

        // "." and ".." survive every filter above and are not names.
        if (cleaned is "" or "." or "..")
        {
            return fallback;
        }

        return cleaned.Length > ChatConstants.MaxAttachmentFileNameLength
            ? Truncate(cleaned)
            : cleaned;
    }

    /// <summary>
    /// Force the name's extension to agree with what the file was actually detected to be
    /// (feature 049).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The extension is the only part of a file name that does something.</b> Every operating
    /// system opens a downloaded file by extension, so serving the sender's name unchanged means
    /// the thing that decides which application runs is attacker-controlled — and can disagree with
    /// the content we validated. An archive that satisfies the OOXML check can arrive named
    /// <c>invoice.docm</c> or <c>invoice.exe</c>, and we would hand the recipient exactly that.
    /// Validating content and then serving a contradictory name gives away most of what the
    /// allow-list was for.
    /// </para>
    /// <para>
    /// So the stem is kept — it is the sender's, it is meaningful, and it is only ever displayed —
    /// and the extension is replaced with the canonical one for the type we detected. A file we
    /// decided is a Word document is served as <c>.docx</c>; an image is served as <c>.webp</c>,
    /// which is additionally honest because normalization really did re-encode it.
    /// </para>
    /// </remarks>
    public static string WithExtensionFor(string sanitizedName, string contentType)
    {
        var extension = AttachmentContentType.ExtensionFor(contentType);
        var dot = sanitizedName.LastIndexOf('.');

        // No extension, or something too long to be one: append rather than cut into the name.
        var stem = dot > 0 && sanitizedName.Length - dot <= 16 ? sanitizedName[..dot] : sanitizedName;

        if (stem.Length == 0)
        {
            stem = "file";
        }

        var result = $"{stem}.{extension}";

        return result.Length > ChatConstants.MaxAttachmentFileNameLength
            ? $"{stem[..(ChatConstants.MaxAttachmentFileNameLength - extension.Length - 1)]}.{extension}"
            : result;
    }

    /// <summary>
    /// Shortens an over-long name while keeping its extension, so a truncated file still looks
    /// like the kind of file it is.
    /// </summary>
    private static string Truncate(string name)
    {
        var max = ChatConstants.MaxAttachmentFileNameLength;
        var dot = name.LastIndexOf('.');

        // No extension, or one so long it is not really an extension: just cut.
        if (dot <= 0 || name.Length - dot > 16)
        {
            return name[..max];
        }

        var extension = name[dot..];
        return string.Concat(name.AsSpan(0, max - extension.Length), extension);
    }
}
