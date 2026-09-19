using System.Text.RegularExpressions;
using JuggerHub.Common;

namespace JuggerHub.Services.Teams;

/// <summary>
/// The two path segments of a shared invite link (<c>/join/{slug}/{token}</c>), carried through
/// registration and the verification email so the invitation survives the email hop
/// (feature 053). It is an <b>identity, never a destination</b>: nothing here is a path or a
/// URL, and every place that turns it into one composes that path itself. That is what makes
/// "an emailed link that redirects" a non-issue — the link contains no <i>where</i>.
///
/// <para>
/// <see cref="TryParse"/> validates <b>shape only</b>. Registration never reads the invitation
/// (not even to check it exists): a database read there would add latency to the
/// enumeration-sensitive path for no user-visible gain — the wizard previews the invite anyway —
/// and would make <c>POST /auth/register</c> a second oracle for invite tokens beside the
/// anonymous preview endpoint (research R3). A stale invite is a first-class wizard state, not a
/// registration concern.
/// </para>
/// </summary>
public readonly partial record struct InviteReference(string Slug, string Token)
{
    // Invite tokens are 32 random bytes as base64url with the padding trimmed — 43 characters of
    // [A-Za-z0-9_-] today (TeamInvitationService.NewToken). Bounds rather than {43} so a future
    // change to the token length does not silently drop every invite; the charset is exact.
    [GeneratedRegex("^[A-Za-z0-9_-]{16,128}$")]
    private static partial Regex TokenRegex();

    /// <summary>
    /// Both parts well-formed, or <c>null</c>. A caller treats <c>null</c> as "no invite" and
    /// proceeds exactly as if nothing had been sent — never as a refusal (spec FR-003).
    /// </summary>
    public static InviteReference? TryParse(string? slug, string? token, TeamOptions options)
    {
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalizedSlug = TeamSlugPolicy.Normalize(slug);
        if (TeamSlugPolicy.Validate(normalizedSlug, options.SlugMinLength, options.SlugMaxLength) != SlugRejection.None)
        {
            return null;
        }

        var trimmedToken = token.Trim();
        return TokenRegex().IsMatch(trimmedToken) ? new InviteReference(normalizedSlug, trimmedToken) : null;
    }
}
