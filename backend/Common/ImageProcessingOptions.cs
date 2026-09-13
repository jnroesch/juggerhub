namespace JuggerHub.Common;

/// <summary>How an image is fitted to its target dimension (feature 034 / #98).</summary>
public enum ImageResizeMode
{
    /// <summary>Downscale preserving aspect ratio so the largest side ≤ MaxDimension; never upscales.</summary>
    Fit,

    /// <summary>Center-crop to a square, then downscale to MaxDimension; never upscales.</summary>
    SquareCrop,
}

/// <summary>
/// Per-context processing settings. Named profiles let avatars and the future gallery (#99)
/// share one processor with different limits (spec Clarifications: avatar = square-crop,
/// gallery = fit). No secrets here.
/// </summary>
public sealed class ImageProcessingProfile
{
    /// <summary>Fit vs square-crop for this context.</summary>
    public ImageResizeMode ResizeMode { get; set; } = ImageResizeMode.SquareCrop;

    /// <summary>Largest output side in pixels. Smaller images are never upscaled.</summary>
    public int MaxDimension { get; set; } = 512;

    /// <summary>WebP encode quality (1–100).</summary>
    public int Quality { get; set; } = 80;

    /// <summary>Stored-output ceiling in bytes; an encode larger than this is rejected.</summary>
    public int MaxOutputBytes { get; set; } = 512 * 1024;
}

/// <summary>
/// Configuration for the server-side image processing pipeline (feature 034 / #98). Bound from
/// the <c>ImageProcessing</c> section with safe defaults so the feature runs with zero
/// configuration and behaves identically across local/Dev/Prod. No secrets here.
/// </summary>
public sealed class ImageProcessingOptions
{
    public const string SectionName = "ImageProcessing";

    /// <summary>Generous input acceptance cap in bytes — accept a large phone photo (the small
    /// stored result is bounded by resize + re-encode, not by this).</summary>
    public int MaxInputBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>Decompression-bomb guard: reject (before decoding pixels) when width*height
    /// exceeds this. ~40 MP comfortably accepts real photos while bounding decode memory.</summary>
    public long MaxDecodePixels { get; set; } = 40_000_000;

    /// <summary>Accepted input types. The declared content type is never trusted — the type
    /// detected from the bytes must be in this list.</summary>
    /// <remarks>
    /// <b>One list for every profile, and feature 049 widened it.</b> Chat attachments accept GIF,
    /// and because this list is shared that also admits a GIF avatar or catalogue icon. The
    /// decision was deliberate rather than incidental: every accepted image is re-encoded to a
    /// still WebP regardless of what arrived, and the processor already flattens animation to the
    /// first frame, so GIF reaches storage as exactly the same kind of object PNG and JPEG do.
    /// Giving each profile its own list would be tidier but would change avatar and icon
    /// validation for the sake of one file type.
    /// </remarks>
    public string[] AllowedContentTypes { get; set; } =
        ["image/png", "image/jpeg", "image/webp", "image/gif"];

    /// <summary>The avatar upload context profile (center square-crop).</summary>
    public ImageProcessingProfile Avatar { get; set; } = new();

    /// <summary>
    /// The badge/achievement icon context profile (#101). Fit rather than square-crop — an icon
    /// is artwork, so cropping it would cut off content; a non-square icon simply stays
    /// non-square. Icons render at ≤56 px, so 256 px is generous for high-DPI displays and keeps
    /// the stored blob tiny.
    /// </summary>
    public ImageProcessingProfile Icon { get; set; } = new()
    {
        ResizeMode = ImageResizeMode.Fit,
        MaxDimension = 256,
        Quality = 80,
        MaxOutputBytes = 128 * 1024,
    };

    /// <summary>
    /// The chat-attachment context profile (feature 049 / #282) — a photo shared into a
    /// conversation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="ImageResizeMode.Fit"/>, never square-crop, and that is the whole reason this
    /// profile exists.</b> <see cref="Avatar"/> centre-crops because an avatar is rendered in a
    /// circle and the subject is one face. Applying that to a team photo would cut the people at
    /// the edges out of the picture the sender chose to share — silently, with no way to get them
    /// back, because the original is discarded.
    /// </para>
    /// <para>
    /// 1600 px is sized to be legible full-screen on a phone and comfortable on a laptop without
    /// storing a print master. The output ceiling is generous by comparison with avatars and icons
    /// because a photo of a pitch full of people carries far more detail than a face or a glyph,
    /// and a ceiling that rejects ordinary phone photos would read as an arbitrary failure.
    /// </para>
    /// </remarks>
    public ImageProcessingProfile ChatImage { get; set; } = new()
    {
        ResizeMode = ImageResizeMode.Fit,
        MaxDimension = 1600,
        Quality = 82,
        MaxOutputBytes = 1_500_000,
    };
}
