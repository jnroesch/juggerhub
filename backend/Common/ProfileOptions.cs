namespace JuggerHub.Common;

/// <summary>
/// Configuration for the profile feature (handle length bounds). Bound from the
/// <c>Profile</c> config section with safe defaults so the feature works with zero
/// configuration. No secrets here.
/// </summary>
/// <remarks>
/// The avatar upload size cap moved to <see cref="ImageProcessingOptions.MaxInputBytes"/>
/// (feature 034 / #98): uploads are now normalized server-side, so the input cap is generous
/// and the stored-output size is bounded by the processing profile, not this class.
/// </remarks>
public sealed class ProfileOptions
{
    public const string SectionName = "Profile";

    /// <summary>Minimum handle length (inclusive).</summary>
    public int HandleMinLength { get; set; } = 3;

    /// <summary>Maximum handle length (inclusive).</summary>
    public int HandleMaxLength { get; set; } = 30;
}
