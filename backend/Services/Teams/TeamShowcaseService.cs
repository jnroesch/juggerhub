using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Profile;
using JuggerHub.Entities;
using JuggerHub.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Teams;

/// <inheritdoc />
public sealed class TeamShowcaseService : ITeamShowcaseService
{
    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;
    private readonly ShowcaseWriter _writer;
    private readonly IImageProcessor _imageProcessor;
    private readonly ImageProcessingOptions _imageOptions;
    private readonly IMediaStore _mediaStore;
    private readonly ILogger<TeamShowcaseService> _logger;

    public TeamShowcaseService(
        AppDbContext db,
        TeamMembershipGuard guard,
        ShowcaseWriter writer,
        IImageProcessor imageProcessor,
        IOptions<ImageProcessingOptions> imageOptions,
        IMediaStore mediaStore,
        ILogger<TeamShowcaseService> logger)
    {
        _db = db;
        _guard = guard;
        _writer = writer;
        _imageProcessor = imageProcessor;
        _imageOptions = imageOptions.Value;
        _mediaStore = mediaStore;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ShowcaseImageDto>?> ListAsync(
        string slug, Guid viewerUserId, CancellationToken ct = default)
    {
        var teamId = await ResolveTeamIdAsync(slug, ct);
        if (teamId is null)
        {
            return null;
        }

        // No membership join: the gallery is what the team shows the platform, so it neither widens
        // for members nor narrows for signed-in non-members (spec FR-020). The signed-in requirement
        // is the controller's class-level [Authorize] (feature 026).
        return await _db.TeamShowcaseImages
            .AsNoTracking()
            .Where(g => g.TeamId == teamId.Value)
            .OrderBy(g => g.Position)
            .ThenBy(g => g.Id)
            .Select(g => new ShowcaseImageDto(g.Id, g.Caption, g.Position))
            .ToListAsync(ct);
    }

    public async Task<MediaContent?> GetImageAsync(
        string slug, Guid imageId, Guid viewerUserId, CancellationToken ct = default)
    {
        var normalized = TeamSlugPolicy.Normalize(slug);

        // Descriptor first, store second — authorization is decided against relational data before a
        // byte is fetched (feature 035).
        var data = await _db.TeamShowcaseImages
            .AsNoTracking()
            .Where(g => g.Id == imageId && g.Team.Slug == normalized)
            .Select(g => new { g.ObjectKey, g.ContentType })
            .FirstOrDefaultAsync(ct);

        if (data is null)
        {
            return null;
        }

        var content = await _mediaStore.OpenReadAsync(data.ObjectKey, ct);
        return content is null ? null : new MediaContent(content, data.ContentType, data.ObjectKey);
    }

    public async Task<ShowcaseAddResult> AddAsync(
        string slug, Guid actorUserId, byte[] content, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { } team)
        {
            return ShowcaseAddResult.Fail(ShowcaseAddStatus.OwnerNotFound);
        }

        // A non-member cannot distinguish a team they may not touch from one that does not exist;
        // an admin-less member gets an honest 403, because they already know the team exists.
        if (!team.IsMember)
        {
            return ShowcaseAddResult.Fail(ShowcaseAddStatus.OwnerNotFound);
        }

        if (!team.IsAdmin)
        {
            return ShowcaseAddResult.Fail(ShowcaseAddStatus.Forbidden);
        }

        var processed = _imageProcessor.Process(content, _imageOptions.Showcase);
        if (processed.Status != ImageProcessingStatus.Success)
        {
            return ShowcaseAddResult.Fail(MapProcessingStatus(processed.Status), processed.Reason);
        }

        var existing = await _db.TeamShowcaseImages.CountAsync(g => g.TeamId == team.TeamId, ct);
        if (existing >= ShowcaseWriter.MaxImagesPerOwner)
        {
            return ShowcaseAddResult.Fail(ShowcaseAddStatus.GalleryFull);
        }

        var objectKey = MediaObjectKey.Create(MediaKind.TeamShowcase);

        try
        {
            using var normalized = new MemoryStream(processed.Bytes!);
            await _mediaStore.PutAsync(objectKey, normalized, processed.ContentType!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Showcase image could not be stored for team {TeamId}", team.TeamId);
            return ShowcaseAddResult.Fail(ShowcaseAddStatus.StoreUnavailable);
        }

        var result = await _writer.AddAsync<TeamShowcaseImage>(
            ShowcaseOwner.Team,
            team.TeamId,
            g => g.TeamId == team.TeamId,
            position => new TeamShowcaseImage
            {
                TeamId = team.TeamId,
                Position = position,
                ObjectKey = objectKey,
                ContentType = processed.ContentType!,
                SizeBytes = processed.Bytes!.Length,
            },
            ct);

        if (result.Status != ShowcaseAddStatus.Success)
        {
            await TryDeleteObjectAsync(objectKey, ct);
        }

        return result;
    }

    public async Task<ShowcaseMutateStatus> SetCaptionAsync(
        string slug, Guid actorUserId, Guid imageId, string? caption, CancellationToken ct = default)
    {
        var (team, refusal) = await ResolveAdminAsync(slug, actorUserId, ct);
        if (refusal is { } denied)
        {
            return denied;
        }

        var trimmed = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (trimmed is not null && trimmed.Length > Profile.ProfileShowcaseService.MaxCaptionLength)
        {
            return ShowcaseMutateStatus.CaptionTooLong;
        }

        var updated = await _db.TeamShowcaseImages
            .Where(g => g.Id == imageId && g.TeamId == team!.Value.TeamId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.Caption, trimmed)
                // ExecuteUpdate bypasses the change tracker, so the audit interceptor does not run
                // (constitution Principle III).
                .SetProperty(g => g.ModifiedDate, DateTime.UtcNow), ct);

        return updated == 0 ? ShowcaseMutateStatus.NotFound : ShowcaseMutateStatus.Success;
    }

    public async Task<ShowcaseMutateStatus> RemoveAsync(
        string slug, Guid actorUserId, Guid imageId, CancellationToken ct = default)
    {
        var (team, refusal) = await ResolveAdminAsync(slug, actorUserId, ct);
        if (refusal is { } denied)
        {
            return denied;
        }

        var teamId = team!.Value.TeamId;
        var result = await _writer.RemoveAsync<TeamShowcaseImage>(
            ShowcaseOwner.Team, teamId, imageId, g => g.TeamId == teamId, ct);

        if (result.Status == ShowcaseMutateStatus.Success)
        {
            await TryDeleteObjectAsync(result.ObjectKey!, ct);
        }

        return result.Status;
    }

    public async Task<ShowcaseMutateStatus> ReorderAsync(
        string slug, Guid actorUserId, IReadOnlyList<Guid> imageIds, CancellationToken ct = default)
    {
        var (team, refusal) = await ResolveAdminAsync(slug, actorUserId, ct);
        if (refusal is { } denied)
        {
            return denied;
        }

        var teamId = team!.Value.TeamId;
        return await _writer.ReorderAsync<TeamShowcaseImage>(
            ShowcaseOwner.Team, teamId, imageIds, g => g.TeamId == teamId, ct);
    }

    public async Task<IReadOnlyList<string>> ObjectKeysForTeamAsync(Guid teamId, CancellationToken ct = default) =>
        await _db.TeamShowcaseImages
            .AsNoTracking()
            .Where(g => g.TeamId == teamId)
            .Select(g => g.ObjectKey)
            .ToListAsync(ct);

    public async Task ReclaimObjectsAsync(IReadOnlyList<string> objectKeys, CancellationToken ct = default)
    {
        foreach (var objectKey in objectKeys)
        {
            await TryDeleteObjectAsync(objectKey, ct);
        }
    }

    private async Task<Guid?> ResolveTeamIdAsync(string slug, CancellationToken ct)
    {
        var normalized = TeamSlugPolicy.Normalize(slug);
        return await _db.Teams
            .AsNoTracking()
            .Where(t => t.Slug == normalized)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Resolve the caller as an admin of the team, or the refusal to return instead.</summary>
    private async Task<(TeamAccess? Team, ShowcaseMutateStatus? Refusal)> ResolveAdminAsync(
        string slug, Guid actorUserId, CancellationToken ct)
    {
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { IsMember: true } team)
        {
            return (null, ShowcaseMutateStatus.NotFound);
        }

        return team.IsAdmin ? (team, null) : (null, ShowcaseMutateStatus.Forbidden);
    }

    private async Task TryDeleteObjectAsync(string objectKey, CancellationToken ct)
    {
        try
        {
            await _mediaStore.DeleteAsync(objectKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Team showcase object could not be deleted; left for reconciliation");
        }
    }

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
