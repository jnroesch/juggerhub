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
    public string[] AllowedContentTypes { get; set; } = ["image/png", "image/jpeg", "image/webp"];

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
    /// The showcase-gallery context profile (feature 046 / #99). <b>Fit, never square-crop</b>: a
    /// showcase picture is the subject — a team huddle, a tournament shot, a panorama of the pitch —
    /// and center-cropping it to a square cuts that subject out of exactly the pictures the gallery
    /// exists to show. The avatar profile crops because an avatar is rendered in a circle; nothing
    /// here is. 1280 px covers a full-width phone view and the enlarged desktop view, and the 1 MB
    /// ceiling bounds a full five-image gallery at 5 MB per owner (spec SC-005).
    /// </summary>
    public ImageProcessingProfile Showcase { get; set; } = new()
    {
        ResizeMode = ImageResizeMode.Fit,
        MaxDimension = 1280,
        Quality = 80,
        MaxOutputBytes = 1024 * 1024,
    };
}
