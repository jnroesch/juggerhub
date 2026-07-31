using Asp.Versioning;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Services.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers.Admin;

/// <summary>
/// Media maintenance for platform admins (feature 035 / #97).
/// </summary>
/// <remarks>
/// The sweep is exposed as an operator action rather than run on a timer, deliberately. A process
/// whose job is deleting media is a hazard if it is wrong, and orphans are both rare and inert —
/// so a human triggers it and can see what it reclaimed. See
/// <see cref="MediaReconciliationService"/> for the full reasoning and the condition under which
/// this should become scheduled.
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/media")]
[Authorize(Policy = PlatformAdminPolicy.Name)]
public sealed class AdminMediaController : ControllerBase
{
    private readonly MediaReconciliationService _reconciliation;

    public AdminMediaController(MediaReconciliationService reconciliation) => _reconciliation = reconciliation;

    /// <summary>
    /// Delete stored objects that no descriptor references and that are older than the configured
    /// grace period. Returns what was reclaimed and what was left inside the grace window.
    /// </summary>
    [HttpPost("reconcile")]
    public async Task<ActionResult<MediaReconciliationResult>> Reconcile(CancellationToken ct) =>
        Ok(await _reconciliation.SweepAsync(ct));
}
