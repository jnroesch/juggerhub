using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Trainings;
using JuggerHub.Entities;
using JuggerHub.Services.Trainings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Training- and session-scoped surface (feature 018): whole-series edit + visibility, and per-session
/// read/RSVP/edit/skip/cancel/visibility/attendance/guest-removal, plus the caller's cross-team
/// "Your trainings" agenda. Thin — the training services enforce team-admin (writes) or member/public
/// (reads/RSVP) server-side; a team-only session is 404 to an outsider so it never leaks.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/trainings")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class TrainingsController : ControllerBase
{
    private readonly ITrainingSeriesService _series;
    private readonly ITrainingSessionService _sessions;
    private readonly ITrainingResponseService _responses;

    public TrainingsController(ITrainingSeriesService series, ITrainingSessionService sessions, ITrainingResponseService responses)
    {
        _series = series;
        _sessions = sessions;
        _responses = responses;
    }

    // --- Whole series ---------------------------------------------------------

    [HttpPatch("{trainingId:guid}")]
    public async Task<ActionResult<SeriesEditResultDto>> EditSeries(Guid trainingId, [FromBody] EditSeriesRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _series.EditSeriesAsync(trainingId, request, userId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPut("{trainingId:guid}/visibility")]
    public async Task<IActionResult> SetSeriesVisibility(Guid trainingId, [FromBody] SetVisibilityRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _series.SetSeriesVisibilityAsync(trainingId, request.Visibility, userId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    // --- Single session -------------------------------------------------------

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<ActionResult<TrainingSessionDetailDto>> Session(Guid sessionId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _sessions.GetDetailAsync(sessionId, userId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPut("sessions/{sessionId:guid}/response")]
    public async Task<ActionResult<TrainingSessionRowDto>> SetResponse(Guid sessionId, [FromBody] SetResponseRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _responses.SetResponseAsync(sessionId, request.Answer, userId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPatch("sessions/{sessionId:guid}")]
    public async Task<ActionResult<TrainingSessionDetailDto>> EditSession(Guid sessionId, [FromBody] EditSessionRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _sessions.EditSingleAsync(sessionId, request, userId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPost("sessions/{sessionId:guid}/skip")]
    public async Task<IActionResult> Skip(Guid sessionId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _sessions.SkipAsync(sessionId, userId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    [HttpPost("sessions/{sessionId:guid}/cancel")]
    public async Task<ActionResult<TrainingSessionRowDto>> Cancel(Guid sessionId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _sessions.CancelAsync(sessionId, userId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPut("sessions/{sessionId:guid}/visibility")]
    public async Task<IActionResult> SetSessionVisibility(Guid sessionId, [FromBody] SetVisibilityRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _sessions.SetSessionVisibilityAsync(sessionId, request.Visibility, userId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    [HttpGet("sessions/{sessionId:guid}/attendance")]
    public async Task<ActionResult<PagedResult<AttendanceEntryDto>>> Attendance(
        Guid sessionId, [FromQuery] string? group, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        TrainingRsvp? parsed = null;
        if (!string.IsNullOrWhiteSpace(group))
        {
            if (!Enum.TryParse<TrainingRsvp>(group, ignoreCase: true, out var g))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid group", detail: "group must be one of: going, maybe, cant.");
            }

            parsed = g;
        }

        var result = await _responses.GetAttendanceAsync(sessionId, parsed, userId, pagination, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpDelete("sessions/{sessionId:guid}/guests/{targetUserId:guid}")]
    public async Task<IActionResult> RemoveGuest(Guid sessionId, Guid targetUserId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _responses.RemoveGuestAsync(sessionId, targetUserId, userId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    // --- Dashboard agenda -----------------------------------------------------

    [HttpGet("/api/v{version:apiVersion}/me/trainings")]
    public async Task<ActionResult<PagedResult<AgendaSessionDto>>> MyAgenda([FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _responses.GetMyAgendaAsync(userId, pagination, ct));
    }

    private ObjectResult Fail(TrainingOutcome outcome, string? detail) => TrainingHttp.Fail(this, outcome, detail);

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
