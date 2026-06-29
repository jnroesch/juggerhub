namespace JuggerHub.Common;

/// <summary>
/// JWT issuance/validation settings bound from the <c>Jwt</c> configuration
/// section (sourced from environment / .env — never hard-coded).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Symmetric signing key (HMAC-SHA256). Must be >= 32 bytes.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
