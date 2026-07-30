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
}
