using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Marketplace;
using JuggerHub.Entities;
using JuggerHub.Services.Marketplace;
using JuggerHub.Services.Parties;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Event marketplace (feature 017) — the two-sided board, the caller's listing + market context, the
/// request actions (accept/decline/revoke), and the dashboard summary. Board reads are public (they sit
/// on the public event page); every write and inbox read is authenticated and gated server-side
/// (constitution Principle I).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class MarketController : ControllerBase
{
    private readonly IMarketListingService _listings;
    private readonly IMarketRecruitingService _recruiting;
    private readonly IMarketRequestService _requests;

    public MarketController(
        IMarketListingService listings, IMarketRecruitingService recruiting, IMarketRequestService requests)
    {
        _listings = listings;
        _recruiting = recruiting;
        _requests = requests;
    }

    // --- Board (authenticated since feature 026 — event market data is not anonymous) ---------

    [HttpGet("events/{eventId:guid}/market/free-agents")]
    public async Task<ActionResult<PagedResult<MarketListingCardDto>>> FreeAgents(
        Guid eventId, [FromQuery] string? position, [FromQuery] PaginationRequest pagination, CancellationToken ct) =>
        Ok(await _listings.ListFreeAgentsAsync(eventId, ParsePosition(position), pagination, ct));

    [HttpGet("events/{eventId:guid}/market/parties")]
    public async Task<ActionResult<PagedResult<RecruitingPartyCardDto>>> RecruitingParties(
        Guid eventId, [FromQuery] string? position, [FromQuery] PaginationRequest pagination, CancellationToken ct) =>
        Ok(await _recruiting.ListRecruitingPartiesAsync(eventId, ParsePosition(position), pagination, ct));

    // --- Caller's market context + listing ------------------------------------

    [HttpGet("events/{eventId:guid}/market/me")]
    public async Task<ActionResult<MyMarketDto>> MyMarket(Guid eventId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var dto = await _requests.GetMyMarketAsync(eventId, userId, ct);
        return dto is null ? NotFoundProblem("No such event.") : Ok(dto);
    }

    [HttpPost("events/{eventId:guid}/market/listing")]
    public async Task<ActionResult<MarketListingDto>> PostListing(Guid eventId, [FromBody] PostListingRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _listings.PostAsync(eventId, userId, request, ct);
        return result.IsOk
            ? Created($"/api/v1/events/{eventId}/market/listing", result.Value)
            : Fail(result.Outcome, result.Error);
    }

    [HttpPut("events/{eventId:guid}/market/listing")]
    public async Task<ActionResult<MarketListingDto>> EditListing(Guid eventId, [FromBody] PostListingRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _listings.EditAsync(eventId, userId, request, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpDelete("events/{eventId:guid}/market/listing")]
    public async Task<IActionResult> TakeDownListing(Guid eventId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _listings.TakeDownAsync(eventId, userId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    // --- Request actions ------------------------------------------------------

    [HttpPost("market/requests/{id:guid}/accept")]
    public async Task<ActionResult<MarketRequestDto>> Accept(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _requests.AcceptAsync(id, userId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPost("market/requests/{id:guid}/decline")]
    public async Task<ActionResult<MarketRequestDto>> Decline(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _requests.DeclineAsync(id, userId, ct);
        return result.IsOk ? Ok(result.Value) : Fail(result.Outcome, result.Error);
    }

    [HttpPost("market/requests/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _requests.RevokeAsync(id, userId, ct);
        return result.IsOk ? NoContent() : Fail(result.Outcome, result.Error);
    }

    // --- Dashboard ------------------------------------------------------------

    [HttpGet("market/mine")]
    public async Task<ActionResult<PagedResult<MyMarketRequestDto>>> Mine([FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _requests.ListMineAsync(userId, pagination, ct));
    }

    [HttpGet("market/mine/listings")]
    public async Task<ActionResult<PagedResult<MyListingDto>>> MyListings([FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _listings.ListMyListingsAsync(userId, pagination, ct));
    }

    // --- Helpers --------------------------------------------------------------

    private static Pompfe? ParsePosition(string? position) =>
        Enum.TryParse<Pompfe>(position, ignoreCase: true, out var p) ? p : null;

    private ObjectResult Fail(PartyOutcome outcome, string? detail) => outcome switch
    {
        PartyOutcome.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: detail ?? "Not found."),
        PartyOutcome.Forbidden => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail ?? "Not allowed."),
        PartyOutcome.NotTeamAdmin => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail ?? "Not allowed."),
        PartyOutcome.Invalid => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: detail),
        PartyOutcome.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail),
        PartyOutcome.Full => Problem(statusCode: StatusCodes.Status409Conflict, title: "Full", detail: detail),
        PartyOutcome.Closed => Problem(statusCode: StatusCodes.Status409Conflict, title: "Closed", detail: detail),
        _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Request failed", detail: detail),
    };

    private ObjectResult NotFoundProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: detail);

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
