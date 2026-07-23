using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using JuggerHub.Common;
using JuggerHub.Dtos.Home;
using JuggerHub.Dtos.Profile;
using JuggerHub.Dtos.Search;
using JuggerHub.Dtos.Teams;
using JuggerHub.Services.Events;
using JuggerHub.Services.Home;
using JuggerHub.Services.Profile;
using JuggerHub.Services.Search;
using JuggerHub.Services.Teams;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JuggerHub.Controllers;

/// <summary>
/// Player-profile endpoints. The controller requires authentication by default (feature 026);
/// owner routes (<c>/me*</c>) act ONLY on the authenticated subject (never a client-supplied id).
/// The public-profile routes (<c>/{handle}*</c>) are the sole <see cref="AllowAnonymousAttribute"/>
/// exception: they are visibility-gated in the service so an anonymous caller sees a profile only
/// when its owner opted it public (else the same 404 as a missing handle — no existence oracle),
/// while an authenticated caller may view any profile. They return DTOs that carry no
/// email/account/security data (constitution Principle I; SC-002).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/profiles")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class ProfilesController : ControllerBase
{
    private readonly IProfileService _profiles;
    private readonly IEventActivityService _activity;
    private readonly IPlayerSearchService _search;
    private readonly IHomeService _home;
    private readonly ITeamInvitationService _invitations;

    public ProfilesController(
        IProfileService profiles, IEventActivityService activity, IPlayerSearchService search, IHomeService home,
        ITeamInvitationService invitations)
    {
        _profiles = profiles;
        _activity = activity;
        _search = search;
        _home = home;
        _invitations = invitations;
    }

    // --- Browse (public) ------------------------------------------------------

    /// <summary>Player browse/search (feature 007; authenticated-only since feature 026). Returns
    /// every non-banned player matching the query (banned accounts are excluded globally; the
    /// per-player search opt-in was removed in feature 020). Public card fields only.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PlayerCardDto>>> Browse(
        [FromQuery] PlayerBrowseQuery query, [FromQuery] PaginationRequest pagination, CancellationToken ct) =>
        Ok(await _search.BrowseAsync(query, pagination, ct));

    // --- Owner (authenticated) -------------------------------------------------

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<OwnerProfileDto>> GetMine(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var profile = await _profiles.GetOwnerAsync(userId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<OwnerProfileDto>> UpdateMine([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var updated = await _profiles.UpdateAsync(userId, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("me/avatar")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "No image",
                detail: "No image was provided.");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var result = await _profiles.SetAvatarAsync(userId, ms.ToArray(), file.ContentType, ct);

        return result.Status switch
        {
            AvatarSetStatus.Success => NoContent(),
            AvatarSetStatus.ProfileNotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid image",
                detail: result.Reason),
        };
    }

    /// <summary>The caller's team memberships — drives the nav "My team" target + Home snapshots
    /// (feature 008). Owner-only: acts on the authenticated subject alone. Paginated.</summary>
    [HttpGet("me/teams")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<PagedResult<MyTeamDto>>> GetMyTeams(
        [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _home.ListMyTeamsAsync(userId, pagination, ct));
    }

    /// <summary>The caller's usable targeted invitations — powers the "My team" home for teamless
    /// players (feature 023). Owner-only: acts on the authenticated subject alone; a player can only
    /// see invitations addressed to them. Paginated.</summary>
    [HttpGet("me/invitations")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<PagedResult<MyInvitationDto>>> GetMyInvitations(
        [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _invitations.ListMineAsync(userId, pagination, ct));
    }

    [HttpPost("me/onboarding/complete")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        // Idempotent + owner-only: acts on the authenticated subject alone. Called on
        // ANY terminal exit of the flow (finish or dismiss) — see specs/004-onboarding.
        var status = await _profiles.CompleteOnboardingAsync(userId, ct);
        return status == CompleteOnboardingStatus.Completed ? NoContent() : NotFound();
    }

    // --- Public (anonymous) ----------------------------------------------------

    [HttpGet("{handle}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicProfileDto>> GetPublic(string handle, CancellationToken ct)
    {
        var profile = await _profiles.GetPublicAsync(handle, GetOptionalUserId(), ct);
        return profile is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found",
                detail: "No profile exists for that handle.")
            : Ok(profile);
    }

    [HttpGet("{handle}/avatar")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvatar(string handle, CancellationToken ct)
    {
        var avatar = await _profiles.GetAvatarAsync(handle, GetOptionalUserId(), ct);
        return avatar is null ? NotFound() : File(avatar.Value.Bytes, avatar.Value.ContentType);
    }

    [HttpGet("{handle}/activity")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ActivityItemDto>>> GetActivity(
        string handle, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        var profileId = await _profiles.GetProfileIdAsync(handle, GetOptionalUserId(), ct);
        if (profileId is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found",
                detail: "No profile exists for that handle.");
        }

        var page = await _activity.GetRecentAsync(profileId.Value, pagination, ct);
        return Ok(page);
    }

    // --- Helpers ---------------------------------------------------------------

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }

    /// <summary>The caller's id when a valid auth cookie is present; null for an anonymous caller.
    /// Used by the public-profile reads to apply the visibility gate (feature 026): an anonymous
    /// caller sees only public profiles; an authenticated caller sees any.</summary>
    private Guid? GetOptionalUserId() => TryGetUserId(out var userId) ? userId : null;
}
