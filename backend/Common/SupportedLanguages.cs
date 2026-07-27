namespace JuggerHub.Common;

/// <summary>
/// The single backend source of truth for which interface languages JuggerHub supports
/// (feature 031). Kept in parity with the frontend <c>supported-languages.ts</c> constant and the
/// <c>RequestLocalization</c> supported-culture list in <c>Program.cs</c>. English is the default
/// and the universal fallback. Adding a language is a one-line change here plus its catalogs,
/// <c>.resx</c> resources and email folder — no architecture change (spec SC-008 / FR-017).
/// </summary>
public static class SupportedLanguages
{
    /// <summary>Supported BCP-47 base tags, lowercase.</summary>
    public static readonly IReadOnlyList<string> All = new[] { "en", "de", "es" };

    /// <summary>The default and universal fallback language (FR-018).</summary>
    public const string Default = "en";

    /// <summary>True when <paramref name="language"/> is one of the supported base tags.</summary>
    public static bool IsSupported(string? language) =>
        !string.IsNullOrWhiteSpace(language) && All.Contains(language.Trim().ToLowerInvariant());

    /// <summary>
    /// Collapse an arbitrary language tag (e.g. <c>"de-AT"</c>) to a supported base language,
    /// or fall back to <see cref="Default"/> when unsupported/absent (FR-003/FR-012).
    /// </summary>
    public static string ResolveOrDefault(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return Default;
        }

        var baseTag = tag.Split('-')[0].Trim().ToLowerInvariant();
        return All.Contains(baseTag) ? baseTag : Default;
    }
}
