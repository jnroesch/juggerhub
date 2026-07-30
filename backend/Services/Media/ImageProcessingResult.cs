namespace JuggerHub.Services.Media;

/// <summary>
/// Outcome of running an uploaded image through <see cref="IImageProcessor"/> (feature 034 / #98).
/// Distinct failure categories so callers can surface non-technical, distinguishable reasons.
/// </summary>
public enum ImageProcessingStatus
{
    Success,
    Empty,
    UnsupportedType,
    InputTooLarge,
    DimensionsTooLarge,
    Unreadable,
    OutputTooLarge,
}

/// <summary>
/// Result of <see cref="IImageProcessor.Process"/>. On <see cref="ImageProcessingStatus.Success"/>
/// the normalized WebP <see cref="Bytes"/> plus its <see cref="ContentType"/> and output
/// dimensions are set; on any failure only <see cref="Status"/> and a non-technical
/// <see cref="Reason"/> are populated (bytes are null).
/// </summary>
public sealed record ImageProcessingResult(
    ImageProcessingStatus Status,
    byte[]? Bytes,
    string? ContentType,
    int Width,
    int Height,
    string? Reason)
{
    public static ImageProcessingResult Ok(byte[] bytes, string contentType, int width, int height) =>
        new(ImageProcessingStatus.Success, bytes, contentType, width, height, null);

    public static ImageProcessingResult Fail(ImageProcessingStatus status, string reason) =>
        new(status, null, null, 0, 0, reason);
}
