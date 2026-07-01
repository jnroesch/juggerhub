using System.Text.RegularExpressions;

namespace JuggerHub.Services.Profile;

/// <summary>
/// The reason a handle was rejected (used to shape UX messages). <see cref="None"/>
/// means the handle is well-formed and not reserved (uniqueness is checked separately).
/// </summary>
public enum HandleRejection
{
    None,
    Empty,
    TooShort,
    TooLong,
    InvalidFormat,
    Reserved,
}

/// <summary>
/// Central handle (username/slug) rules — format, length, and reserved words —
/// applied at registration and by the availability check. URL-safe, lowercase,
/// hyphen-separated; no Unicode (sidesteps confusable/homoglyph impersonation).
/// See specs/003-profile/research.md §1. Uniqueness is enforced elsewhere by the
/// unique index; this type never touches the database.
/// </summary>
public static partial class HandlePolicy
{
    // Lowercase alphanumeric segments joined by single hyphens; no leading/trailing/double hyphen.
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex HandleRegex();

    /// <summary>
    /// Route/system words that must never be claimable as a handle (they collide
    /// with app routes such as <c>/u</c> or are otherwise reserved).
    /// </summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "admin", "api", "u", "login", "logout", "register", "sign-in", "signin",
        "sign-up", "signup", "settings", "account", "profile", "profiles", "me",
        "auth", "dashboard", "static", "assets", "public", "well-known",
        "null", "undefined", "root", "support", "help", "about",
    };

    /// <summary>Trim + lowercase input to its canonical form before validation/storage.</summary>
    public static string Normalize(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Validate a normalized handle against format, length, and reserved rules.
    /// Returns <see cref="HandleRejection.None"/> when acceptable (still subject to
    /// the uniqueness check at persistence time).
    /// </summary>
    public static HandleRejection Validate(string normalized, int minLength, int maxLength)
    {
        if (string.IsNullOrEmpty(normalized))
        {
            return HandleRejection.Empty;
        }

        if (normalized.Length < minLength)
        {
            return HandleRejection.TooShort;
        }

        if (normalized.Length > maxLength)
        {
            return HandleRejection.TooLong;
        }

        if (!HandleRegex().IsMatch(normalized))
        {
            return HandleRejection.InvalidFormat;
        }

        return Reserved.Contains(normalized) ? HandleRejection.Reserved : HandleRejection.None;
    }

    /// <summary>A short, user-facing reason for a rejection (never leaks internals).</summary>
    public static string? Describe(HandleRejection rejection, int minLength, int maxLength) => rejection switch
    {
        HandleRejection.None => null,
        HandleRejection.Empty => "Choose a handle.",
        HandleRejection.TooShort => $"Use at least {minLength} characters.",
        HandleRejection.TooLong => $"Use at most {maxLength} characters.",
        HandleRejection.InvalidFormat => "Use lowercase letters, numbers, and single hyphens.",
        HandleRejection.Reserved => "That handle isn't available.",
        _ => "That handle isn't available.",
    };
}
