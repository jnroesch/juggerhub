using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Admin;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers.Admin;

/// <summary>
/// Admin team browse for award assignment (feature 014): search the teams list and open a team's
/// detail. Thin — logic lives in <see cref="IAdminTeamService"/>. The team's awards and the
/// assign/revoke actions reuse the existing <c>/admin/teams/{slug}/awards</c> read and the
/// <c>teamSlug</c> grant/revoke routes. Server-side <c>PlatformAdmin</c> policy is the boundary.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/teams")]
[Authorize(Policy = PlatformAdminPolicy.Name)]
public sealed class AdminTeamsController : AdminControllerBase
{
    private readonly IAdminTeamService _adminTeams;

    public AdminTeamsController(IAdminTeamService adminTeams) => _adminTeams = adminTeams;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminTeamListItemDto>>> Search(
        [FromQuery] string? q, [FromQuery] PaginationRequest pagination, CancellationToken ct)
        => Ok(await _adminTeams.SearchAsync(q, pagination, ct));

    [HttpGet("{slug}")]
    public async Task<ActionResult<AdminTeamDetailDto>> Detail(string slug, CancellationToken ct)
    {
        var detail = await _adminTeams.GetDetailAsync(slug, ct);
        return detail is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Team not found",
                detail: "No team matches that slug.")
            : Ok(detail);
    }
}
