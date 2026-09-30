using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;

namespace JuggerHub.Common;

/// <summary>
/// Refuses a hub handshake made with an access token that is already past its expiry (#402), so a
/// hub admits a connection by the same rule it later closes one by.
/// </summary>
/// <remarks>
/// <para>
/// The hubs are mapped with <c>CloseOnAuthenticationExpiration</c>: SignalR closes a connection the
/// moment the token it was made with expires, reading the expiry from the authentication ticket. The
/// JWT validator is more lenient than that — it accepts a token for <c>ClockSkew</c> (30 seconds)
/// <em>past</em> its expiry. Left alone, the two rules disagree for those 30 seconds: a client that
/// reconnects with the token it was just closed for is admitted, joins its <c>user:{id}</c> group,
/// is closed again a second later, and reconnects — a loop that keeps a socket which should be gone
/// open most of the time, and is the whole reconnect path for an account whose session was revoked.
/// </para>
/// <para>
/// Answering 401 here ends that: the client is told to renew its session first, exactly as any REST
/// call would tell it, and an account that cannot renew (signed out, password reset, suspended,
/// banned) has no way back in. It reads the same ticket property SignalR reads, so the two rules
/// cannot drift apart.
/// </para>
/// <para>
/// Scoped by <see cref="HubMetadata"/>, which <c>MapHub</c> puts on both the negotiate and the
/// transport endpoint — so a hub added later is covered without touching this. REST endpoints keep
/// the validator's skew: a request there is over in milliseconds, which is what the skew is for.
/// </para>
/// </remarks>
public sealed class HubTokenExpiryMiddleware
{
    private readonly RequestDelegate _next;

    public HubTokenExpiryMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<HubMetadata>() is not null
            && context.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult?.Properties?.ExpiresUtc is { } expires
            && expires <= DateTimeOffset.UtcNow)
        {
            // The scheme's own challenge, so the body is the same generic 401 every other endpoint gives.
            await context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);
            return;
        }

        await _next(context);
    }
}
