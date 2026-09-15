using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Admin;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Services.Admin;
using JuggerHub.Services.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers.Admin;

/// <summary>
/// The platform admins' tournament-placement queue (feature 050): connect a placement to the team it
/// was, one at a time, or disconnect it. Thin — logic lives in <see cref="IAdminPlacementService"/>.
/// Server-side <c>PlatformAdmin</c> policy is the boundary; team admins and members never reach this.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/results")]
[Authorize(Policy = PlatformAdminPolicy.Name)]
public sealed class AdminResultsController : AdminControllerBase
{
    private readonly IAdminPlacementService _placements;

    public AdminResultsController(IAdminPlacementService placements) => _placements = placements;

    [HttpGet("placements")]
    public async Task<ActionResult<PagedResult<AdminPlacementDto>>> List(
        [FromQuery] bool connected, [FromQuery] string? q, [FromQuery] PaginationRequest pagination, CancellationToken ct)
        => Ok(await _placements.ListAsync(connected, q, pagination, ct));

    [HttpPut("placements/{id:guid}/team")]
    public async Task<ActionResult<AdminPlacementDto>> Connect(Guid id, [FromBody] ConnectTeamRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Map(await _placements.ConnectAsync(id, request.TeamSlug, actorId, ct));
    }

    [HttpDelete("placements/{id:guid}/team")]
    public async Task<ActionResult<AdminPlacementDto>> Disconnect(Guid id, CancellationToken ct) =>
        Map(await _placements.DisconnectAsync(id, ct));

    private ActionResult<AdminPlacementDto> Map(ResultOutcome<AdminPlacementDto> outcome) => outcome.Status switch
    {
        ResultStatus.Ok => Ok(outcome.Value),
        ResultStatus.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: outcome.Detail),
        ResultStatus.AlreadyPlaced => Problem(statusCode: StatusCodes.Status409Conflict, title: "Already placed", detail: outcome.Detail),
        _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: outcome.Detail),
    };
}
