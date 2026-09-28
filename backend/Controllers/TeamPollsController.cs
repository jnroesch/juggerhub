using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Teams;
using JuggerHub.Services.Teams;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Team polls (feature 062). The same access rules as the rest of a team's internal space
/// (<see cref="TeamsController"/>): every route needs a signed-in MEMBER, and a caller who is not on
/// the team gets exactly what an unknown team gets — 404 "Team not found" — so nothing here reveals
/// whether a team has polls (spec FR-032). Starting, changing, closing and deleting are admin-only,
/// decided in <see cref="ITeamPollService"/>.
/// </summary>
/// <remarks>
/// Refusals are ProblemDetails with a machine-readable <c>code</c> (and <c>option</c>, the 0-based index
/// of the option a refusal is about), so the client shows its own sentence in the member's language and
/// never this class's English <c>detail</c> (GH #179).
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teams/{slug}/polls")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class TeamPollsController : ControllerBase
{
    private readonly ITeamPollService _polls;

    public TeamPollsController(ITeamPollService polls) => _polls = polls;

    /// <summary>The team's open or closed polls, built for the caller. Member-only.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TeamPollDto>>> List(
        string slug, [FromQuery] TeamPollState state, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var page = await _polls.ListAsync(slug, userId, state, pagination, ct);
        return page is null ? TeamNotFound() : Ok(page);
    }

    /// <summary>Start a poll (admin-only). Every other member is notified once it is saved.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(string slug, [FromBody] CreateTeamPollRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _polls.CreateAsync(slug, userId, request, ct));
    }

    /// <summary>
    /// Change a poll (admin-only, open polls). The question, options and settings only until someone
    /// answers; the close time at any point while it is open. Whether it is anonymous never changes.
    /// </summary>
    [HttpPut("{pollId:guid}")]
    public async Task<IActionResult> Update(
        string slug, Guid pollId, [FromBody] UpdateTeamPollRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _polls.UpdateAsync(slug, pollId, userId, request, ct));
    }

    /// <summary>Give or replace the caller's answer (member-only, open polls).</summary>
    [HttpPut("{pollId:guid}/answer")]
    public async Task<IActionResult> Answer(
        string slug, Guid pollId, [FromBody] AnswerTeamPollRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _polls.AnswerAsync(slug, pollId, userId, request.OptionIds ?? [], ct));
    }

    /// <summary>Withdraw the caller's answer (member-only, open polls). Withdrawing nothing is still 200.</summary>
    [HttpDelete("{pollId:guid}/answer")]
    public async Task<IActionResult> Withdraw(string slug, Guid pollId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _polls.WithdrawAsync(slug, pollId, userId, ct));
    }

    /// <summary>Close an open poll now, for good (admin-only). A second close is 409 <c>closed</c>.</summary>
    [HttpPost("{pollId:guid}/close")]
    public async Task<IActionResult> Close(string slug, Guid pollId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Map(await _polls.CloseAsync(slug, pollId, userId, ct));
    }

    /// <summary>
    /// Delete a poll, open or closed, with its answers and every alert it produced (admin-only). A
    /// second delete is 404, not 204.
    /// </summary>
    [HttpDelete("{pollId:guid}")]
    public async Task<IActionResult> Delete(string slug, Guid pollId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return await _polls.DeleteAsync(slug, pollId, userId, ct) switch
        {
            TeamPollStatus.Ok => NoContent(),
            TeamPollStatus.Forbidden => Forbidden(),
            TeamPollStatus.PollNotFound => PollNotFound(),
            _ => TeamNotFound(),
        };
    }

    private IActionResult Map(TeamPollResult result) => result.Status switch
    {
        TeamPollStatus.Ok => Ok(result.Poll),
        TeamPollStatus.Created => StatusCode(StatusCodes.Status201Created, result.Poll),
        TeamPollStatus.Invalid => Coded(StatusCodes.Status400BadRequest, "Invalid poll", result),
        TeamPollStatus.Conflict => Coded(StatusCodes.Status409Conflict, "Poll conflict", result),
        TeamPollStatus.Forbidden => Forbidden(),
        TeamPollStatus.PollNotFound => PollNotFound(),
        _ => TeamNotFound(),
    };

    /// <summary>A refusal the client can translate: <c>code</c> always, <c>option</c> when it is about one option.</summary>
    private ObjectResult Coded(int status, string title, TeamPollResult result)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = result.Reason,
            Instance = HttpContext.Request.Path,
        };
        problem.Extensions["code"] = JsonNamingPolicy.CamelCase.ConvertName(result.Code!.Value.ToString());
        if (result.OptionIndex is { } option)
        {
            problem.Extensions["option"] = option;
        }

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }

    // The same wording TeamsController uses, so a non-member's answer is identical wherever they ask.
    private ObjectResult TeamNotFound() => Problem(statusCode: StatusCodes.Status404NotFound,
        title: "Team not found", detail: "No team exists at that address, or you're not a member.");

    private ObjectResult PollNotFound() => Problem(statusCode: StatusCodes.Status404NotFound,
        title: "Poll not found", detail: "That poll doesn't exist, or was deleted.");

    private ObjectResult Forbidden() => Problem(statusCode: StatusCodes.Status403Forbidden,
        title: "Forbidden", detail: "Only the team's admins can do that.");

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
