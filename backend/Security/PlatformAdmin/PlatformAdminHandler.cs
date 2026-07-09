using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using JuggerHub.Common;
using JuggerHub.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace JuggerHub.Security.PlatformAdmin;

/// <summary>
/// The TEMPORARY platform-admin gate (feature 012, FR-013): authorizes the caller iff their
/// account email is in the configured <see cref="AdminOptions.Emails"/> allowlist. Fails closed —
/// an unconfigured allowlist authorizes no one. Enforced server-side; the client admin guard is
/// UX only. A real admin role replaces just this handler later (GitHub issue #21).
/// </summary>
public sealed class PlatformAdminHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    private readonly UserManager<User> _users;
    private readonly IOptionsMonitor<AdminOptions> _options;

    public PlatformAdminHandler(UserManager<User> users, IOptionsMonitor<AdminOptions> options)
    {
        _users = users;
        _options = options;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        var allowlist = _options.CurrentValue.NormalizedEmails;
        if (allowlist.Count == 0)
        {
            return; // fail closed — no admins configured
        }

        var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subject))
        {
            return;
        }

        var user = await _users.FindByIdAsync(subject);
        var email = user?.Email?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(email) && allowlist.Contains(email))
        {
            context.Succeed(requirement);
        }
    }
}
