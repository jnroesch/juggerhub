using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace JuggerHub.Services.Notifications.Realtime;

/// <summary>
/// Real-time notification channel (feature 010). Authenticated with the same JWT-in-httpOnly-cookie
/// scheme as the REST API (the browser sends the cookie on the same-origin WebSocket handshake).
/// On connect the connection joins a group named <c>user:{subject}</c> derived from the *validated*
/// token — never from client input — so a client can only ever receive its own stream. There are no
/// client-invokable server methods: the hub is push-only (server → client).
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class NotificationHub : Hub
{
    /// <summary>The SignalR group carrying a single user's notifications.</summary>
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
