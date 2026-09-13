using System.IO.Compression;
using System.Text;

namespace JuggerHub.Services.Chat.Attachments;

/// <summary>
/// Decides what an uploaded non-image file actually is, from its bytes (feature 049 / #282).
/// </summary>
/// <remarks>
/// <para>
/// <b>This type routes; it does not validate.</b> It answers "what family of thing is this?" so
/// the accept path knows whether to hand the bytes to <c>IImageProcessor</c> or to store them as a
/// document. The processor remains the only authority on whether an image is actually decodable,
/// and refuses anything that is not.
/// </para>
/// <para>
/// <b>Why images are sniffed here rather than left to the processor.</b> The obvious design is to
/// try <c>IImageProcessor</c> first and treat its failure as "not an image" — and it does not
/// work, because the processor reports a PDF and a truncated PNG identically. ImageSharp raises
/// <c>ImageFormatException</c> for anything whose format it does not recognise at all, which the
/// processor maps to <c>Unreadable</c>; its <c>UnsupportedType</c> is reserved for formats it
/// <em>does</em> recognise but the allow-list excludes. So "not an image" arrives as "damaged
/// image", and every document would be refused with the wrong reason. Sniffing the well-known
/// signatures first separates the two questions cleanly: this decides <em>which</em> validator
/// runs, and the validator decides whether the file is any good.
/// </para>
/// <para>
/// <b>Nothing here looks at the file name, the extension, or the client's declared content
/// type.</b> All three are the sender's device talking, and the product already refuses to trust
/// them for avatars. An allow-list checked against a claimed type is not an allow-list.
/// </para>
/// <para>
/// <b>The OOXML case is the one that is easy to get wrong.</b> Every <c>.docx</c>, <c>.xlsx</c>
/// and <c>.pptx</c> <em>is</em> a ZIP archive, so a signature check alone accepts any ZIP renamed
/// to <c>.docx</c> — which is precisely how an archive full of executables gets past an allow-list
/// that believes it is strict. Distinguishing them needs the archive opened and its
/// <c>[Content_Types].xml</c> part read, which is what <see cref="DetectOpenXml"/> does.
/// </para>
/// </remarks>
public static class AttachmentContentType
{
    public const string Pdf = "application/pdf";
    public const string PlainText = "text/plain";
    public const string Word = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Excel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string PowerPoint = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    /// <summary>The image type every accepted image is stored as, after normalization.</summary>
    public const string NormalizedImage = "image/webp";

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] GifSignature = "GIF8"u8.ToArray();
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    /// <summary>What family of file this is, so the accept path knows which validator to run.</summary>
    public enum Family
    {
        /// <summary>Nothing this platform accepts.</summary>
        Unknown,

        /// <summary>Claims to be an image. <c>IImageProcessor</c> decides whether it really is one.</summary>
        Image,

        /// <summary>A document that is accepted as-is.</summary>
        Document,
    }

    /// <summary>
    /// Which family <paramref name="content"/> belongs to and, for a document, what it is.
    /// </summary>
    public static (Family Family, string? ContentType) Detect(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (LooksLikeImage(content))
        {
            return (Family.Image, null);
        }

        if (StartsWith(content, PdfSignature))
        {
            return (Family.Document, Pdf);
        }

        if (StartsWith(content, ZipSignature))
        {
            var openXml = DetectOpenXml(content);
            return openXml is null ? (Family.Unknown, null) : (Family.Document, openXml);
        }

        // Text last: it has no signature, so "is it text?" can only be answered by trying to read
        // it as text. Checking it earlier would let a binary that happens to decode cleanly be
        // filed as text instead of being recognised or refused on its own merits.
        return IsPlainText(content) ? (Family.Document, PlainText) : (Family.Unknown, null);
    }

    /// <summary>
    /// Whether the bytes carry the signature of an image format the platform accepts.
    /// </summary>
    /// <remarks>
    /// A claim, not a verdict: a truncated PNG still starts with the PNG signature, and it is
    /// <c>IImageProcessor</c> that then refuses it. Keeping this to signatures — rather than
    /// asking ImageSharp to identify the file — is what lets "damaged image" and "not an image"
    /// be told apart at all.
    /// </remarks>
    private static bool LooksLikeImage(byte[] content) =>
        StartsWith(content, PngSignature)
        || StartsWith(content, JpegSignature)
        || StartsWith(content, GifSignature)
        || IsRiffWebp(content);

    /// <summary>WebP is a RIFF container: <c>RIFF</c>, four bytes of length, then <c>WEBP</c>.</summary>
    private static bool IsRiffWebp(byte[] content) =>
        content.Length >= 12
        && StartsWith(content, RiffSignature)
        && content.AsSpan(8, 4).SequenceEqual(WebpSignature);

    /// <summary>
    /// The extension to hang on the stored object's key, for operator legibility only. Derived
    /// from the <em>stored</em> content type — never from a supplied file name.
    /// </summary>
    public static string ExtensionFor(string contentType) => contentType switch
    {
        NormalizedImage => "webp",
        Pdf => "pdf",
        PlainText => "txt",
        Word => "docx",
        Excel => "xlsx",
        PowerPoint => "pptx",
        _ => "bin",
    };

    /// <summary>Whether a stored object is an image, and therefore renders inline rather than downloading.</summary>
    public static bool IsImage(string contentType) =>
        string.Equals(contentType, NormalizedImage, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opens the archive and reads <c>[Content_Types].xml</c> to tell a real OOXML document from a
    /// ZIP wearing its extension.
    /// </summary>
    /// <remarks>
    /// The part declares an override for the document's own main part, and which override is
    /// present is what distinguishes Word from Excel from PowerPoint. A ZIP without that part, or
    /// with one naming something else, is not a document we accept.
    /// </remarks>
    private static string? DetectOpenXml(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var entry = archive.GetEntry("[Content_Types].xml");
            if (entry is null)
            {
                return null;
            }

            // Bounded read: the part is a small manifest in every real document, and an archive
            // claiming otherwise is not one we are going to accept anyway. This is also what stops
            // a crafted entry expanding without limit (a zip bomb) while we look at it.
            if (entry.Length > 1024 * 1024)
            {
                return null;
            }

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var manifest = reader.ReadToEnd();

            if (manifest.Contains("wordprocessingml.document.main+xml", StringComparison.Ordinal))
            {
                return Word;
            }

            if (manifest.Contains("spreadsheetml.sheet.main+xml", StringComparison.Ordinal))
            {
                return Excel;
            }

            if (manifest.Contains("presentationml.presentation.main+xml", StringComparison.Ordinal))
            {
                return PowerPoint;
            }

            return null;
        }
        catch (InvalidDataException)
        {
            // Not a readable archive after all. Refused, never thrown out of the accept path.
            return null;
        }
    }

    /// <summary>
    /// Whether the bytes decode as UTF-8 text carrying no control characters beyond the ones text
    /// legitimately contains (tab, carriage return, line feed).
    /// </summary>
    /// <remarks>
    /// Strict decoding is the point: <see cref="Encoding.UTF8"/> by default replaces invalid
    /// sequences with U+FFFD and reports success, which would file any binary at all as text. A
    /// throwing decoder is what makes this a test rather than a formality.
    /// </remarks>
    private static bool IsPlainText(byte[] content)
    {
        if (content.Length == 0)
        {
            return true;
        }

        try
        {
            var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(content);

            return !text.Any(c => char.IsControl(c) && c is not ('\t' or '\r' or '\n'));
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool StartsWith(byte[] content, byte[] signature) =>
        content.Length >= signature.Length && content.AsSpan(0, signature.Length).SequenceEqual(signature);
}
