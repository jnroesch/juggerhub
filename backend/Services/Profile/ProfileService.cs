using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Profile;
using JuggerHub.Entities;
using JuggerHub.Services.Events;
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
    private readonly ProfileOptions _options;

    public ProfileService(
        AppDbContext db,
        IEventActivityService activity,
        Recognition.IRecognitionDisplayService recognitions,
        IOptions<ProfileOptions> options)
    {
        _db = db;
        _activity = activity;
        _recognitions = recognitions;
        _options = options.Value;
    }

    public async Task<HandleAvailabilityDto> CheckHandleAsync(string rawHandle, CancellationToken ct = default)
    {
        var check = await ResolveHandleForRegistrationAsync(rawHandle, ct);
        return new HandleAvailabilityDto(
            rawHandle, check.Normalized, check.Status == HandleCheckStatus.Available, check.Reason);
    }

    public async Task<HandleCheck> ResolveHandleForRegistrationAsync(string rawHandle, CancellationToken ct = default)
    {
        // Validate the trimmed input as-typed: the lowercase-only format rules reject
        // uppercase/spaces/symbols outright (spec US1 AS-3) rather than silently
        // rewriting them. A valid handle is therefore already its canonical form.
        var candidate = (rawHandle ?? string.Empty).Trim();
        var rejection = HandlePolicy.Validate(candidate, _options.HandleMinLength, _options.HandleMaxLength);
        if (rejection != HandleRejection.None)
        {
            return new HandleCheck(HandleCheckStatus.Invalid, candidate,
                HandlePolicy.Describe(rejection, _options.HandleMinLength, _options.HandleMaxLength));
        }

        var taken = await _db.PlayerProfiles.AsNoTracking().AnyAsync(p => p.Handle == candidate, ct);
        return taken
            ? new HandleCheck(HandleCheckStatus.Taken, candidate, "That handle isn't available.")
            : new HandleCheck(HandleCheckStatus.Available, candidate, null);
    }

    public async Task<OwnerProfileDto?> GetOwnerAsync(Guid userId, CancellationToken ct = default)
    {
        var projection = await _db.PlayerProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new ProfileProjection(
                p.Id, p.UserId, p.Handle, p.DisplayName, p.Hometown, p.Description,
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
        return new OwnerProfileDto(projection.Handle, projection.DisplayName, projection.Hometown,
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
                p.Id, p.UserId, p.Handle, p.DisplayName, p.Hometown, p.Description,
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
        return new PublicProfileDto(projection.Handle, projection.DisplayName, projection.Hometown,
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
        var profile = await _db.PlayerProfiles
            .Include(p => p.Pompfen)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile is null)
        {
            return null;
        }

        profile.DisplayName = request.DisplayName.Trim();
        profile.Hometown = BlankToNull(request.Hometown);
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

    public async Task<AvatarSetResult> SetAvatarAsync(Guid userId, byte[] content, string? declaredContentType, CancellationToken ct = default)
    {
        if (content.Length == 0)
        {
            return AvatarSetResult.Fail(AvatarSetStatus.Empty, "No image was provided.");
        }

        if (content.Length > _options.MaxAvatarBytes)
        {
            return AvatarSetResult.Fail(AvatarSetStatus.TooLarge,
                $"Image is too large (max {_options.MaxAvatarBytes / (1024 * 1024)} MB).");
        }

        // Never trust the declared content type — sniff the magic bytes.
        var sniffed = SniffImageContentType(content);
        if (sniffed is null)
        {
            return AvatarSetResult.Fail(AvatarSetStatus.InvalidType, "Use a PNG, JPEG, or WebP image.");
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
        // clean INSERT for a new avatar and an UPDATE for an existing one.
        var avatar = await _db.ProfileAvatars.FirstOrDefaultAsync(a => a.ProfileId == profileId.Value, ct);
        if (avatar is null)
        {
            _db.ProfileAvatars.Add(new ProfileAvatar
            {
                ProfileId = profileId.Value,
                Bytes = content,
                ContentType = sniffed,
            });
        }
        else
        {
            avatar.Bytes = content;
            avatar.ContentType = sniffed;
        }

        await _db.SaveChangesAsync(ct);
        return AvatarSetResult.Ok();
    }

    public async Task<AvatarData?> GetAvatarAsync(string handle, Guid? viewerUserId, CancellationToken ct = default)
    {
        var normalized = HandlePolicy.Normalize(handle);
        var data = await _db.ProfileAvatars
            .AsNoTracking()
            .Where(a => a.Profile.Handle == normalized)
            .Select(a => new { a.Bytes, a.ContentType, a.Profile.IsPublic })
            .FirstOrDefaultAsync(ct);

        // Same visibility gate: a private profile's avatar is not served anonymously.
        return data is null || !IsVisibleTo(data.IsPublic, viewerUserId)
            ? null
            : new AvatarData(data.Bytes, data.ContentType);
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
            .Select(m => new ProfileTeamDto(m.Team.Slug, m.Team.Name, m.Team.Type, m.Team.City, m.Role))
            .ToListAsync(ct);
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Identify a supported image by its magic bytes (PNG/JPEG/WebP). Returns the
    /// canonical content type, or null if unrecognized. Declared types are ignored.
    /// </summary>
    private static string? SniffImageContentType(byte[] b)
    {
        if (b.Length >= 8 &&
            b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
        {
            return "image/png";
        }

        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // RIFF....WEBP
        if (b.Length >= 12 &&
            b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
            b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }

    private sealed record ProfileProjection(
        Guid Id,
        Guid UserId,
        string Handle,
        string DisplayName,
        string? Hometown,
        string? Description,
        bool HasAvatar,
        bool IsPublic,
        List<Pompfe> Pompfen);
}
