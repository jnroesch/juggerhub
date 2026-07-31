using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Badges;
using JuggerHub.Dtos.Recognition;
using JuggerHub.Entities;
using JuggerHub.Services.Media;
using JuggerHub.Services.Recognition;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JuggerHub.Services.Badges;

/// <inheritdoc />
public sealed class BadgeService : IBadgeService
{
    private readonly AppDbContext _db;
    private readonly RecognitionOptions _options;
    private readonly IImageProcessor _imageProcessor;
    private readonly ImageProcessingOptions _imageOptions;
    private readonly IMediaStore _mediaStore;

    public BadgeService(
        AppDbContext db,
        IOptions<RecognitionOptions> options,
        IImageProcessor imageProcessor,
        IOptions<ImageProcessingOptions> imageOptions,
        IMediaStore mediaStore)
    {
        _db = db;
        _options = options.Value;
        _imageProcessor = imageProcessor;
        _imageOptions = imageOptions.Value;
        _mediaStore = mediaStore;
    }

    public async Task<PagedResult<BadgeDefinitionDto>> ListDefinitionsAsync(
        PaginationRequest pagination, bool includeRetired, CancellationToken ct = default)
    {
        var query = _db.BadgeDefinitions.AsNoTracking();
        if (!includeRetired)
        {
            query = query.Where(d => !d.IsRetired);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(d => d.Name)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(d => new BadgeDefinitionDto(
                d.Id, d.Name, d.Description, d.AppliesToPlayers, d.AppliesToTeams, d.IsRetired, d.Icon != null,
                d.Awards.Count(a => a.Status == AwardStatus.Active), d.CreatedDate))
            .ToListAsync(ct);

        return new PagedResult<BadgeDefinitionDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<BadgeDefinitionDto> CreateDefinitionAsync(BadgeDefinitionUpsertRequest request, CancellationToken ct = default)
    {
        var definition = new BadgeDefinition
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            AppliesToPlayers = request.AppliesToPlayers,
            AppliesToTeams = request.AppliesToTeams,
        };
        _db.BadgeDefinitions.Add(definition);
        await _db.SaveChangesAsync(ct);
        return ToDto(definition, hasIcon: false, grantedCount: 0);
    }

    public async Task<BadgeDefinitionDto?> UpdateDefinitionAsync(Guid id, BadgeDefinitionUpsertRequest request, CancellationToken ct = default)
    {
        var definition = await _db.BadgeDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (definition is null)
        {
            return null;
        }

        definition.Name = request.Name.Trim();
        definition.Description = request.Description.Trim();
        definition.AppliesToPlayers = request.AppliesToPlayers;
        definition.AppliesToTeams = request.AppliesToTeams;
        await _db.SaveChangesAsync(ct);

        var hasIcon = await _db.BadgeIcons.AnyAsync(i => i.BadgeDefinitionId == id, ct);
        var grantedCount = await _db.BadgeAwards.CountAsync(a => a.BadgeDefinitionId == id && a.Status == AwardStatus.Active, ct);
        return ToDto(definition, hasIcon, grantedCount);
    }

    public async Task<bool> RetireDefinitionAsync(Guid id, CancellationToken ct = default)
    {
        var definition = await _db.BadgeDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (definition is null)
        {
            return false;
        }

        definition.IsRetired = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReinstateDefinitionAsync(Guid id, CancellationToken ct = default)
    {
        var definition = await _db.BadgeDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (definition is null)
        {
            return false;
        }

        definition.IsRetired = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveIconAsync(Guid definitionId, CancellationToken ct = default)
    {
        if (!await _db.BadgeDefinitions.AnyAsync(d => d.Id == definitionId, ct))
        {
            return false;
        }

        var icon = await _db.BadgeIcons.FirstOrDefaultAsync(i => i.BadgeDefinitionId == definitionId, ct);
        if (icon is not null)
        {
            var removedKey = icon.ObjectKey;
            _db.BadgeIcons.Remove(icon);
            await _db.SaveChangesAsync(ct);

            // Delete the object AFTER the row commits (feature 035). This is application code, so
            // it is one of the paths where the object genuinely goes away rather than being left
            // for the reconciliation sweep.
            await _mediaStore.DeleteAsync(removedKey, ct);
        }

        return true;
    }

    public async Task<IconSetResult> SetIconAsync(Guid definitionId, byte[] content, CancellationToken ct = default)
    {
        // Normalize server-side (#101, same pipeline as avatars in feature 034 / #98): decompression-bomb
        // guard + strip metadata + resize + re-encode to WebP. The declared content type is never
        // trusted, and on any rejection we return without touching the stored icon.
        var processed = _imageProcessor.Process(content, _imageOptions.Icon);
        if (processed.Status != ImageProcessingStatus.Success)
        {
            return IconProcessing.ToFailure(processed);
        }

        if (!await _db.BadgeDefinitions.AnyAsync(d => d.Id == definitionId, ct))
        {
            return IconSetResult.Fail(IconOutcome.DefinitionNotFound, "This badge definition doesn't exist.");
        }

        // Only the normalized WebP is stored; the original upload is discarded.
        var icon = await _db.BadgeIcons.FirstOrDefaultAsync(i => i.BadgeDefinitionId == definitionId, ct);

        // Same ordering as avatars (feature 035): key → object → descriptor → delete superseded.
        var objectKey = MediaObjectKey.Create(MediaKind.BadgeIcon);
        var supersededKey = icon?.ObjectKey;

        using (var normalized = new MemoryStream(processed.Bytes!))
        {
            await _mediaStore.PutAsync(objectKey, normalized, processed.ContentType!, ct);
        }

        if (icon is null)
        {
            _db.BadgeIcons.Add(new BadgeIcon
            {
                BadgeDefinitionId = definitionId,
                ObjectKey = objectKey,
                SizeBytes = processed.Bytes!.Length,
                ContentType = processed.ContentType!,
            });
        }
        else
        {
            icon.ObjectKey = objectKey;
            icon.SizeBytes = processed.Bytes!.Length;
            icon.ContentType = processed.ContentType!;
        }

        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(supersededKey))
        {
            await _mediaStore.DeleteAsync(supersededKey, ct);
        }

        return IconSetResult.Stored();
    }

    public async Task<MediaContent?> GetIconAsync(Guid definitionId, CancellationToken ct = default)
    {
        // Catalogue icons carry no subject data and are anonymously readable by intent (feature
        // 026 allowlist), so there is no visibility gate here — but the bytes still come through
        // the platform, never from a publicly-readable store.
        var data = await _db.BadgeIcons
            .AsNoTracking()
            .Where(i => i.BadgeDefinitionId == definitionId)
            .Select(i => new { i.ObjectKey, i.ContentType })
            .FirstOrDefaultAsync(ct);

        if (data is null)
        {
            return null;
        }

        var content = await _mediaStore.OpenReadAsync(data.ObjectKey, ct);
        return content is null ? null : new MediaContent(content, data.ContentType, data.ObjectKey);
    }

    public async Task<(GrantOutcome Outcome, BadgeAwardDto? Award)> GrantAsync(
        Guid definitionId, GrantBadgeRequest request, Guid grantedByUserId, CancellationToken ct = default)
    {
        var definition = await _db.BadgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == definitionId, ct);
        if (definition is null)
        {
            return (GrantOutcome.DefinitionNotFound, null);
        }

        if (definition.IsRetired)
        {
            return (GrantOutcome.DefinitionRetired, null);
        }

        Guid? playerProfileId = null;
        Guid? teamId = null;
        SubjectType subjectType;
        string subjectRef;

        if (!string.IsNullOrWhiteSpace(request.PlayerHandle))
        {
            if (!definition.AppliesToPlayers)
            {
                return (GrantOutcome.SubjectTypeMismatch, null);
            }

            var handle = request.PlayerHandle.Trim().ToLowerInvariant();
            playerProfileId = await _db.PlayerProfiles
                .Where(p => p.Handle == handle)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
            if (playerProfileId is null)
            {
                return (GrantOutcome.SubjectNotFound, null);
            }

            subjectType = SubjectType.Player;
            subjectRef = handle;
        }
        else
        {
            if (!definition.AppliesToTeams)
            {
                return (GrantOutcome.SubjectTypeMismatch, null);
            }

            var slug = request.TeamSlug!.Trim().ToLowerInvariant();
            teamId = await _db.Teams
                .Where(t => t.Slug == slug)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(ct);
            if (teamId is null)
            {
                return (GrantOutcome.SubjectNotFound, null);
            }

            subjectType = SubjectType.Team;
            subjectRef = slug;
        }

        var alreadyActive = await _db.BadgeAwards.AnyAsync(a =>
            a.BadgeDefinitionId == definitionId &&
            a.Status == AwardStatus.Active &&
            a.PlayerProfileId == playerProfileId &&
            a.TeamId == teamId, ct);
        if (alreadyActive)
        {
            return (GrantOutcome.Duplicate, null);
        }

        var award = new BadgeAward
        {
            BadgeDefinitionId = definitionId,
            PlayerProfileId = playerProfileId,
            TeamId = teamId,
            Source = AwardSource.Manual,
            Status = AwardStatus.Active,
            EarnedAt = DateTime.UtcNow,
            GrantedByUserId = grantedByUserId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };
        _db.BadgeAwards.Add(award);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The filtered unique index is the backstop against a concurrent duplicate grant.
            return (GrantOutcome.Duplicate, null);
        }

        return (GrantOutcome.Granted, new BadgeAwardDto(
            award.Id, definitionId, subjectType, subjectRef, award.Source, award.Status, award.EarnedAt));
    }

    public async Task<RevokeOutcome> RevokeAsync(Guid awardId, string? reason, Guid revokedByUserId, CancellationToken ct = default)
    {
        var award = await _db.BadgeAwards.FirstOrDefaultAsync(a => a.Id == awardId, ct);
        if (award is null || award.Status != AwardStatus.Active)
        {
            return RevokeOutcome.NotFound;
        }

        award.Status = AwardStatus.Revoked;
        award.RevokedAt = DateTime.UtcNow;
        award.RevokedByUserId = revokedByUserId;
        award.RevokedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await _db.SaveChangesAsync(ct);
        return RevokeOutcome.Revoked;
    }

    public async Task<IReadOnlyList<AdminAwardDto>?> ListPlayerAwardsAsync(string handle, CancellationToken ct = default)
    {
        var norm = handle.Trim().ToLowerInvariant();
        var profileId = await _db.PlayerProfiles.Where(p => p.Handle == norm).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        return profileId is null ? null : await AdminAwardsAsync(a => a.PlayerProfileId == profileId, ct);
    }

    public async Task<IReadOnlyList<AdminAwardDto>?> ListTeamAwardsAsync(string slug, CancellationToken ct = default)
    {
        var norm = slug.Trim().ToLowerInvariant();
        var teamId = await _db.Teams.Where(t => t.Slug == norm).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        return teamId is null ? null : await AdminAwardsAsync(a => a.TeamId == teamId, ct);
    }

    private async Task<IReadOnlyList<AdminAwardDto>> AdminAwardsAsync(
        System.Linq.Expressions.Expression<Func<BadgeAward, bool>> subject, CancellationToken ct) =>
        await _db.BadgeAwards
            .AsNoTracking()
            .Where(a => a.Status == AwardStatus.Active)
            .Where(subject)
            .OrderByDescending(a => a.EarnedAt)
            .Select(a => new AdminAwardDto(
                a.Id,
                a.BadgeDefinitionId,
                a.Definition.Name,
                a.EarnedAt,
                _db.PlayerProfiles.Where(p => p.UserId == a.GrantedByUserId).Select(p => p.DisplayName).FirstOrDefault() ?? "An admin",
                a.Note,
                null,
                null))
            .ToListAsync(ct);

    private static BadgeDefinitionDto ToDto(BadgeDefinition d, bool hasIcon, int grantedCount) =>
        new(d.Id, d.Name, d.Description, d.AppliesToPlayers, d.AppliesToTeams, d.IsRetired, hasIcon, grantedCount, d.CreatedDate);
}
