using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Profile;
using JuggerHub.Entities;
using JuggerHub.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Profile;

/// <inheritdoc />
public sealed class ProfileShowcaseService : IProfileShowcaseService
{
    /// <summary>Caption ceiling (spec FR-005), matching the column width.</summary>
    public const int MaxCaptionLength = 120;

    private readonly AppDbContext _db;
    private readonly ShowcaseWriter _writer;
    private readonly IImageProcessor _imageProcessor;
    private readonly ImageProcessingOptions _imageOptions;
    private readonly IMediaStore _mediaStore;
    private readonly ILogger<ProfileShowcaseService> _logger;

    public ProfileShowcaseService(
        AppDbContext db,
        ShowcaseWriter writer,
        IImageProcessor imageProcessor,
        IOptions<ImageProcessingOptions> imageOptions,
        IMediaStore mediaStore,
        ILogger<ProfileShowcaseService> logger)
    {
        _db = db;
        _writer = writer;
        _imageProcessor = imageProcessor;
        _imageOptions = imageOptions.Value;
        _mediaStore = mediaStore;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ShowcaseImageDto>?> ListAsync(
        string handle, Guid? viewerUserId, CancellationToken ct = default)
    {
        var normalized = HandlePolicy.Normalize(handle);

        // One query for the gate and the gallery. The banned-account filter on
        // ProfileShowcaseImages applies automatically, so a banned owner yields nothing before any
        // decision is made; IsPublic then answers the feature-026 question for anonymous callers.
        var profile = await _db.PlayerProfiles
            .AsNoTracking()
            .Where(p => p.Handle == normalized)
            .Select(p => new
            {
                p.IsPublic,
                Images = p.ShowcaseImages
                    .OrderBy(g => g.Position)
                    .ThenBy(g => g.Id)
                    .Select(g => new ShowcaseImageDto(g.Id, g.Caption, g.Position))
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (profile is null || !IsVisibleTo(profile.IsPublic, viewerUserId))
        {
            return null;
        }

        return profile.Images;
    }

    public async Task<MediaContent?> GetImageAsync(
        string handle, Guid imageId, Guid? viewerUserId, CancellationToken ct = default)
    {
        var normalized = HandlePolicy.Normalize(handle);

        // Step 1 — read the DESCRIPTOR only. The ban filter applies here automatically.
        var data = await _db.ProfileShowcaseImages
            .AsNoTracking()
            .Where(g => g.Id == imageId && g.Profile.Handle == normalized)
            .Select(g => new { g.ObjectKey, g.ContentType, g.Profile.IsPublic })
            .FirstOrDefaultAsync(ct);

        // Step 2 — apply the visibility gate.
        if (data is null || !IsVisibleTo(data.IsPublic, viewerUserId))
        {
            return null;
        }

        // Step 3 — and ONLY now touch the media store. Authorization is decided against relational
        // data before any byte is fetched (feature 035), so the store never becomes the place where
        // visibility is determined.
        var content = await _mediaStore.OpenReadAsync(data.ObjectKey, ct);

        // A descriptor whose object has vanished degrades to the ordinary "no picture" outcome
        // rather than an error, and does not stop the rest of the gallery rendering (spec FR-024).
        return content is null ? null : new MediaContent(content, data.ContentType, data.ObjectKey);
    }

    public async Task<ShowcaseAddResult> AddAsync(Guid userId, byte[] content, CancellationToken ct = default)
    {
        // Normalize server-side (feature 034 / #98) BEFORE anything is stored or counted: validate,
        // pixel guard, strip metadata, resize to fit, re-encode to WebP. The showcase profile fits
        // rather than square-crops — cropping would cut the subject out of the picture (FR-014).
        var processed = _imageProcessor.Process(content, _imageOptions.Showcase);
        if (processed.Status != ImageProcessingStatus.Success)
        {
            return ShowcaseAddResult.Fail(MapProcessingStatus(processed.Status), processed.Reason);
        }

        var profileId = await _db.PlayerProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (profileId is null)
        {
            return ShowcaseAddResult.Fail(ShowcaseAddStatus.OwnerNotFound);
        }

        // Cheap pre-check so the overwhelmingly common "already full" case never writes an object it
        // would immediately have to delete. It is NOT the guarantee — the locked re-count inside
        // ShowcaseWriter is (see its remarks on the TOCTOU race).
        var existing = await _db.ProfileShowcaseImages.CountAsync(g => g.ProfileId == profileId.Value, ct);
        if (existing >= ShowcaseWriter.MaxImagesPerOwner)
        {
            return ShowcaseAddResult.Fail(ShowcaseAddStatus.GalleryFull);
        }

        // Ordering (feature 035): mint the key, write the object, then commit the descriptor. A row
        // and a blob cannot share a transaction, so some failure window is unavoidable; this
        // ordering picks the harmless one — a descriptor never points at bytes that are not there.
        var objectKey = MediaObjectKey.Create(MediaKind.ProfileShowcase);

        try
        {
            using var normalized = new MemoryStream(processed.Bytes!);
            await _mediaStore.PutAsync(objectKey, normalized, processed.ContentType!, ct);
        }
        catch (Exception ex)
        {
            // Never surfaced beyond "we could not store that" (Principle I). No row exists, so the
            // gallery is exactly as it was and no slot was consumed (FR-015).
            _logger.LogError(ex, "Showcase image could not be stored for profile {ProfileId}", profileId.Value);
            return ShowcaseAddResult.Fail(ShowcaseAddStatus.StoreUnavailable);
        }

        var result = await _writer.AddAsync<ProfileShowcaseImage>(
            ShowcaseOwner.Profile,
            profileId.Value,
            g => g.ProfileId == profileId.Value,
            position => new ProfileShowcaseImage
            {
                ProfileId = profileId.Value,
                Position = position,
                ObjectKey = objectKey,
                ContentType = processed.ContentType!,
                SizeBytes = processed.Bytes!.Length,
            },
            ct);

        if (result.Status != ShowcaseAddStatus.Success)
        {
            // The cap was reached between the pre-check and the lock. Take the object back out
            // rather than leaving litter for the sweep on a path we can see coming.
            await TryDeleteObjectAsync(objectKey, ct);
        }

        return result;
    }

    public async Task<ShowcaseMutateStatus> SetCaptionAsync(
        Guid userId, Guid imageId, string? caption, CancellationToken ct = default)
    {
        var trimmed = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (trimmed is not null && trimmed.Length > MaxCaptionLength)
        {
            return ShowcaseMutateStatus.CaptionTooLong;
        }

        // Single-column update on a row the WHERE clause proves belongs to the caller. No lock
        // needed: a caption change cannot affect the cap or the ordering.
        var updated = await _db.ProfileShowcaseImages
            .Where(g => g.Id == imageId && g.Profile.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.Caption, trimmed)
                // ExecuteUpdate bypasses the change tracker, so the audit interceptor does not run
                // (constitution Principle III).
                .SetProperty(g => g.ModifiedDate, DateTime.UtcNow), ct);

        return updated == 0 ? ShowcaseMutateStatus.NotFound : ShowcaseMutateStatus.Success;
    }

    public async Task<ShowcaseMutateStatus> RemoveAsync(Guid userId, Guid imageId, CancellationToken ct = default)
    {
        var profileId = await _db.PlayerProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (profileId is null)
        {
            return ShowcaseMutateStatus.NotFound;
        }

        var result = await _writer.RemoveAsync<ProfileShowcaseImage>(
            ShowcaseOwner.Profile,
            profileId.Value,
            imageId,
            g => g.ProfileId == profileId.Value,
            ct);

        if (result.Status == ShowcaseMutateStatus.Success)
        {
            // After the row is committed, never before: a blob delete cannot be rolled back, so
            // deleting first would destroy a picture for a removal that then failed.
            await TryDeleteObjectAsync(result.ObjectKey!, ct);
        }

        return result.Status;
    }

    public async Task<ShowcaseMutateStatus> ReorderAsync(
        Guid userId, IReadOnlyList<Guid> imageIds, CancellationToken ct = default)
    {
        var profileId = await _db.PlayerProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (profileId is null)
        {
            return ShowcaseMutateStatus.NotFound;
        }

        return await _writer.ReorderAsync<ProfileShowcaseImage>(
            ShowcaseOwner.Profile,
            profileId.Value,
            imageIds,
            g => g.ProfileId == profileId.Value,
            ct);
    }

    /// <summary>
    /// Visibility rule (feature 026), identical to the avatar's: an authenticated caller may view any
    /// profile; an anonymous caller may view one only when its owner opted it public.
    /// </summary>
    private static bool IsVisibleTo(bool isPublic, Guid? viewerUserId) => isPublic || viewerUserId is not null;

    /// <summary>
    /// Delete a stored object without letting a storage failure fail the caller's request. What is
    /// left behind is an unreferenced object the reconciliation sweep reclaims — logged, because
    /// somebody should know it happened.
    /// </summary>
    private async Task TryDeleteObjectAsync(string objectKey, CancellationToken ct)
    {
        try
        {
            await _mediaStore.DeleteAsync(objectKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Showcase object could not be deleted; left for reconciliation");
        }
    }

    /// <summary>Map a processing failure to an add status, keeping the reasons distinguishable (FR-016).</summary>
    private static ShowcaseAddStatus MapProcessingStatus(ImageProcessingStatus status) => status switch
    {
        ImageProcessingStatus.Empty => ShowcaseAddStatus.Empty,
        ImageProcessingStatus.UnsupportedType => ShowcaseAddStatus.InvalidType,
        ImageProcessingStatus.InputTooLarge => ShowcaseAddStatus.TooLarge,
        ImageProcessingStatus.OutputTooLarge => ShowcaseAddStatus.TooLarge,
        ImageProcessingStatus.DimensionsTooLarge => ShowcaseAddStatus.TooManyPixels,
        _ => ShowcaseAddStatus.Unreadable,
    };
}
