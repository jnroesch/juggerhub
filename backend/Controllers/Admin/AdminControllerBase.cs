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

    /// <summary>
    /// Transport cap for icon uploads (#101), matching the avatar endpoint. Generous on input —
    /// a big source image is accepted and normalized down; the *stored* blob is bounded by the
    /// processing profile, not by this. Keeps <see cref="ReadBodyBytesAsync"/> from buffering
    /// an arbitrarily large body.
    /// </summary>
    protected const int MaxIconUploadBytes = 8 * 1024 * 1024;

    /// <summary>Read the raw request body into a byte array (icon uploads). Bounded by the
    /// endpoint's <c>[RequestSizeLimit]</c> and re-checked in the image processor.</summary>
    protected async Task<byte[]> ReadBodyBytesAsync(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
