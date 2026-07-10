using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Badges;
using JuggerHub.Dtos.Recognition;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Services.Badges;
using JuggerHub.Services.Recognition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers.Admin;

/// <summary>
/// Platform-admin management of the badge catalog and awards (feature 012). Every action requires
/// the server-side <c>PlatformAdmin</c> policy — the security boundary for define/grant/revoke.
/// Thin controller: all logic lives in <see cref="IBadgeService"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/badges")]
[Authorize(Policy = PlatformAdminPolicy.Name)]
public sealed class BadgesAdminController : AdminControllerBase
{
    private readonly IBadgeService _badges;

    public BadgesAdminController(IBadgeService badges) => _badges = badges;

    [HttpGet]
    public async Task<ActionResult<PagedResult<BadgeDefinitionDto>>> List(
        [FromQuery] PaginationRequest pagination, [FromQuery] bool includeRetired, CancellationToken ct) =>
        Ok(await _badges.ListDefinitionsAsync(pagination, includeRetired, ct));

    [HttpPost]
    public async Task<ActionResult<BadgeDefinitionDto>> Create(
        [FromBody] BadgeDefinitionUpsertRequest request, CancellationToken ct)
    {
        var dto = await _badges.CreateDefinitionAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpPut("{definitionId:guid}")]
    public async Task<ActionResult<BadgeDefinitionDto>> Update(
        Guid definitionId, [FromBody] BadgeDefinitionUpsertRequest request, CancellationToken ct)
    {
        var dto = await _badges.UpdateDefinitionAsync(definitionId, request, ct);
        return dto is null ? DefinitionNotFound() : Ok(dto);
    }

    [HttpDelete("{definitionId:guid}")]
    public async Task<IActionResult> Retire(Guid definitionId, CancellationToken ct) =>
        await _badges.RetireDefinitionAsync(definitionId, ct) ? NoContent() : DefinitionNotFound();

    [HttpPost("{definitionId:guid}/reinstate")]
    public async Task<IActionResult> Reinstate(Guid definitionId, CancellationToken ct) =>
        await _badges.ReinstateDefinitionAsync(definitionId, ct) ? NoContent() : DefinitionNotFound();

    [HttpPut("{definitionId:guid}/icon")]
    public async Task<IActionResult> SetIcon(Guid definitionId, CancellationToken ct)
    {
        var bytes = await ReadBodyBytesAsync(ct);
        var outcome = await _badges.SetIconAsync(definitionId, bytes, ct);
        return outcome switch
        {
            IconOutcome.Stored => NoContent(),
            IconOutcome.DefinitionNotFound => DefinitionNotFound(),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid icon",
                detail: "Provide a PNG, JPEG, or WebP image within the size limit."),
        };
    }

    [HttpDelete("{definitionId:guid}/icon")]
    public async Task<IActionResult> RemoveIcon(Guid definitionId, CancellationToken ct) =>
        await _badges.RemoveIconAsync(definitionId, ct) ? NoContent() : DefinitionNotFound();

    [HttpPost("{definitionId:guid}/awards")]
    public async Task<ActionResult<BadgeAwardDto>> Grant(
        Guid definitionId, [FromBody] GrantBadgeRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized();
        }

        var (outcome, award) = await _badges.GrantAsync(definitionId, request, adminId, ct);
        return outcome switch
        {
            GrantOutcome.Granted => StatusCode(StatusCodes.Status201Created, award),
            GrantOutcome.DefinitionNotFound => DefinitionNotFound(),
            GrantOutcome.SubjectNotFound => Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Subject not found", detail: "No player or team matches that handle/slug."),
            GrantOutcome.DefinitionRetired => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Badge retired", detail: "This badge is retired and cannot be granted."),
            GrantOutcome.SubjectTypeMismatch => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Subject type mismatch", detail: "This badge does not apply to that kind of subject."),
            GrantOutcome.Duplicate => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Already awarded", detail: "The subject already holds this badge."),
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

        var outcome = await _badges.RevokeAsync(awardId, request?.Reason, adminId, ct);
        return outcome == RevokeOutcome.Revoked
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "Award not found",
                detail: "This award doesn't exist or is already revoked.");
    }

    private ObjectResult DefinitionNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Badge not found",
            detail: "This badge definition doesn't exist.");
}
