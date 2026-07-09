using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Achievements;
using JuggerHub.Dtos.Recognition;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Services.Achievements;
using JuggerHub.Services.Recognition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers.Admin;

/// <summary>
/// Platform-admin management of the achievement catalog and awards (feature 012). Parallel to
/// <see cref="BadgesAdminController"/>, with optional accomplishment context on grants. Every
/// action requires the server-side <c>PlatformAdmin</c> policy.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/achievements")]
[Authorize(Policy = PlatformAdminPolicy.Name)]
public sealed class AchievementsAdminController : AdminControllerBase
{
    private readonly IAchievementService _achievements;

    public AchievementsAdminController(IAchievementService achievements) => _achievements = achievements;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AchievementDefinitionDto>>> List(
        [FromQuery] PaginationRequest pagination, [FromQuery] bool includeRetired, CancellationToken ct) =>
        Ok(await _achievements.ListDefinitionsAsync(pagination, includeRetired, ct));

    [HttpPost]
    public async Task<ActionResult<AchievementDefinitionDto>> Create(
        [FromBody] AchievementDefinitionUpsertRequest request, CancellationToken ct)
    {
        var dto = await _achievements.CreateDefinitionAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpPut("{definitionId:guid}")]
    public async Task<ActionResult<AchievementDefinitionDto>> Update(
        Guid definitionId, [FromBody] AchievementDefinitionUpsertRequest request, CancellationToken ct)
    {
        var dto = await _achievements.UpdateDefinitionAsync(definitionId, request, ct);
        return dto is null ? DefinitionNotFound() : Ok(dto);
    }

    [HttpDelete("{definitionId:guid}")]
    public async Task<IActionResult> Retire(Guid definitionId, CancellationToken ct) =>
        await _achievements.RetireDefinitionAsync(definitionId, ct) ? NoContent() : DefinitionNotFound();

    [HttpPut("{definitionId:guid}/icon")]
    public async Task<IActionResult> SetIcon(Guid definitionId, CancellationToken ct)
    {
        var bytes = await ReadBodyBytesAsync(ct);
        var outcome = await _achievements.SetIconAsync(definitionId, bytes, ct);
        return outcome switch
        {
            IconOutcome.Stored => NoContent(),
            IconOutcome.DefinitionNotFound => DefinitionNotFound(),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid icon",
                detail: "Provide a PNG, JPEG, or WebP image within the size limit."),
        };
    }

    [HttpPost("{definitionId:guid}/awards")]
    public async Task<ActionResult<AchievementAwardDto>> Grant(
        Guid definitionId, [FromBody] GrantAchievementRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized();
        }

        var (outcome, award) = await _achievements.GrantAsync(definitionId, request, adminId, ct);
        return outcome switch
        {
            GrantOutcome.Granted => StatusCode(StatusCodes.Status201Created, award),
            GrantOutcome.DefinitionNotFound => DefinitionNotFound(),
            GrantOutcome.SubjectNotFound => Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Subject not found", detail: "No player or team matches that handle/slug."),
            GrantOutcome.DefinitionRetired => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Achievement retired", detail: "This achievement is retired and cannot be granted."),
            GrantOutcome.SubjectTypeMismatch => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Subject type mismatch", detail: "This achievement does not apply to that kind of subject."),
            GrantOutcome.Duplicate => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Already awarded", detail: "The subject already holds this achievement."),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Grant failed"),
        };
    }

    [HttpDelete("awards/{awardId:guid}")]
    public async Task<IActionResult> Revoke(Guid awardId, [FromBody] RevokeAwardRequest? request, CancellationToken ct)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized();
        }

        var outcome = await _achievements.RevokeAsync(awardId, request?.Reason, adminId, ct);
        return outcome == RevokeOutcome.Revoked
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Award not found",
                detail: "This award doesn't exist or is already revoked.");
    }

    private ObjectResult DefinitionNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Achievement not found",
            detail: "This achievement definition doesn't exist.");
}
