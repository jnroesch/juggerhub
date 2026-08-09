using System.Net;
using System.Text.RegularExpressions;
using JuggerHub.Common;

namespace JuggerHub.Services.Email;

/// <summary>
/// Deliverability details shared by every <see cref="IEmailSender"/> implementation, so Resend
/// (Dev/Prod) and SMTP (local Mailpit) put the same envelope on the wire. Three things live here:
/// a plain-text alternative derived from the rendered HTML, the <c>List-Unsubscribe</c> header
/// value, and bare-address extraction for <c>Reply-To</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is central to the spam problem.</b> Three users reported that mail from the deployed
/// site lands in spam. The dominant cause is domain authentication (SPF/DKIM/DMARC), which lives at
/// the registrar and in Resend's dashboard — outside this repository. On top of that, the code was
/// emitting single-part HTML with no unsubscribe header and no reply target, all of which Gmail and
/// Yahoo's bulk-sender rules score against. This helper fixes the parts that ARE in the code; it
/// cannot fix DNS, and no code change here will rescue a domain that fails authentication.
/// </para>
/// <para>
/// <b>One-click unsubscribe is deliberately NOT emitted.</b> RFC 8058 (<c>List-Unsubscribe-Post:
/// List-Unsubscribe=One-Click</c>) requires an unauthenticated endpoint that actually unsubscribes
/// the recipient on a bare <c>POST</c>. The product has no such endpoint — the notification settings
/// page is behind auth — and advertising one-click without a working target makes a mailbox
/// provider's probe fail, which is worse than omitting it. So we ship the RFC 2369 header only (a
/// <c>mailto:</c> plus the settings link), which still gives Gmail a machine-readable unsubscribe
/// affordance. A real one-click endpoint (signed token, anonymous POST that mutates preferences) is
/// a separate, larger change and belongs in its own spec.
/// </para>
/// </remarks>
public static class EmailDelivery
{
    private const RegexOptions Flags = RegexOptions.Singleline | RegexOptions.IgnoreCase;

    // Whole non-content elements: their text is CSS/JS/head metadata, never prose. Dropped outright
    // so the plain-text body is not a wall of stylesheet rules. Backreference \1 closes the element
    // it opened; IgnoreCase makes </STYLE> close <style>.
    private static readonly Regex NonContentBlocks =
        new(@"<(head|style|script|title)\b[^>]*>.*?</\1\s*>", Flags);

    // HTML comments, including the MSO conditional comments the base template carries.
    private static readonly Regex Comments = new(@"<!--.*?-->", RegexOptions.Singleline);

    // Anchors get special treatment: the destination is often the whole point of the mail (the
    // verification/reset link), so it must survive into the plain-text part, not be stripped with
    // the tag.
    private static readonly Regex Anchor =
        new(@"<a\b[^>]*?\bhref\s*=\s*(?:""|')(?<href>[^""']*)(?:""|')[^>]*>(?<text>.*?)</a\s*>", Flags);

    // Block-level boundaries become line breaks so paragraphs and list items don't run together.
    private static readonly Regex Breaks =
        new(@"<br\b[^>]*>|</(p|div|tr|li|h[1-6]|table|ul|ol|blockquote)\s*>", Flags);

    private static readonly Regex AnyTag = new(@"<[^>]+>", RegexOptions.Singleline);

    // Runs of horizontal whitespace, never newlines.   is the non-breaking space that &nbsp;
    // decodes to — collapsed here so a decoded body reads with ordinary spaces.
    private static readonly Regex InlineWhitespace = new(@"[ \t\f\v\u00a0]+");
    private static readonly Regex SpacesAroundNewline = new(@"[ \t]*\n[ \t]*");
    private static readonly Regex BlankLineRuns = new(@"\n{3,}");

    /// <summary>
    /// Derives a readable <c>text/plain</c> alternative from a rendered HTML email. Not a full HTML
    /// renderer — it strips chrome, keeps prose, and preserves link destinations — but enough to make
    /// every message multipart, which single-part HTML-only mail is penalised for.
    /// </summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = NonContentBlocks.Replace(html, "\n");
        text = Comments.Replace(text, " ");

        text = Anchor.Replace(text, match =>
        {
            var href = (WebUtility.HtmlDecode(match.Groups["href"].Value) ?? string.Empty).Trim();
            var inner = AnyTag.Replace(match.Groups["text"].Value, " ");
            inner = (WebUtility.HtmlDecode(inner) ?? string.Empty).Trim();
            inner = InlineWhitespace.Replace(inner, " ");

            // A mailto: or an empty/self-describing link adds nothing by repeating the URL.
            if (href.Length == 0 || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                return inner;
            }

            if (inner.Length == 0 || string.Equals(inner, href, StringComparison.OrdinalIgnoreCase))
            {
                return href;
            }

            // Parentheses, NOT angle brackets: the tag-stripping pass below is `<[^>]+>`, so a
            // "text <url>" form would be swallowed whole as if it were markup.
            return $"{inner} ({href})";
        });

        text = Breaks.Replace(text, "\n");
        text = AnyTag.Replace(text, " ");
        text = WebUtility.HtmlDecode(text) ?? string.Empty;

        text = InlineWhitespace.Replace(text, " ");
        text = SpacesAroundNewline.Replace(text, "\n");
        text = BlankLineRuns.Replace(text, "\n\n");

        return text.Trim();
    }

    /// <summary>
    /// Builds the RFC 2369 <c>List-Unsubscribe</c> header value from configuration: a <c>mailto:</c>
    /// to the sending address and, when a frontend base URL is configured, the notification-settings
    /// link. Returns <see langword="null"/> when neither can be formed (nothing to advertise).
    /// </summary>
    public static string? BuildListUnsubscribe(EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var parts = new List<string>(2);

        if (BareAddress(options.FromAddress) is { } mailbox)
        {
            parts.Add($"<mailto:{mailbox}?subject=unsubscribe>");
        }

        var baseUrl = options.FrontendBaseUrl?.TrimEnd('/');
        if (!string.IsNullOrEmpty(baseUrl))
        {
            parts.Add($"<{baseUrl}/settings/notifications>");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    /// <summary>
    /// Extracts the bare <c>local@domain</c> from either a plain address or the
    /// <c>Display Name &lt;addr&gt;</c> form both senders accept. Returns <see langword="null"/> when
    /// the value is empty or not address-shaped, so callers can decide by presence.
    /// </summary>
    public static string? BareAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim();
        var open = candidate.LastIndexOf('<');
        var close = candidate.LastIndexOf('>');
        if (open >= 0 && close > open)
        {
            candidate = candidate[(open + 1)..close].Trim();
        }

        return candidate.Contains('@') && !candidate.Contains(' ') ? candidate : null;
    }
}
