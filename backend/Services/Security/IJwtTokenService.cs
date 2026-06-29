using JuggerHub.Entities;

namespace JuggerHub.Services.Security;

/// <summary>
/// Issues signed access-token JWTs for authenticated users. Encapsulates token
/// creation so the later auth feature (sign-in) can mint cookies without
/// reaching into JWT plumbing. No token is issued in the walking skeleton — the
/// service is wired so the protected endpoint's validation has a counterpart.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Creates a signed access token for the user and returns the
    /// compact JWT plus its expiry.</summary>
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
}
