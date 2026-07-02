using System.Text.RegularExpressions;

namespace JuggerHub.Services.Teams;

/// <summary>
/// The reason a team slug was rejected (used to shape UX messages). <see cref="None"/>
/// means the slug is well-formed and not reserved (uniqueness is checked separately).
/// </summary>
public enum SlugRejection
{
    None,
    Empty,
    TooShort,
    TooLong,
    InvalidFormat,
    Reserved,
}

/// <summary>
/// Central team-slug rules — format, length, and reserved words — applied at team creation
/// and by the availability check. Mirrors <see cref="Profile.HandlePolicy"/>: URL-safe,
/// lowercase, hyphen-separated, no Unicode. Team slugs live in a separate URL namespace
/// (<c>/t/…</c>) from profile handles (<c>/u/…</c>), so the two may coincide. Uniqueness is
/// enforced elsewhere by the unique index; this type never touches the database.
/// </summary>
public static partial class TeamSlugPolicy
{
    // Lowercase alphanumeric segments joined by single hyphens; no leading/trailing/double hyphen.
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    /// <summary>Route/system words that must never be claimable as a team slug.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "t", "teams", "team", "new", "create", "join", "invite", "invites", "invitations",
        "slug-available", "public", "settings", "members", "member", "api", "admin", "u", "me", "auth", "login",
        "logout", "register", "dashboard", "static", "assets", "public", "well-known",
        "null", "undefined", "root", "support", "help", "about",
    };

    /// <summary>Trim + lowercase input to its canonical form before validation/storage.</summary>
    public static string Normalize(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Validate a normalized slug against format, length, and reserved rules. Returns
    /// <see cref="SlugRejection.None"/> when acceptable (still subject to the uniqueness
    /// check at persistence time).
    /// </summary>
    public static SlugRejection Validate(string normalized, int minLength, int maxLength)
    {
        if (string.IsNullOrEmpty(normalized))
        {
            return SlugRejection.Empty;
        }

        if (normalized.Length < minLength)
        {
            return SlugRejection.TooShort;
        }

        if (normalized.Length > maxLength)
        {
            return SlugRejection.TooLong;
        }

        if (!SlugRegex().IsMatch(normalized))
        {
            return SlugRejection.InvalidFormat;
        }

        return Reserved.Contains(normalized) ? SlugRejection.Reserved : SlugRejection.None;
    }

    /// <summary>A short, user-facing reason for a rejection (never leaks internals).</summary>
    public static string? Describe(SlugRejection rejection, int minLength, int maxLength) => rejection switch
    {
        SlugRejection.None => null,
        SlugRejection.Empty => "Choose a team address.",
        SlugRejection.TooShort => $"Use at least {minLength} characters.",
        SlugRejection.TooLong => $"Use at most {maxLength} characters.",
        SlugRejection.InvalidFormat => "Use lowercase letters, numbers, and single hyphens.",
        SlugRejection.Reserved => "That team address isn't available.",
        _ => "That team address isn't available.",
    };
}
