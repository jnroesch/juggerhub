namespace JuggerHub.Common;

/// <summary>
/// Configuration for the badges &amp; achievements feature (012). Bound from the
/// <c>Recognition</c> config section with safe defaults so the feature works with zero
/// configuration. No secrets here.
/// </summary>
public sealed class RecognitionOptions
{
    public const string SectionName = "Recognition";

    /// <summary>Maximum earned badges/achievements embedded per subject in a page payload.</summary>
    public int MaxDisplayedPerGroup { get; set; } = 50;
}
