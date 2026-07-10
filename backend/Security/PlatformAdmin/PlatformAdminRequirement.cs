using Microsoft.AspNetCore.Authorization;

namespace JuggerHub.Security.PlatformAdmin;

/// <summary>
/// Authorization requirement for platform-administrator-only operations (feature 012).
/// A marker requirement — the actual check lives in <see cref="PlatformAdminHandler"/>.
/// </summary>
/// <remarks>
/// This requirement is the stable seam for the TEMPORARY admin gate. When a real system-admin
/// role lands (GitHub issue #21), only the handler changes; controllers keep using
/// <c>[Authorize(Policy = PlatformAdminPolicy.Name)]</c> unchanged.
/// </remarks>
public sealed class PlatformAdminRequirement : IAuthorizationRequirement;

/// <summary>Canonical policy name so controllers and registration never drift on a string literal.</summary>
public static class PlatformAdminPolicy
{
    public const string Name = "PlatformAdmin";
}
