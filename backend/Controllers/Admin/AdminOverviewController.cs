using Asp.Versioning;
using JuggerHub.Dtos.Admin;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers.Admin;

/// <summary>
/// The admin landing aggregate (feature 013 US2). Thin: authorization via the
/// server-side <c>PlatformAdmin</c> policy, then straight to the service.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = PlatformAdminPolicy.Name)]
public sealed class AdminOverviewController : ControllerBase
{
    private readonly IAdminOverviewService _overview;

    public AdminOverviewController(IAdminOverviewService overview) => _overview = overview;

    [HttpGet("overview")]
    public async Task<ActionResult<AdminOverviewDto>> Overview(CancellationToken ct) =>
        Ok(await _overview.GetOverviewAsync(ct));
}
