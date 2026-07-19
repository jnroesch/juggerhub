using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace JuggerHub.Services.Chat.Realtime;

/// <summary>
/// Real-time chat channel (feature 019). Authenticated with the same JWT-in-httpOnly-cookie scheme as
/// the REST API (the browser sends the cookie on the same-origin WebSocket handshake). On connect the
/// connection joins a group named <c>user:{subject}</c> derived from the <em>validated</em> token —
/// never from client input — so a client can only ever receive its own stream. There are no
/// client-invokable server methods: the hub is push-only (server → client).
/// </summary>
/// <remarks>
/// <para>
/// A near-copy of feature 010's <c>NotificationHub</c>, deliberately. Its security property is that
/// group membership is derived from the token, so "a client can only receive its own stream" is true
/// by construction. Chat fans out by resolving a conversation's participants <b>server-side</b> and
/// pushing to each one's own user group — rather than a group-per-conversation, which would need an
/// authorized client-invokable <c>Subscribe(conversationId)</c> and reintroduce exactly the
/// authorization decision this design avoids, on the transport where it is easiest to get wrong
/// (research §1).
/// </para>
/// <para>
/// <b>Typing does not arrive here.</b> It is a debounced REST call, keeping the hub push-only and the
/// constitution's "REST is primary" rule intact (research §2).
/// </para>
/// <para>
/// <b>Multi-replica</b>: the deployment runs several pods, so a Redis backplane carries pushes between
/// them (research §10). The backplane is registered on SignalR itself in <c>Program.cs</c>, so nothing
/// here changes. Note the ingress must also use cookie affinity — a backplane fixes fan-out, not the
/// negotiate handshake.
/// </para>
/// </remarks>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class ChatHub : Hub
{
    /// <summary>The SignalR group carrying a single user's chat stream.</summary>
    public static string GroupFor(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        if (TryGetUserId(out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
        }
        else
        {
            // No valid subject — refuse rather than leave an unscoped connection open.
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    private bool TryGetUserId(out Guid userId)
    {
        var subject = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
