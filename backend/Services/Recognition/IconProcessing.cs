using JuggerHub.Services.Media;

namespace JuggerHub.Services.Recognition;

/// <summary>
/// Maps an <see cref="IImageProcessor"/> rejection onto the icon-upload outcome (#101). Shared by
/// the badge and achievement services so both catalogues reject identically and carry the
/// processor's own non-technical reason.
/// </summary>
internal static class IconProcessing
{
    public static IconSetResult ToFailure(ImageProcessingResult processed) =>
        IconSetResult.Fail(
            processed.Status switch
            {
                ImageProcessingStatus.Empty => IconOutcome.Empty,
                ImageProcessingStatus.UnsupportedType => IconOutcome.InvalidType,
                ImageProcessingStatus.DimensionsTooLarge => IconOutcome.DimensionsTooLarge,
                ImageProcessingStatus.Unreadable => IconOutcome.Unreadable,
                // Both the input cap and the stored-output ceiling mean "too big to accept".
                _ => IconOutcome.TooLarge,
            },
            processed.Reason!);
}
