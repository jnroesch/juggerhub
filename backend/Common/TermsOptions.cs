namespace JuggerHub.Common;

/// <summary>
/// The authoritative version of the Terms of Use (feature 041). This is the value written into
/// every <see cref="Entities.TermsAcceptance"/> row — never the version string the client
/// submitted.
/// </summary>
/// <remarks>
/// <para>
/// <b>The client sends the version it displayed; the server refuses anything else.</b>
/// Registration carries a <c>TermsVersion</c> field, which is compared against
/// <see cref="CurrentVersion"/> and then <i>discarded</i>. That comparison is the whole point:
/// it proves the client actually rendered the current document, so the stored record evidences
/// what the person <i>saw</i> rather than what the server assumed. Stamping the current version
/// without checking would silently mis-record anyone holding a stale cached catalogue — the exact
/// failure this feature exists to prevent (spec FR-020, research R1).
/// </para>
/// <para>
/// <b>Kept in parity with the catalogues by a test, not by discipline.</b> The document text
/// lives in <c>frontend/apps/web/public/i18n/legal/{en,de,es}.json</c> under <c>terms.version</c>,
/// and <c>TermsVersionParityTests</c> fails the build if any of them disagrees with this value.
/// Bump this constant and all three catalogues in the same commit — a version change is a code
/// change, deliberately, so it cannot happen without review.
/// </para>
/// <para>
/// Bound from the <c>Terms</c> config section. The default is not a placeholder: a missing or
/// empty configuration section must fall back to a real version rather than to "no version" or
/// "accept anything" (constitution VII — safe defaults, never a disabled limit).
/// </para>
/// </remarks>
public sealed class TermsOptions
{
    public const string SectionName = "Terms";

    /// <summary>
    /// Version identifier of the currently published Terms of Use, in date form. Recorded on
    /// every acceptance; a registration quoting any other value is refused with <c>409</c>.
    /// </summary>
    public string CurrentVersion { get; set; } = "2026-08-03";

    /// <summary>
    /// The configured version, or the built-in default when the section is present but blank.
    /// Guards against <c>Terms__CurrentVersion=</c> resolving to an empty string, which would
    /// otherwise make every acceptance record name nothing at all.
    /// </summary>
    public string ResolvedVersion =>
        string.IsNullOrWhiteSpace(CurrentVersion) ? "2026-08-03" : CurrentVersion.Trim();
}
