using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Results;
using JuggerHub.Security.RateLimiting;
using JuggerHub.Services.Results;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JuggerHub.Controllers;

/// <summary>
/// A tournament event's results (feature 050): reading them is open to every signed-in player;
/// writing them is for the event's admins, gated server-side in the result services (Principle I).
/// Shapes: specs/050-tournament-results/contracts/results-api.md.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/events/{eventId:guid}/results")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class EventResultsController : ControllerBase
{
    /// <summary>The problem type that tells the page Tugeny has no final results for this tournament yet.</summary>
    public const string NotFinalizedType = "https://juggerhub.com/problems/tugeny-not-finalized";

    private readonly ITournamentResultService _results;
    private readonly ITugenyImportService _tugeny;

    public EventResultsController(ITournamentResultService results, ITugenyImportService tugeny)
    {
        _results = results;
        _tugeny = tugeny;
    }

    // --- Reads (any signed-in user) -------------------------------------------------------

    [HttpGet]
    public async Task<ActionResult<TournamentResultDto>> Get(Guid eventId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var dto = await _results.GetAsync(eventId, userId, ct);
        return dto is null ? EventNotFound() : Ok(dto);
    }

    [HttpGet("matches")]
    public async Task<ActionResult<PagedResult<TournamentMatchDto>>> GetMatches(
        Guid eventId, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        var page = await _results.GetMatchesAsync(eventId, pagination, ct);
        return page is null ? EventNotFound() : Ok(page);
    }

    // --- The results page (event admins) ---------------------------------------------------

    [HttpGet("editor")]
    public async Task<ActionResult<ResultEditorDto>> GetEditor(Guid eventId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _results.GetEditorAsync(eventId, userId, ct));
    }

    [HttpPut("ranking")]
    public async Task<ActionResult<TournamentResultDto>> SaveRanking(
        Guid eventId, [FromBody] SaveRankingRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _results.ReplaceRankingAsync(eventId, userId, request.Placements, ct));
    }

    [HttpDelete("ranking")]
    public async Task<IActionResult> ClearRanking(Guid eventId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var outcome = await _results.ClearAsync(eventId, userId, ct);
        return outcome.Status == ResultStatus.Ok ? NoContent() : Failure(outcome.Status, outcome.Detail, outcome.Row);
    }

    // --- Tugeny (the only actions that make the server contact Tugeny) ----------------------

    [HttpPut("tugeny-link")]
    [EnableRateLimiting(RateLimitPolicies.Tugeny)]
    public async Task<ActionResult<TugenyLinkedDto>> LinkTugeny(
        Guid eventId, [FromBody] LinkTugenyRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _tugeny.LinkAsync(eventId, userId, request.Address, ct));
    }

    [HttpDelete("tugeny-link")]
    public async Task<IActionResult> UnlinkTugeny(Guid eventId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var outcome = await _tugeny.UnlinkAsync(eventId, userId, ct);
        return outcome.Status == ResultStatus.Ok ? NoContent() : Failure(outcome.Status, outcome.Detail, outcome.Row);
    }

    [HttpGet("tugeny-import")]
    [EnableRateLimiting(RateLimitPolicies.Tugeny)]
    public async Task<ActionResult<TugenyImportPreviewDto>> PreviewTugenyImport(Guid eventId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _tugeny.PreviewAsync(eventId, userId, ct));
    }

    [HttpPost("tugeny-import")]
    [EnableRateLimiting(RateLimitPolicies.Tugeny)]
    public async Task<ActionResult<TournamentResultDto>> CommitTugenyImport(
        Guid eventId, [FromBody] ImportCommitRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _tugeny.CommitAsync(eventId, userId, request.Connections ?? [], ct));
    }

    // --- Mapping -------------------------------------------------------------------------------

    private ActionResult Map<T>(ResultOutcome<T> outcome) =>
        outcome.Status == ResultStatus.Ok ? Ok(outcome.Value) : Failure(outcome.Status, outcome.Detail, outcome.Row);

    /// <summary>Generic problem details only — never an internal message (Principle I).</summary>
    private ActionResult Failure(ResultStatus status, string? detail, int? row)
    {
        var (code, title, fallback) = status switch
        {
            ResultStatus.NotFound => (StatusCodes.Status404NotFound, "Event not found", "No event matches that address."),
            ResultStatus.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden", "Only an admin of this event can do that."),
            ResultStatus.NotATournament => (StatusCodes.Status409Conflict, "Not a tournament", "Only a tournament has results."),
            ResultStatus.Cancelled => (StatusCodes.Status409Conflict, "Event cancelled", "This event is cancelled."),
            ResultStatus.NotStarted => (StatusCodes.Status409Conflict, "Not started", "Results can be recorded once the tournament has started."),
            ResultStatus.Invalid => (StatusCodes.Status400BadRequest, "Invalid ranking", "The ranking is not valid."),
            ResultStatus.TeamNotAllowed => (StatusCodes.Status422UnprocessableEntity, "Team not allowed", "That team can't be connected here."),
            ResultStatus.NotLinked => (StatusCodes.Status409Conflict, "Not linked", "Link this event to its Tugeny tournament first."),
            ResultStatus.TugenyNotFound => (StatusCodes.Status404NotFound, "Tugeny tournament not found", "Tugeny has no tournament at that address."),
            ResultStatus.TugenyNotFinalized => (StatusCodes.Status422UnprocessableEntity, "Not finalized in Tugeny",
                "Tugeny shares results only once the organizers finalize the tournament there."),
            ResultStatus.TugenyUnavailable => (StatusCodes.Status503ServiceUnavailable, "Tugeny unavailable",
                "Tugeny can't be reached right now — try again in a few minutes."),
            _ => (StatusCodes.Status400BadRequest, "Invalid request", "The request is not valid."),
        };

        var problem = new ProblemDetails
        {
            Status = code,
            Title = title,
            Detail = detail ?? fallback,
            Instance = HttpContext.Request.Path,
        };
        if (status == ResultStatus.TugenyNotFinalized)
        {
            problem.Type = NotFinalizedType;
        }
        if (row is not null)
        {
            // Which request row the problem is about, so the page can point at it.
            problem.Extensions["row"] = row;
        }

        return new ObjectResult(problem) { StatusCode = code, ContentTypes = { "application/problem+json" } };
    }

    private ActionResult EventNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Event not found", detail: "No event matches that address.");

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
