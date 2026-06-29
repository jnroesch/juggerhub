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

    /// <summary>
    /// Builds the cookie options. <paramref name="secure"/> is driven by
    /// environment (false on local HTTP, true on HTTPS Dev/Prod). The app and API
    /// are same-origin via the nginx /api proxy, so <c>SameSite=Strict</c> holds.
    /// </summary>
    public static CookieOptions BuildAccessTokenCookieOptions(bool secure, DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expires,
        IsEssential = true,
    };
}
