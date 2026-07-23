using Asp.Versioning;
using JuggerHub.Services.Achievements;
using JuggerHub.Services.Badges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Anonymous read of badge/achievement icon images (feature 012). Icons are referenced by
/// public profiles (a profile whose owner opted it public — feature 026) as well as authenticated
/// profile/team pages, so these endpoints stay anonymous by intent; they expose only the icon
/// bytes, never any award or subject data. This is part of the feature-026 anonymous allowlist.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[AllowAnonymous]
public sealed class RecognitionIconsController : ControllerBase
{
    private readonly IBadgeService _badges;
    private readonly IAchievementService _achievements;

    public RecognitionIconsController(IBadgeService badges, IAchievementService achievements)
    {
        _badges = badges;
        _achievements = achievements;
    }

    [HttpGet("badges/{definitionId:guid}/icon")]
    public async Task<IActionResult> BadgeIcon(Guid definitionId, CancellationToken ct)
    {
        var icon = await _badges.GetIconAsync(definitionId, ct);
        return icon is null ? NotFound() : File(icon.Value.Bytes, icon.Value.ContentType);
    }

    [HttpGet("achievements/{definitionId:guid}/icon")]
    public async Task<IActionResult> AchievementIcon(Guid definitionId, CancellationToken ct)
    {
        var icon = await _achievements.GetIconAsync(definitionId, ct);
        return icon is null ? NotFound() : File(icon.Value.Bytes, icon.Value.ContentType);
    }
}
