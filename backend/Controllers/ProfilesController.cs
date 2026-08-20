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
using JuggerHub.Services.Media;
using JuggerHub.Services.Profile;
using JuggerHub.Services.Search;
using JuggerHub.Services.Teams;
using JuggerHub.Security.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

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
    private readonly IProfileShowcaseService _showcase;
    private readonly IOptions<MediaStorageOptions> _mediaOptions;

    public ProfilesController(
        IProfileService profiles, IEventActivityService activity, IPlayerSearchService search, IHomeService home,
        ITeamInvitationService invitations, IProfileShowcaseService showcase,
        IOptions<MediaStorageOptions> mediaOptions)
    {
        _profiles = profiles;
        _activity = activity;
        _search = search;
        _home = home;
        _invitations = invitations;
        _showcase = showcase;
        _mediaOptions = mediaOptions;
    }

    // --- Browse (public) ------------------------------------------------------

    /// <summary>Player browse/search (feature 007; authenticated-only since feature 026). Returns
    /// every non-banned player matching the query (banned accounts are excluded globally; the
    /// per-player search opt-in was removed in feature 020). Public card fields only.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PlayerCardDto>>> Browse(
        [FromQuery] PlayerBrowseQuery query, [FromQuery] PaginationRequest pagination, CancellationToken ct)
    {
        // Authenticated-only since feature 026; resolve the caller up front so the auth check never
        // depends on user-supplied query values (a proximity request must not be the only path that
        // enforces it).
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        // Proximity sort (feature 030 pattern) anchors on the caller's OWN home city, resolved
        // server-side and never accepted from the query. Without one there is nothing to measure
        // from, so ask them to set it (409) rather than silently returning a different order.
        Guid? homeCityId = null;
        if (query.Sort == JuggerHub.Services.Search.PlayerSort.Proximity)
        {
            homeCityId = await _profiles.GetHomeCityIdAsync(userId, ct);
            if (homeCityId is null)
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "No home city",
                    detail: "Set your home city to sort players by distance.");
            }
        }

        return Ok(await _search.BrowseAsync(query, pagination, homeCityId, ct));
    }

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

    /// <summary>
    /// Set or clear ONLY the owner's home city (feature 030). Onboarding calls this the instant a
    /// city is picked so the team step can order by proximity (FR-013), without persisting the rest
    /// of the (still-unfinished) onboarding profile. Degrades exactly like the create/update paths:
    /// an unresolvable city id → 422.
    /// </summary>
    [HttpPut("me/home-city")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> SetHomeCity(
        [FromBody] JuggerHub.Dtos.Cities.LocationSelectionDto selection, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var updated = await _profiles.SetHomeCityAsync(userId, selection, ct);
            return updated ? NoContent() : NotFound();
        }
        catch (JuggerHub.Services.Geocoding.CityNotResolvableException)
        {
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "City not found",
                detail: "That city could not be found. Please pick another.");
        }
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
    [EnableRateLimiting(RateLimitPolicies.MediaRead)]
    public async Task<IActionResult> GetAvatar(string handle, CancellationToken ct)
    {
        // The service applies the visibility gate and the banned-account filter BEFORE it opens the
        // stored object, so reaching this line already means the caller is entitled to the bytes
        // (feature 035). Anonymous callers legitimately get here for a public profile — the gate is
        // "the platform decides per request", never "authenticated only".
        var avatar = await _profiles.GetAvatarAsync(handle, GetOptionalUserId(), ct);
        if (avatar is null)
        {
            // 404 rather than 403 for every refusal — not found, not permitted, and store-unavailable
            // are deliberately indistinguishable, so the endpoint never becomes an existence oracle.
            return NotFound();
        }

        return MediaResponse.File(
            this,
            new MediaContent(avatar.Value.Content, avatar.Value.ContentType, avatar.Value.ObjectKey),
            _mediaOptions.Value);
    }


    // --- Showcase gallery (feature 046 / #99) ---------------------------------

    /// <summary>
    /// A player's showcase gallery: at most five pictures, in the owner's order. Visibility-gated in
    /// the service exactly like the avatar — any signed-in caller, or an anonymous one when the owner
    /// opted the profile public. No pagination: the collection is capped at five, so a
    /// <c>PagedResult</c> envelope would advertise paging that cannot exist (see the plan's
    /// Complexity Tracking).
    /// </summary>
    [HttpGet("{handle}/showcase")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ShowcaseImageDto>>> GetShowcase(string handle, CancellationToken ct)
    {
        var images = await _showcase.ListAsync(handle, GetOptionalUserId(), ct);
        return images is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found",
                detail: "No profile exists for that handle.")
            : Ok(images);
    }

    /// <summary>The bytes of one showcase picture.</summary>
    [HttpGet("{handle}/showcase/{imageId:guid}/image")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.MediaRead)]
    public async Task<IActionResult> GetShowcaseImage(string handle, Guid imageId, CancellationToken ct)
    {
        // The service applies the visibility gate and the banned-account filter BEFORE it opens the
        // stored object, so reaching this line already means the caller is entitled to the bytes.
        var image = await _showcase.GetImageAsync(handle, imageId, GetOptionalUserId(), ct);
        if (image is null)
        {
            // 404 for every refusal — not found, not permitted, and store-unavailable are
            // deliberately indistinguishable, so the endpoint never becomes an existence oracle.
            return NotFound();
        }

        return MediaResponse.File(this, image.Value, _mediaOptions.Value);
    }

    /// <summary>Add a picture to the caller's own showcase. Owner-only: acts on the authenticated
    /// subject alone.</summary>
    [HttpPost("me/showcase")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequestSizeLimit(8 * 1024 * 1024)]
    [EnableRateLimiting(RateLimitPolicies.MediaUpload)]
    public async Task<ActionResult<ShowcaseImageDto>> AddShowcaseImage(IFormFile file, CancellationToken ct)
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
        var result = await _showcase.AddAsync(userId, ms.ToArray(), ct);

        return result.Status switch
        {
            ShowcaseAddStatus.Success => CreatedAtAction(
                nameof(GetShowcase),
                new { handle = await _profiles.GetHandleAsync(userId, ct) },
                new ShowcaseImageDto(result.Id, null, result.Position)),
            ShowcaseAddStatus.OwnerNotFound => NotFound(),
            // Distinct from a processing failure so the client can say "you already have five"
            // rather than "invalid image" (spec FR-016).
            ShowcaseAddStatus.GalleryFull => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Gallery full",
                detail: $"A showcase holds at most {ShowcaseWriter.MaxImagesPerOwner} pictures. Remove one to add another."),
            ShowcaseAddStatus.StoreUnavailable => Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Could not store the picture",
                detail: "We could not store that picture just now. Please try again."),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid image",
                detail: result.Reason ?? "That picture could not be used."),
        };
    }

    /// <summary>Set or clear the caption on one of the caller's own showcase pictures.</summary>
    [HttpPatch("me/showcase/{imageId:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> SetShowcaseCaption(
        Guid imageId, [FromBody] UpdateShowcaseCaptionRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _showcase.SetCaptionAsync(userId, imageId, request.Caption, ct);
        return MapShowcaseMutation(status);
    }

    /// <summary>Remove one of the caller's own showcase pictures.</summary>
    [HttpDelete("me/showcase/{imageId:guid}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> RemoveShowcaseImage(Guid imageId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _showcase.RemoveAsync(userId, imageId, ct);
        return MapShowcaseMutation(status);
    }

    /// <summary>Apply a complete new order to the caller's own showcase.</summary>
    [HttpPut("me/showcase/order")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> ReorderShowcase(
        [FromBody] ReorderShowcaseRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var status = await _showcase.ReorderAsync(userId, request.ImageIds ?? [], ct);
        return MapShowcaseMutation(status);
    }

    /// <summary>Shared status mapping for the three showcase mutations.</summary>
    private IActionResult MapShowcaseMutation(ShowcaseMutateStatus status) => status switch
    {
        ShowcaseMutateStatus.Success => NoContent(),
        ShowcaseMutateStatus.CaptionTooLong => Problem(statusCode: StatusCodes.Status400BadRequest,
            title: "Caption too long",
            detail: $"A caption is at most {ProfileShowcaseService.MaxCaptionLength} characters."),
        // The caller's view is out of date — typically a picture was removed while their page was
        // open. Nothing was written; the client reloads and tries again.
        ShowcaseMutateStatus.StaleOrder => Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Gallery changed",
            detail: "That gallery changed while you were editing it. Reload and try again."),
        // NotFound also covers "belongs to someone else" — deliberately indistinguishable.
        _ => NotFound(),
    };

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
