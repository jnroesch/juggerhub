using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>Shares one Testcontainers Postgres + host across all auth test classes.</summary>
[CollectionDefinition("Auth")]
public sealed class AuthCollection : ICollectionFixture<JuggerHubApiFactory>;

internal static class AuthTestHelpers
{
    /// <summary>Meets the policy: upper, lower, digit, non-alphanumeric, len ≥ 8, ≥ 3 unique chars.</summary>
    public const string ValidPassword = "Str0ng!Passw0rd";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string? password = null) =>
        client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = password ?? ValidPassword });

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, bool rememberMe = false) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password, rememberMe });

    public static (Guid UserId, string Token) ParseVerificationLink(string html) => ParseLink(html);
    public static (Guid UserId, string Token) ParseResetLink(string html) => ParseLink(html);

    private static (Guid UserId, string Token) ParseLink(string html)
    {
        var userId = Guid.Parse(Regex.Match(html, "userId=([0-9a-fA-F-]{36})").Groups[1].Value);
        var tokenEncoded = Regex.Match(html, "token=([^\"'&\\s<]+)").Groups[1].Value;
        return (userId, Uri.UnescapeDataString(tokenEncoded));
    }

    /// <summary>Registers, reads the captured verification email, and confirms the account.</summary>
    public static async Task<(Guid UserId, string Email)> RegisterAndVerifyAsync(
        HttpClient client, JuggerHubApiFactory factory, string? email = null, string? password = null)
    {
        email ??= NewEmail();
        var register = await RegisterAsync(client, email, password);
        register.EnsureSuccessStatusCode();

        var mail = factory.EmailSender.LatestFor(email)
            ?? throw new InvalidOperationException("No verification email was captured.");
        var (userId, token) = ParseVerificationLink(mail.HtmlBody);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId, token });
        verify.EnsureSuccessStatusCode();
        return (userId, email);
    }

    /// <summary>Extracts a cookie value from a response's Set-Cookie headers (for HandleCookies=false clients).</summary>
    public static string? CookieValue(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            var match = Regex.Match(cookie, $"^{Regex.Escape(name)}=([^;]+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }
}
