namespace JuggerHub.Common;

/// <summary>
/// The neutral stand-in shown wherever a member's display name cannot be resolved — because
/// their account is banned (feature 013, hidden by a global query filter) or erased
/// (feature 037, the profile row is gone).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what makes retained history readable.</b> Feature 037 keeps a departing member's
/// messages and news posts verbatim and severs the authorship (spec FR-024); every one of those
/// surviving rows renders through this string. It is deliberately generic — it must identify
/// nobody and must not vary per person, or it becomes a re-identification channel (FR-026).
/// </para>
/// <para>
/// <b>How the absence arises.</b> Nothing checks account status to decide whether to use this.
/// The projections read <c>Sender.Profile.DisplayName</c> through a left join, and that value is
/// null when the profile is filtered (banned) or deleted (erased). Keying on the null rather than
/// on a status is why feature 037 needed no new rendering logic — and why adding a status value
/// could not break it.
/// </para>
/// <para>
/// Lived as an <c>internal const</c> on <c>ChatConversationService</c> until feature 037 needed it
/// for news-post authors and rosters too. Localized per the <see cref="Services.Email.EmailLocalizer"/>
/// pattern: in-code dictionaries with an English fallback, no resource tooling.
/// </para>
/// </remarks>
public static class MemberPlaceholder
{
    /// <summary>The English text. Unchanged from feature 019 so existing readers see no difference.</summary>
    public const string English = "A former player";

    private static readonly IReadOnlyDictionary<string, string> ByCulture =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = English,
            ["de"] = "Ein ehemaliger Spieler",
            ["es"] = "Un jugador anterior",
        };

    /// <summary>
    /// The placeholder for <paramref name="culture"/>, falling back to English for anything
    /// unsupported or null. Accepts either a base tag ("de") or a full one ("de-DE").
    /// </summary>
    public static string For(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return English;
        }

        if (ByCulture.TryGetValue(culture, out var exact))
        {
            return exact;
        }

        // "de-DE" -> "de". Everything else falls back rather than throwing: a placeholder is not
        // worth failing a request over.
        var dash = culture.IndexOf('-');
        return dash > 0 && ByCulture.TryGetValue(culture[..dash], out var baseTag) ? baseTag : English;
    }
}
