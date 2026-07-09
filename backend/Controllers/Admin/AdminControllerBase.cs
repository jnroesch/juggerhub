using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers.Admin;

/// <summary>
/// Shared helpers for the platform-admin controllers (feature 012). Concrete controllers carry the
/// <c>[Authorize(Policy = PlatformAdminPolicy.Name)]</c> attribute; this base only provides the
/// caller-id lookup and raw-body reader they both need.
/// </summary>
public abstract class AdminControllerBase : ControllerBase
{
    /// <summary>The authenticated admin's user id from the JWT subject claim.</summary>
    protected bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }

    /// <summary>Read the raw request body into a byte array (icon uploads). Size is capped in the service.</summary>
    protected async Task<byte[]> ReadBodyBytesAsync(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
