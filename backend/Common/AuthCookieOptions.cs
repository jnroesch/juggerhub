namespace JuggerHub.Common;

/// <summary>
/// Auth-cookie settings bound from the <c>Auth:Cookies</c> configuration section (GH #246).
/// </summary>
/// <remarks>
/// <see cref="Secure"/> used to be derived from the ASP.NET environment name (<c>!IsDevelopment()</c>).
/// But the internet-facing Dev deployment also runs as <c>Development</c>, so https://dev.juggerhub.com
/// issued its session and 14-day refresh cookies WITHOUT <c>Secure</c> — sendable over plain HTTP.
/// Whether the transport is secure is a fact about the deployment, not about the environment name, so
/// it is configured explicitly, and it defaults to the safe value: only the plain-HTTP local stacks
/// (docker-compose and the test host) turn it off.
/// </remarks>
public sealed class AuthCookieOptions
{
    public const string SectionName = "Auth:Cookies";

    /// <summary>Emit the <c>Secure</c> attribute. Default <c>true</c>; <c>false</c> only for plain-HTTP localhost.</summary>
    public bool Secure { get; set; } = true;
}
