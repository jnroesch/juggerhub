using Asp.Versioning;
using JuggerHub.Dtos.Recognition;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Services.Achievements;
using JuggerHub.Services.Badges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers.Admin;

/// <summary>
/// Cross-cutting platform-admin reads for the badge/achievement grant UI (feature 012): an
/// access probe for the client nav guard, and a subject's current awards (both families) so the
/// admin can see what's held, mark already-granted items, and revoke by award id. All gated by
/// the server-side <c>PlatformAdmin</c> policy.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = PlatformAdminPolicy.Name)]
public sealed class RecognitionAdminController : ControllerBase
{
    private readonly IBadgeService _badges;
    private readonly IAchievementService _achievements;

    public RecognitionAdminController(IBadgeService badges, IAchievementService achievements)
    {
        _badges = badges;
        _achievements = achievements;
    }

    /// <summary>
    /// Access probe — 200 for a platform admin, 401/403 otherwise. The client nav uses this to
    /// decide whether to render the (UX-only) Admin entry; the server policy is the real boundary.
    /// </summary>
    [HttpGet("access")]
    public IActionResult Access() => Ok(new { isAdmin = true });

    [HttpGet("players/{handle}/awards")]
    public async Task<ActionResult<AdminSubjectAwardsDto>> PlayerAwards(string handle, CancellationToken ct)
    {
        var badges = await _badges.ListPlayerAwardsAsync(handle, ct);
        if (badges is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Player not found",
                detail: "No player matches that handle.");
        }

        var achievements = await _achievements.ListPlayerAwardsAsync(handle, ct) ?? [];
        return Ok(new AdminSubjectAwardsDto(handle.Trim().ToLowerInvariant(), badges, achievements));
    }

    [HttpGet("teams/{slug}/awards")]
    public async Task<ActionResult<AdminSubjectAwardsDto>> TeamAwards(string slug, CancellationToken ct)
    {
        var badges = await _badges.ListTeamAwardsAsync(slug, ct);
        if (badges is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Team not found",
                detail: "No team matches that slug.");
        }

        var achievements = await _achievements.ListTeamAwardsAsync(slug, ct) ?? [];
        return Ok(new AdminSubjectAwardsDto(slug.Trim().ToLowerInvariant(), badges, achievements));
    }
}
