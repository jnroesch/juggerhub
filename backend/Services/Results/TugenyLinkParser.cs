using System.Text.RegularExpressions;

namespace JuggerHub.Services.Results;

/// <summary>
/// Reduces what an event admin pastes — a tugeny.org tournament address or a bare slug — to a
/// validated tournament slug (feature 050, research R6). Pure.
/// </summary>
/// <remarks>
/// This is the only user input that ever reaches an outbound request, and only the slug survives:
/// the host is always the configured <c>Tugeny:BaseUrl</c>, never taken from the input. Anything that
/// is not a tugeny.org tournament address is refused before a request is made, which is what keeps
/// linking free of server-side request forgery (Principle I).
/// </remarks>
public static partial class TugenyLinkParser
{
    public const int MaxSlugLength = 150;

    private static readonly string[] Hosts = ["tugeny.org", "www.tugeny.org"];

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    /// <summary>The tournament slug in <paramref name="input"/>, when it is a tugeny.org tournament.</summary>
    public static bool TryParseSlug(string? input, out string slug)
    {
        slug = string.Empty;
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return false;
        }

        if (!text.Contains('/') && !text.Contains(':'))
        {
            return Accept(text, out slug);
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !Hosts.Contains(uri.Host.ToLowerInvariant())
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        // /tournaments/{slug} with any trailing page (/all-teams, /live-view, /tournament-tree, …).
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && segments[0] == "tournaments" && Accept(segments[1], out slug);
    }

    private static bool Accept(string candidate, out string slug)
    {
        slug = string.Empty;
        if (candidate.Length > MaxSlugLength || !SlugPattern().IsMatch(candidate))
        {
            return false;
        }

        slug = candidate;
        return true;
    }
}
