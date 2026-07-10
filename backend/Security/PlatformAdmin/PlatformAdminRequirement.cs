using Microsoft.AspNetCore.Authorization;

namespace JuggerHub.Security.PlatformAdmin;

/// <summary>
/// Authorization requirement for platform-administrator-only operations.
/// A marker requirement — the actual check lives in <see cref="PlatformAdminHandler"/>
/// (since feature 013: membership in the <c>PlatformAdmin</c> Identity role).
/// </summary>
/// <remarks>
/// This requirement was the stable seam of 012's interim gate; issue #21's migration
/// swapped only the handler, and controllers keep using
/// <c>[Authorize(Policy = PlatformAdminPolicy.Name)]</c> unchanged.
/// </remarks>
public sealed class PlatformAdminRequirement : IAuthorizationRequirement;

/// <summary>Canonical policy name so controllers and registration never drift on a string literal.</summary>
public static class PlatformAdminPolicy
{
    public const string Name = "PlatformAdmin";
}
