using Asp.Versioning;
using JuggerHub.Dtos;
using JuggerHub.Services.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Public health endpoint — proves the frontend → API → database round trip.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    /// <summary>
    /// GET /api/v1/health — overall status + database reachability. Always 200
    /// with a body describing the condition (even when the DB is down) so the
    /// dashboard can render it rather than receive a raw error.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HealthDto>> Get(CancellationToken cancellationToken)
    {
        var health = await _healthService.GetHealthAsync(cancellationToken);
        return Ok(health);
    }
}
