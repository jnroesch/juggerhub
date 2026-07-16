using System.Text.RegularExpressions;
using JuggerHub.Entities;

namespace JuggerHub.Services.Chat;

/// <summary>A JuggerHub item a message linked to: what kind, and how to find it.</summary>
public readonly record struct ParsedLink(ChatLinkKind Kind, Guid? Id, string? Slug);

/// <summary>
/// Finds a JuggerHub item link in a message body (feature 019, User Story 7).
/// </summary>
/// <remarks>
/// <para>
/// <b>This class makes no network call, and that is the entire security design.</b> A conventional
/// unfurl service takes an attacker-controlled URL and asks the <em>server</em> to fetch it — which is
/// a textbook SSRF engine, reaching cloud metadata endpoints and anything else the pod can route to.
/// Here, unfurl is pattern-matching our own route shapes and then reading our own database
/// (spec FR-042). The capability to fetch simply never exists, so there is nothing to mitigate.
/// </para>
/// <para>
/// Anything that is not one of our four route shapes — including every external URL — parses to
/// <see cref="ChatLinkKind.None"/> and is delivered as plain text (spec FR-039).
/// </para>
/// <para>
/// The shapes are the app's <em>real</em> routes (<c>app.routes.ts</c>), not the wireframe's
/// illustrative <c>/p/…</c> and <c>/e/…</c> (research §12).
/// </para>
/// </remarks>
public static partial class ChatLinkParser
{
    // A leading absolute-URL prefix is tolerated but never used to decide anything: only the PATH is
    // matched. That keeps "https://evil.example.com/u/ada" from unfurling as one of our players —
    // the host is checked, not ignored.
    private const string HostPattern = @"(?:https?://[^\s/]+)?";

    [GeneratedRegex($@"{HostPattern}/u/(?<handle>[A-Za-z0-9\-_]{{1,30}})\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerLink();

    [GeneratedRegex($@"{HostPattern}/t/(?<slug>[A-Za-z0-9\-_]{{1,50}})\b", RegexOptions.IgnoreCase)]
    private static partial Regex TeamLink();

    [GeneratedRegex($@"{HostPattern}/events/(?<id>[0-9a-fA-F\-]{{36}})\b", RegexOptions.IgnoreCase)]
    private static partial Regex EventLink();

    [GeneratedRegex($@"{HostPattern}/trainings/sessions/(?<id>[0-9a-fA-F\-]{{36}})\b", RegexOptions.IgnoreCase)]
    private static partial Regex TrainingLink();

    // Absolute URLs pointing somewhere that is not us must never unfurl.
    [GeneratedRegex(@"https?://(?<host>[^\s/]+)", RegexOptions.IgnoreCase)]
    private static partial Regex AnyAbsoluteUrl();

    /// <summary>
    /// The first JuggerHub item link in <paramref name="body"/>, or
    /// <see cref="ChatLinkKind.None"/> when there is none.
    /// </summary>
    /// <param name="body">The message text to scan.</param>
    /// <param name="allowedHosts">
    /// Hosts that count as "us". An absolute URL to any other host is ignored, so pasting
    /// <c>https://evil.example.com/t/beavers</c> does not render our Beavers card.
    /// </param>
    public static ParsedLink Parse(string body, IReadOnlyCollection<string> allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new ParsedLink(ChatLinkKind.None, null, null);
        }

        // Training is checked before Event: "/trainings/sessions/{id}" would otherwise be missed, and
        // more importantly a naive ordering could mis-key one kind's id onto another's lookup.
        if (TryMatch(TrainingLink(), body, allowedHosts, out var m) && Guid.TryParse(m!.Groups["id"].Value, out var trainingId))
        {
            return new ParsedLink(ChatLinkKind.Training, trainingId, null);
        }

        if (TryMatch(EventLink(), body, allowedHosts, out m) && Guid.TryParse(m!.Groups["id"].Value, out var eventId))
        {
            return new ParsedLink(ChatLinkKind.Event, eventId, null);
        }

        if (TryMatch(PlayerLink(), body, allowedHosts, out m))
        {
            return new ParsedLink(ChatLinkKind.Player, null, m!.Groups["handle"].Value);
        }

        if (TryMatch(TeamLink(), body, allowedHosts, out m))
        {
            return new ParsedLink(ChatLinkKind.Team, null, m!.Groups["slug"].Value);
        }

        return new ParsedLink(ChatLinkKind.None, null, null);
    }

    private static bool TryMatch(Regex regex, string body, IReadOnlyCollection<string> allowedHosts, out Match? match)
    {
        match = null;

        foreach (Match candidate in regex.Matches(body))
        {
            if (IsOurs(candidate.Value, allowedHosts))
            {
                match = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the matched text is a bare path, or an absolute URL on one of our own hosts.
    /// </summary>
    private static bool IsOurs(string matched, IReadOnlyCollection<string> allowedHosts)
    {
        var absolute = AnyAbsoluteUrl().Match(matched);
        if (!absolute.Success)
        {
            return true; // A relative path can only be ours.
        }

        var host = absolute.Groups["host"].Value;

        // Compare host only, ignoring any port, and case-insensitively.
        var bare = host.Split(':')[0];
        return allowedHosts.Any(h => string.Equals(h, bare, StringComparison.OrdinalIgnoreCase));
    }
}
