using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Cities;
using JuggerHub.Dtos.Profile;
using JuggerHub.Entities;
using JuggerHub.Services.Events;
using JuggerHub.Services.Geocoding;
using JuggerHub.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Profile;

/// <inheritdoc />
public sealed class ProfileService : IProfileService
{
    // How many recent-activity items to embed inline in a profile payload.
    private const int EmbedActivityCap = 4;

    private readonly AppDbContext _db;
    private readonly IEventActivityService _activity;
    private readonly Recognition.IRecognitionDisplayService _recognitions;
    private readonly ICityService _cities;
    private readonly ProfileOptions _options;
    private readonly IImageProcessor _imageProcessor;
    private readonly ImageProcessingOptions _imageOptions;
    private readonly IMediaStore _mediaStore;

    public ProfileService(
        AppDbContext db,
        IEventActivityService activity,
        Recognition.IRecognitionDisplayService recognitions,
        ICityService cities,
        IOptions<ProfileOptions> options,
        IImageProcessor imageProcessor,
        IOptions<ImageProcessingOptions> imageOptions,
        IMediaStore mediaStore)
    {
        _db = db;
        _activity = activity;
        _recognitions = recognitions;
        _cities = cities;
        _options = options.Value;
        _imageProcessor = imageProcessor;
        _imageOptions = imageOptions.Value;
        _mediaStore = mediaStore;
    }

    public async Task<HandleAvailabilityDto> CheckHandleAsync(string rawHandle, CancellationToken ct = default)
    {
        var check = await ResolveHandleForRegistrationAsync(rawHandle, ct);
        return new HandleAvailabilityDto(
            rawHandle,
            check.Normalized,
            check.Status == HandleCheckStatus.Available,
            check.Reason == HandleRejection.None ? null : check.Reason);
    }

    public async Task<HandleCheck> ResolveHandleForRegistrationAsync(string rawHandle, CancellationToken ct = default)
    {
        // Case is FOLDED, not refused. `Nik-Berlin` and `nik-berlin` are not two different
        // handles — the second is what the first means — so refusing the capital teaches the
        // rule only by breaking it, and the uniqueness check below would otherwise treat the
        // two as distinct. Normalize is the same function every read path already resolves a
        // handle through, so what is checked here is exactly what would be stored and served.
        // Everything the format genuinely disallows — spaces, accents, punctuation — still
        // fails Validate, which is what a pasted display name trips on.
        var candidate = HandlePolicy.Normalize(rawHandle);
        var rejection = HandlePolicy.Validate(candidate, _options.HandleMinLength, _options.HandleMaxLength);
        if (rejection != HandleRejection.None)
        {
            return new HandleCheck(HandleCheckStatus.Invalid, candidate, rejection,
                HandlePolicy.Describe(rejection, _options.HandleMinLength, _options.HandleMaxLength));
        }

        var taken = await _db.PlayerProfiles.AsNoTracking().AnyAsync(p => p.Handle == candidate, ct);
        return taken
            ? new HandleCheck(HandleCheckStatus.Taken, candidate, HandleRejection.Taken,
                HandlePolicy.Describe(HandleRejection.Taken, _options.HandleMinLength, _options.HandleMaxLength))
            : new HandleCheck(HandleCheckStatus.Available, candidate, HandleRejection.None, null);
    }

    public async Task<OwnerProfileDto?> GetOwnerAsync(Guid userId, CancellationToken ct = default)
    {
        var projection = await _db.PlayerProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new ProfileProjection(
                p.Id, p.UserId, p.Handle, p.DisplayName,
                p.HomeCity == null
                    ? null
                    : new LocationDto(
                        p.HomeCity.ExternalId, p.HomeCity.Name, p.HomeCity.Region, p.HomeCity.CountryName, p.HomeCity.CountryCode,
                        p.HomeCity.Name + ", " + p.HomeCity.CountryName),
                p.Description,
                p.Avatar != null, p.IsPublic,
                p.Pompfen.OrderBy(pp => pp.Pompfe).Select(pp => pp.Pompfe).ToList()))
            .FirstOrDefaultAsync(ct);

        if (projection is null)
        {
            return null;
        }

        var activity = await _activity.GetRecentCappedAsync(projection.Id, EmbedActivityCap, ct);
        var teams = await GetTeamsAsync(projection.UserId, ct);
        var recognitions = await _recognitions.ForPlayerAsync(projection.Id, ct);
        return new OwnerProfileDto(projection.Handle, projection.DisplayName, projection.Location,
            projection.Description, projection.HasAvatar, projection.Pompfen, activity, teams,
            recognitions.Badges, recognitions.Achievements, projection.IsPublic);
    }

    public async Task<bool> HasCompletedOnboardingAsync(Guid userId, CancellationToken ct = default)
    {
        // Projected boolean read — no entity tracked, only the one column considered.
        // No profile row → false (treated as not yet onboarded).
        return await _db.PlayerProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.OnboardingCompletedAt != null)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetHandleAsync(Guid userId, CancellationToken ct = default) =>
        await _db.PlayerProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Handle)
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> GetHomeCityIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.PlayerProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.HomeCityId)
            .FirstOrDefaultAsync(ct);

    public async Task<CompleteOnboardingStatus> CompleteOnboardingAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            return CompleteOnboardingStatus.ProfileNotFound;
        }

        // Idempotent: set the timestamp only if unset, so the first completion stands
        // and repeat calls are no-ops. The AuditFieldsInterceptor updates ModifiedDate.
        if (profile.OnboardingCompletedAt is null)
        {
            profile.OnboardingCompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return CompleteOnboardingStatus.Completed;
    }

    public async Task<PublicProfileDto?> GetPublicAsync(string handle, Guid? viewerUserId, CancellationToken ct = default)
    {
        var normalized = HandlePolicy.Normalize(handle);

        // Explicit projection: sensitive columns (email/account/security) are never
        // even loaded — the public caller physically cannot receive them (SC-002).
        var projection = await _db.PlayerProfiles
            .AsNoTracking()
            .Where(p => p.Handle == normalized)
            .Select(p => new ProfileProjection(
                p.Id, p.UserId, p.Handle, p.DisplayName,
                p.HomeCity == null
                    ? null
                    : new LocationDto(
                        p.HomeCity.ExternalId, p.HomeCity.Name, p.HomeCity.Region, p.HomeCity.CountryName, p.HomeCity.CountryCode,
                        p.HomeCity.Name + ", " + p.HomeCity.CountryName),
                p.Description,
                p.Avatar != null, p.IsPublic,
                p.Pompfen.OrderBy(pp => pp.Pompfe).Select(pp => pp.Pompfe).ToList()))
            .FirstOrDefaultAsync(ct);

        // Visibility gate (feature 026): a private profile is invisible to anonymous callers —
        // returning the SAME null as a missing handle so the two are indistinguishable (no oracle).
        if (projection is null || !IsVisibleTo(projection.IsPublic, viewerUserId))
        {
            return null;
        }

        var activity = await _activity.GetRecentCappedAsync(projection.Id, EmbedActivityCap, ct);
        var teams = await GetTeamsAsync(projection.UserId, ct);
        var recognitions = await _recognitions.ForPlayerAsync(projection.Id, ct);
        return new PublicProfileDto(projection.Handle, projection.DisplayName, projection.Location,
            projection.Description, projection.HasAvatar, projection.Pompfen, activity, teams,
            recognitions.Badges, recognitions.Achievements);
    }

    public async Task<Guid?> GetProfileIdAsync(string handle, Guid? viewerUserId, CancellationToken ct = default)
    {
        var normalized = HandlePolicy.Normalize(handle);
        var row = await _db.PlayerProfiles
            .AsNoTracking()
            .Where(p => p.Handle == normalized)
            .Select(p => new { p.Id, p.IsPublic })
            .FirstOrDefaultAsync(ct);

        // Same visibility gate as GetPublicAsync: a private profile is invisible to anonymous
        // callers, so its activity page 404s exactly like a missing handle.
        return row is null || !IsVisibleTo(row.IsPublic, viewerUserId) ? null : row.Id;
    }

    public async Task<OwnerProfileDto?> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        // Resolve the selected city BEFORE mutating the profile: ResolveAndUpsertAsync owns its own
        // SaveChanges (first use of a city inserts it), which would commit half-applied changes to
        // anything already modified here. When Location is omitted (null) the city is left unchanged;
        // an explicit null CityExternalId clears it (feature 030 contract).
        var changeCity = request.Location is not null;
        Guid? resolvedCityId = null;
        if (changeCity && !string.IsNullOrWhiteSpace(request.Location!.CityExternalId))
        {
            var city = await _cities.ResolveAndUpsertAsync(request.Location.CityExternalId!, request.Location.Name, ct);
            resolvedCityId = city.Id;
        }

        var profile = await _db.PlayerProfiles
            .Include(p => p.Pompfen)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile is null)
        {
            return null;
        }

        profile.DisplayName = request.DisplayName.Trim();
        if (changeCity)
        {
            profile.HomeCityId = resolvedCityId;
        }

        profile.Description = BlankToNull(request.Description);
        // Owner-controlled anonymous visibility (feature 026). Acts only on the caller's own
        // profile (resolved by userId), so a player can never change another's visibility.
        profile.IsPublic = request.IsPublic;

        // Replace the selection set with the requested one (distinct). Operate on the
        // DbSet directly (not the navigation collection): a new ProfilePompfe carries a
        // client-generated GUID key, and adding it via the collection makes EF's change
        // detector treat it as Modified (→ UPDATE of a nonexistent row). DbSet.Add marks
        // it Added explicitly, matching the RefreshToken pattern.
        var desired = (request.Pompfen ?? []).Distinct().ToHashSet();
        var current = profile.Pompfen.ToList();

        var toRemove = current.Where(pp => !desired.Contains(pp.Pompfe)).ToList();
        if (toRemove.Count > 0)
        {
            _db.ProfilePompfen.RemoveRange(toRemove);
        }

        var currentValues = current.Select(pp => pp.Pompfe).ToHashSet();
        var toAdd = desired
            .Where(p => !currentValues.Contains(p))
            .Select(p => new ProfilePompfe { ProfileId = profile.Id, Pompfe = p })
            .ToList();
        if (toAdd.Count > 0)
        {
            _db.ProfilePompfen.AddRange(toAdd);
        }

        await _db.SaveChangesAsync(ct);
        return await GetOwnerAsync(userId, ct);
    }

    public async Task<bool> SetHomeCityAsync(Guid userId, LocationSelectionDto selection, CancellationToken ct = default)
    {
        // Resolve BEFORE mutating the profile — ResolveAndUpsertAsync owns its own SaveChanges, so
        // anything already modified here would be committed by it rather than by this method.
        Guid? resolvedCityId = null;
        if (!string.IsNullOrWhiteSpace(selection.CityExternalId))
        {
            var city = await _cities.ResolveAndUpsertAsync(selection.CityExternalId!, selection.Name, ct);
            resolvedCityId = city.Id;
        }

        var profile = await _db.PlayerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            return false;
        }

        profile.HomeCityId = resolvedCityId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AvatarSetResult> SetAvatarAsync(Guid userId, byte[] content, string? declaredContentType, CancellationToken ct = default)
    {
        // Normalize server-side (feature 034 / #98): validate + decompression-bomb guard + strip
        // metadata + resize + re-encode to WebP. The declared content type is never trusted.
        // On any rejection we return without touching the stored avatar (FR-009).
        var processed = _imageProcessor.Process(content, _imageOptions.Avatar);
        if (processed.Status != ImageProcessingStatus.Success)
        {
            return AvatarSetResult.Fail(MapProcessingStatus(processed.Status), processed.Reason!);
        }

        var profileId = await _db.PlayerProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (profileId is null)
        {
            return AvatarSetResult.Fail(AvatarSetStatus.ProfileNotFound, "Profile not found.");
        }

        // Operate on the DbSet directly (not via the 1:1 navigation) so EF issues a
        // clean INSERT for a new avatar and an UPDATE for an existing one. Only the normalized
        // WebP bytes are stored; the original upload is discarded (FR-015).
        var avatar = await _db.ProfileAvatars.FirstOrDefaultAsync(a => a.ProfileId == profileId.Value, ct);

        // Ordering matters (feature 035): mint the key, write the object, commit the descriptor,
        // and only then delete the object this one replaces. A row and a blob cannot share a
        // transaction, so some failure window is unavoidable — this ordering picks the harmless
        // one. Failing after the write leaves an unreferenced object the sweep reclaims; the
        // alternative, deleting first, would leave a member with no picture at all.
        var objectKey = MediaObjectKey.Create(MediaKind.Avatar);
        var supersededKey = avatar?.ObjectKey;

        using (var normalized = new MemoryStream(processed.Bytes!))
        {
            await _mediaStore.PutAsync(objectKey, normalized, processed.ContentType!, ct);
        }

        if (avatar is null)
        {
            _db.ProfileAvatars.Add(new ProfileAvatar
            {
                ProfileId = profileId.Value,
                ObjectKey = objectKey,
                SizeBytes = processed.Bytes!.Length,
                ContentType = processed.ContentType!,
            });
        }
        else
        {
            avatar.ObjectKey = objectKey;
            avatar.SizeBytes = processed.Bytes!.Length;
            avatar.ContentType = processed.ContentType!;
        }

        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(supersededKey))
        {
            await _mediaStore.DeleteAsync(supersededKey, ct);
        }

        return AvatarSetResult.Ok();
    }

    /// <summary>Map a processing failure to the avatar-upload status (distinct rejection reasons, FR-003).</summary>
    private static AvatarSetStatus MapProcessingStatus(ImageProcessingStatus status) => status switch
    {
        ImageProcessingStatus.Empty => AvatarSetStatus.Empty,
        ImageProcessingStatus.UnsupportedType => AvatarSetStatus.InvalidType,
        ImageProcessingStatus.InputTooLarge => AvatarSetStatus.TooLarge,
        ImageProcessingStatus.OutputTooLarge => AvatarSetStatus.TooLarge,
        ImageProcessingStatus.DimensionsTooLarge => AvatarSetStatus.DimensionsTooLarge,
        ImageProcessingStatus.Unreadable => AvatarSetStatus.Unreadable,
        _ => AvatarSetStatus.InvalidType,
    };

    public async Task<AvatarData?> GetAvatarAsync(string handle, Guid? viewerUserId, CancellationToken ct = default)
    {
        var normalized = HandlePolicy.Normalize(handle);

        // Step 1 — read the DESCRIPTOR only. The banned-account query filter on ProfileAvatar
        // applies here automatically, so a banned owner yields nothing before any decision is made.
        var data = await _db.ProfileAvatars
            .AsNoTracking()
            .Where(a => a.Profile.Handle == normalized)
            .Select(a => new { a.ObjectKey, a.ContentType, a.Profile.IsPublic })
            .FirstOrDefaultAsync(ct);

        // Step 2 — apply the visibility gate: a private profile's avatar is not served anonymously.
        if (data is null || !IsVisibleTo(data.IsPublic, viewerUserId))
        {
            return null;
        }

        // Step 3 — and ONLY now touch the media store. This ordering is the security contract
        // (feature 035): authorization is decided against relational data before any byte is
        // fetched, so the store never becomes the place where visibility is determined.
        var content = await _mediaStore.OpenReadAsync(data.ObjectKey, ct);

        // A descriptor whose object is missing degrades to the ordinary "no picture" outcome
        // rather than an error — the frontend already renders a placeholder for that.
        return content is null ? null : new AvatarData(content, data.ContentType, data.ObjectKey);
    }

    /// <summary>
    /// Visibility rule (feature 026): an authenticated caller (non-null viewer) may view any
    /// profile; an anonymous caller may view a profile only when its owner opted it public.
    /// </summary>
    private static bool IsVisibleTo(bool isPublic, Guid? viewerUserId) => isPublic || viewerUserId is not null;

    private async Task<IReadOnlyList<ProfileTeamDto>> GetTeamsAsync(Guid userId, CancellationToken ct)
    {
        // Teams the player belongs to (feature 005) — shown on both the owner and public profile.
        return await _db.TeamMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Team.Name)
            .Select(m => new ProfileTeamDto(
                m.Team.Slug, m.Team.Name, m.Team.Type,
                m.Team.City == null
                    ? null
                    : new LocationDto(
                        m.Team.City.ExternalId, m.Team.City.Name, m.Team.City.Region, m.Team.City.CountryName, m.Team.City.CountryCode,
                        m.Team.City.Name + ", " + m.Team.City.CountryName),
                m.Role))
            .ToListAsync(ct);
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ProfileProjection(
        Guid Id,
        Guid UserId,
        string Handle,
        string DisplayName,
        LocationDto? Location,
        string? Description,
        bool HasAvatar,
        bool IsPublic,
        List<Pompfe> Pompfen);
}
