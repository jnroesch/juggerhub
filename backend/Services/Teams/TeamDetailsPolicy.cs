using System.Text.RegularExpressions;

namespace JuggerHub.Services.Teams;

/// <summary>
/// Why a team-details save was refused (feature 061). Travels to the client as the ProblemDetails
/// <c>code</c> extension, so the UI renders its own translated sentence rather than the English
/// <see cref="TeamDetailsProblem.Reason"/> (GH #179) — the same reason the slug check ships a
/// <see cref="SlugRejection"/>.
/// </summary>
public enum TeamDetailsCode
{
    NameInvalid,
    CityRequired,
    MixteamHasCity,
    CityNotFound,
    DescriptionTooLong,
    TooManyLinks,
    LinkLabelInvalid,
    LinkUrlInvalid,
    LinkDuplicate,
}

/// <summary>A refused save: the code, which link it is about (0-based, link codes only), and English prose for API readers.</summary>
public sealed record TeamDetailsProblem(TeamDetailsCode Code, int? LinkIndex, string Reason);

/// <summary>A link that passed the rules, in the form it is stored and shown.</summary>
public sealed record NormalizedTeamLink(string Label, string Url);

/// <summary>
/// The rules for what a team says about itself (feature 061): its description and its links. Pure —
/// never touches the database. The name and the type/city rules are NOT here: they are create's, and
/// <c>TeamService</c> applies the same helper on both paths so the two cannot drift.
/// </summary>
/// <remarks>
/// The limits are constants rather than <c>TeamOptions</c> because they are also column lengths
/// (<c>AppDbContext</c> reads them): a configuration knob raised above its column would turn a
/// validation message into a 500.
/// </remarks>
public static partial class TeamDetailsPolicy
{
    public const int DescriptionMaxLength = 1000;
    public const int MaxLinks = 5;
    public const int LabelMaxLength = 30;
    public const int UrlMaxLength = 500;

    // A leading "scheme:" — letters first, then letters/digits/+/./-. Deciding "did they type a
    // scheme?" this way (rather than by looking for "://", the virtual-link precedent) keeps
    // "javascript:…" and "mailto:…" recognisable as schemes to refuse, and does not mistake a
    // scheme-less address whose query happens to contain "https://" for one that has a scheme.
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*:")]
    private static partial Regex LeadingScheme();

    /// <summary>Trim; blank (spaces and line breaks only) means the team has no description.</summary>
    public static string? NormalizeDescription(string? raw)
    {
        var text = raw?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>The description rule, applied to an already-normalised value.</summary>
    public static TeamDetailsProblem? ValidateDescription(string? normalized) =>
        normalized is { Length: > DescriptionMaxLength }
            ? new TeamDetailsProblem(TeamDetailsCode.DescriptionTooLong, null,
                $"Keep the description to {DescriptionMaxLength} characters.")
            : null;

    /// <summary>
    /// A label as stored, or null when it breaks the rules: trimmed, 1–30 characters, and free of
    /// control characters (a line break is one) and of bidirectional formatting characters. The
    /// latter matter because the label sits right before the link's host on the team page: an
    /// unterminated right-to-left override in the label would visually reverse the host beside it,
    /// which is precisely the disguise showing the host exists to prevent (FR-017).
    /// </summary>
    public static string? NormalizeLabel(string? raw)
    {
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > LabelMaxLength)
        {
            return null;
        }

        foreach (var c in text)
        {
            if (char.IsControl(c) || IsBidiControl(c))
            {
                return null;
            }
        }

        return text;
    }

    /// <summary>
    /// An address as stored, or null when it breaks the rules. An address typed without a scheme is
    /// taken to be <c>https</c>; any other scheme is refused, <c>http</c> included (#359 asks for
    /// secure links only). The host must be a DNS name containing a dot, and the address may carry
    /// no user name or password — <c>https://instagram.com@example.net</c> leads to example.net.
    /// What is returned is the normalised <see cref="Uri.AbsoluteUri"/>: canonical (lowercased host,
    /// escaped spaces), which is what makes duplicate detection meaningful and the stored
    /// <c>href</c> safe to render.
    /// </summary>
    public static string? NormalizeUrl(string? raw)
    {
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (!LeadingScheme().IsMatch(text))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.HostNameType != UriHostNameType.Dns
            || !uri.IdnHost.Contains('.')
            || uri.IdnHost.StartsWith('.')
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        var normalized = uri.AbsoluteUri;
        return normalized.Length <= UrlMaxLength ? normalized : null;
    }

    /// <summary>
    /// Validate and normalise the whole list, stopping at the first problem: the count, then each
    /// link's label and address in order, then duplicates (reported on the later of the two).
    /// </summary>
    public static (IReadOnlyList<NormalizedTeamLink> Links, TeamDetailsProblem? Problem) NormalizeLinks(
        IReadOnlyList<(string? Label, string? Url)>? raw)
    {
        if (raw is null || raw.Count == 0)
        {
            return ([], null);
        }

        if (raw.Count > MaxLinks)
        {
            return ([], new TeamDetailsProblem(TeamDetailsCode.TooManyLinks, null,
                $"A team can list up to {MaxLinks} links."));
        }

        var links = new List<NormalizedTeamLink>(raw.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < raw.Count; i++)
        {
            var label = NormalizeLabel(raw[i].Label);
            if (label is null)
            {
                return ([], new TeamDetailsProblem(TeamDetailsCode.LinkLabelInvalid, i,
                    $"Link {i + 1}: give it a name of 1–{LabelMaxLength} characters."));
            }

            var url = NormalizeUrl(raw[i].Url);
            if (url is null)
            {
                return ([], new TeamDetailsProblem(TeamDetailsCode.LinkUrlInvalid, i,
                    $"Link {i + 1}: use a secure web address (https://…) of up to {UrlMaxLength} characters."));
            }

            if (!seen.Add(url))
            {
                return ([], new TeamDetailsProblem(TeamDetailsCode.LinkDuplicate, i,
                    $"Link {i + 1}: that address is already listed."));
            }

            links.Add(new NormalizedTeamLink(label, url));
        }

        return (links, null);
    }

    // U+061C ARABIC LETTER MARK, U+200E/F LRM/RLM, U+202A–U+202E embeddings and overrides,
    // U+2066–U+2069 isolates.
    private static bool IsBidiControl(char c) =>
        c is '؜' or '‎' or '‏'
            or (>= '‪' and <= '‮')
            or (>= '⁦' and <= '⁩');
}
