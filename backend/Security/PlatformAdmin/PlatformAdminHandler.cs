using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using JuggerHub.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace JuggerHub.Security.PlatformAdmin;

/// <summary>
/// The platform-admin gate (feature 013, replacing 012's interim allowlist — GitHub
/// issue #21): authorizes the caller iff their account currently holds the
/// <see cref="PlatformAdminRoleSync.RoleName"/> Identity role. The check goes to the
/// store on every admin request — deliberately NOT a JWT role claim — so a revocation
/// (config change + restart, which re-mirrors the role) takes effect immediately and
/// privilege can never outlive its source on a stale token. Fails closed: no user, no
/// role, or an empty role means no access. The client admin guard remains UX only.
/// </summary>
public sealed class PlatformAdminHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    private readonly UserManager<User> _users;

    public PlatformAdminHandler(UserManager<User> users)
    {
        _users = users;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subject))
        {
            return;
        }

        var user = await _users.FindByIdAsync(subject);
        if (user is not null && await _users.IsInRoleAsync(user, PlatformAdminRoleSync.RoleName))
        {
            context.Succeed(requirement);
        }
    }
}
