using JuggerHub.Common;

namespace JuggerHub.Services.Media;

/// <summary>
/// Reusable, owner-agnostic image normalization (feature 034 / #98): validate → header-only
/// pixel guard → decode → strip metadata → resize → re-encode to WebP. The profile argument
/// selects per-context limits so the same processor serves avatars today and galleries (#99)
/// later. Implementations are stateless and safe to register as a singleton.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Validate, guard, normalize, and re-encode <paramref name="input"/> to WebP per
    /// <paramref name="profile"/>. Never throws for bad input — decode/format failures come
    /// back as a non-Success <see cref="ImageProcessingResult"/> carrying a non-technical reason.
    /// </summary>
    ImageProcessingResult Process(byte[] input, ImageProcessingProfile profile);
}
