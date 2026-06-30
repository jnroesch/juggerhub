namespace JuggerHub.Common;

/// <summary>
/// Constants and options for the auth cookie that carries the JWT. The token is
/// only ever stored in a secure, <c>HttpOnly</c> cookie — never in localStorage
/// or any script-accessible store (constitution Principle IV).
/// </summary>
public static class AuthCookieDefaults
{
    /// <summary>Name of the httpOnly cookie carrying the access-token JWT.</summary>
    public const string AccessTokenCookie = "jh_access";

    /// <summary>Name of the httpOnly cookie carrying the rotating refresh token.</summary>
    public const string RefreshTokenCookie = "jh_refresh";

    /// <summary>
    /// Path the refresh cookie is scoped to, so the long-lived secret is only ever
    /// sent to the auth/refresh endpoints — never on every API call.
    /// </summary>
    public const string RefreshTokenPath = "/api/v1/auth";

    /// <summary>
    /// Builds the access-cookie options. <paramref name="secure"/> is driven by
    /// environment (false on local HTTP, true on HTTPS Dev/Prod). The app and API
    /// are same-origin via the nginx /api proxy, so <c>SameSite=Strict</c> holds.
    /// When <paramref name="persistent"/> is false the cookie is session-scoped
    /// (cleared on browser close); the access JWT's own short lifetime is the real bound.
    /// </summary>
    public static CookieOptions BuildAccessTokenCookieOptions(bool secure, DateTimeOffset expires, bool persistent = true) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = persistent ? expires : null,
        IsEssential = true,
    };

    /// <summary>
    /// Builds the refresh-cookie options: httpOnly, <c>SameSite=Strict</c>, scoped to
    /// <see cref="RefreshTokenPath"/>. Persistent (remember-me) sets an expiry; otherwise
    /// it is a session cookie.
    /// </summary>
    public static CookieOptions BuildRefreshTokenCookieOptions(bool secure, DateTimeOffset expires, bool persistent) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = RefreshTokenPath,
        Expires = persistent ? expires : null,
        IsEssential = true,
    };

    /// <summary>Options used to delete a cookie (must match the original path).</summary>
    public static CookieOptions BuildDeletionOptions(bool secure, string path) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = path,
        IsEssential = true,
    };
}
