using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Trainings;
using JuggerHub.Services.Trainings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Team-scoped trainings surface (feature 018): the Trainings-tab session list, the admin active-series
/// overview, create, and the outsider public list. Thin — delegates to the training services, which
/// enforce team membership/admin server-side (constitution Principle I). Member-gated reads 404 to
/// non-members so a team's trainings never leak.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teams/{slug}/trainings")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class TeamTrainingsController : ControllerBase
{
    private readonly ITrainingSeriesService _series;

    public TeamTrainingsController(ITrainingSeriesService series) => _series = series;

    [HttpGet("sessions")]
    public async Task<ActionResult<PagedResult<TrainingSessionRowDto>>> Sessions(
        string slug, [FromQuery] string window, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var page = await _series.ListSessionsAsync(slug, window ?? "upcoming", userId, pagination, ct);
        return page is null ? NotFoundResult() : Ok(page);
    }

    [HttpGet("series")]
    public async Task<ActionResult<PagedResult<TrainingSeriesSummaryDto>>> Series(
        string slug, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _series.ListSeriesAsync(slug, userId, pagination, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpGet("public")]
    public async Task<ActionResult<PagedResult<TrainingSessionRowDto>>> Public(
        string slug, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _series.ListPublicAsync(slug, userId, pagination, ct));
    }

    [HttpPost]
    public async Task<ActionResult<CreatedTrainingDto>> Create(
        string slug, [FromBody] CreateTrainingRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _series.CreateAsync(slug, request, userId, ct);
        return result.IsOk
            ? Created($"/api/v1/trainings/{result.Value!.TrainingId}", result.Value)
            : Fail(result.Outcome, result.Error);
    }

    private ObjectResult Fail(TrainingOutcome outcome, string? detail) => TrainingHttp.Fail(this, outcome, detail);

    private ObjectResult NotFoundResult() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "No such team.");

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
