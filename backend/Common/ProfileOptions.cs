namespace JuggerHub.Common;

/// <summary>
/// Configuration for the profile feature (avatar size cap, handle length bounds).
/// Bound from the <c>Profile</c> config section with safe defaults so the feature
/// works with zero configuration. No secrets here.
/// </summary>
public sealed class ProfileOptions
{
    public const string SectionName = "Profile";

    /// <summary>Maximum accepted avatar upload size in bytes (default ~2 MB).</summary>
    public int MaxAvatarBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>Minimum handle length (inclusive).</summary>
    public int HandleMinLength { get; set; } = 3;

    /// <summary>Maximum handle length (inclusive).</summary>
    public int HandleMaxLength { get; set; } = 30;
}
