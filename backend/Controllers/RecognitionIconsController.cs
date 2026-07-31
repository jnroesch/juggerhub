using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Services.Achievements;
using JuggerHub.Services.Badges;
using JuggerHub.Security.RateLimiting;
using JuggerHub.Services.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace JuggerHub.Controllers;

/// <summary>
/// Anonymous read of badge/achievement icon images (feature 012). Icons are referenced by
/// public profiles (a profile whose owner opted it public — feature 026) as well as authenticated
/// profile/team pages, so these endpoints stay anonymous by intent; they expose only the icon
/// bytes, never any award or subject data. This is part of the feature-026 anonymous allowlist.
/// </summary>
/// <remarks>
/// Feature 035: the bytes now stream from the media store rather than from Postgres. Anonymous
/// access is granted here, by the platform, per request — it is emphatically <b>not</b> granted by
/// making the store publicly readable. The container is private for icons exactly as it is for
/// avatars; only the gate in front of it differs.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.MediaRead)]
public sealed class RecognitionIconsController : ControllerBase
{
    private readonly IBadgeService _badges;
    private readonly IAchievementService _achievements;
    private readonly IOptions<MediaStorageOptions> _mediaOptions;

    public RecognitionIconsController(
        IBadgeService badges,
        IAchievementService achievements,
        IOptions<MediaStorageOptions> mediaOptions)
    {
        _badges = badges;
        _achievements = achievements;
        _mediaOptions = mediaOptions;
    }

    [HttpGet("badges/{definitionId:guid}/icon")]
    public async Task<IActionResult> BadgeIcon(Guid definitionId, CancellationToken ct)
    {
        var icon = await _badges.GetIconAsync(definitionId, ct);
        return icon is null
            ? NotFound()
            : MediaResponse.File(this, icon.Value, _mediaOptions.Value);
    }

    [HttpGet("achievements/{definitionId:guid}/icon")]
    public async Task<IActionResult> AchievementIcon(Guid definitionId, CancellationToken ct)
    {
        var icon = await _achievements.GetIconAsync(definitionId, ct);
        return icon is null
            ? NotFound()
            : MediaResponse.File(this, icon.Value, _mediaOptions.Value);
    }
}
