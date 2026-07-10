using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Achievements;
using JuggerHub.Dtos.Recognition;
using JuggerHub.Entities;
using JuggerHub.Services.Recognition;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JuggerHub.Services.Achievements;

/// <inheritdoc />
public sealed class AchievementService : IAchievementService
{
    private readonly AppDbContext _db;
    private readonly RecognitionOptions _options;

    public AchievementService(AppDbContext db, IOptions<RecognitionOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<PagedResult<AchievementDefinitionDto>> ListDefinitionsAsync(
        PaginationRequest pagination, bool includeRetired, CancellationToken ct = default)
    {
        var query = _db.AchievementDefinitions.AsNoTracking();
        if (!includeRetired)
        {
            query = query.Where(d => !d.IsRetired);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(d => d.Name)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(d => new AchievementDefinitionDto(
                d.Id, d.Name, d.Description, d.AppliesToPlayers, d.AppliesToTeams, d.IsRetired, d.Icon != null,
                d.Awards.Count(a => a.Status == AwardStatus.Active), d.CreatedDate))
            .ToListAsync(ct);

        return new PagedResult<AchievementDefinitionDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<AchievementDefinitionDto> CreateDefinitionAsync(AchievementDefinitionUpsertRequest request, CancellationToken ct = default)
    {
        var definition = new AchievementDefinition
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            AppliesToPlayers = request.AppliesToPlayers,
            AppliesToTeams = request.AppliesToTeams,
        };
        _db.AchievementDefinitions.Add(definition);
        await _db.SaveChangesAsync(ct);
        return ToDto(definition, hasIcon: false, grantedCount: 0);
    }

    public async Task<AchievementDefinitionDto?> UpdateDefinitionAsync(Guid id, AchievementDefinitionUpsertRequest request, CancellationToken ct = default)
    {
        var definition = await _db.AchievementDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (definition is null)
        {
            return null;
        }

        definition.Name = request.Name.Trim();
        definition.Description = request.Description.Trim();
        definition.AppliesToPlayers = request.AppliesToPlayers;
        definition.AppliesToTeams = request.AppliesToTeams;
        await _db.SaveChangesAsync(ct);

        var hasIcon = await _db.AchievementIcons.AnyAsync(i => i.AchievementDefinitionId == id, ct);
        var grantedCount = await _db.AchievementAwards.CountAsync(a => a.AchievementDefinitionId == id && a.Status == AwardStatus.Active, ct);
        return ToDto(definition, hasIcon, grantedCount);
    }

    public async Task<bool> RetireDefinitionAsync(Guid id, CancellationToken ct = default)
    {
        var definition = await _db.AchievementDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (definition is null)
        {
            return false;
        }

        definition.IsRetired = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IconOutcome> SetIconAsync(Guid definitionId, byte[] content, CancellationToken ct = default)
    {
        if (content.Length == 0)
        {
            return IconOutcome.Empty;
        }

        if (content.Length > _options.MaxIconBytes)
        {
            return IconOutcome.TooLarge;
        }

        var sniffed = ImageValidation.SniffImageContentType(content);
        if (sniffed is null)
        {
            return IconOutcome.InvalidType;
        }

        if (!await _db.AchievementDefinitions.AnyAsync(d => d.Id == definitionId, ct))
        {
            return IconOutcome.DefinitionNotFound;
        }

        var icon = await _db.AchievementIcons.FirstOrDefaultAsync(i => i.AchievementDefinitionId == definitionId, ct);
        if (icon is null)
        {
            _db.AchievementIcons.Add(new AchievementIcon
            {
                AchievementDefinitionId = definitionId,
                Bytes = content,
                ContentType = sniffed,
            });
        }
        else
        {
            icon.Bytes = content;
            icon.ContentType = sniffed;
        }

        await _db.SaveChangesAsync(ct);
        return IconOutcome.Stored;
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetIconAsync(Guid definitionId, CancellationToken ct = default)
    {
        var data = await _db.AchievementIcons
            .AsNoTracking()
            .Where(i => i.AchievementDefinitionId == definitionId)
            .Select(i => new { i.Bytes, i.ContentType })
            .FirstOrDefaultAsync(ct);

        return data is null ? null : (data.Bytes, data.ContentType);
    }

    public async Task<(GrantOutcome Outcome, AchievementAwardDto? Award)> GrantAsync(
        Guid definitionId, GrantAchievementRequest request, Guid grantedByUserId, CancellationToken ct = default)
    {
        var definition = await _db.AchievementDefinitions
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

        var alreadyActive = await _db.AchievementAwards.AnyAsync(a =>
            a.AchievementDefinitionId == definitionId &&
            a.Status == AwardStatus.Active &&
            a.PlayerProfileId == playerProfileId &&
            a.TeamId == teamId, ct);
        if (alreadyActive)
        {
            return (GrantOutcome.Duplicate, null);
        }

        var award = new AchievementAward
        {
            AchievementDefinitionId = definitionId,
            PlayerProfileId = playerProfileId,
            TeamId = teamId,
            Source = AwardSource.Manual,
            Status = AwardStatus.Active,
            EarnedAt = DateTime.UtcNow,
            GrantedByUserId = grantedByUserId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ContextYear = request.ContextYear,
            ContextLabel = string.IsNullOrWhiteSpace(request.ContextLabel) ? null : request.ContextLabel.Trim(),
        };
        _db.AchievementAwards.Add(award);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (GrantOutcome.Duplicate, null);
        }

        return (GrantOutcome.Granted, new AchievementAwardDto(
            award.Id, definitionId, subjectType, subjectRef, award.Source, award.Status, award.EarnedAt,
            award.ContextYear, award.ContextLabel));
    }

    public async Task<RevokeOutcome> RevokeAsync(Guid awardId, string? reason, Guid revokedByUserId, CancellationToken ct = default)
    {
        var award = await _db.AchievementAwards.FirstOrDefaultAsync(a => a.Id == awardId, ct);
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
        System.Linq.Expressions.Expression<Func<AchievementAward, bool>> subject, CancellationToken ct) =>
        await _db.AchievementAwards
            .AsNoTracking()
            .Where(a => a.Status == AwardStatus.Active)
            .Where(subject)
            .OrderByDescending(a => a.EarnedAt)
            .Select(a => new AdminAwardDto(
                a.Id,
                a.AchievementDefinitionId,
                a.Definition.Name,
                a.EarnedAt,
                _db.PlayerProfiles.Where(p => p.UserId == a.GrantedByUserId).Select(p => p.DisplayName).FirstOrDefault() ?? "An admin",
                a.Note,
                a.ContextYear,
                a.ContextLabel))
            .ToListAsync(ct);

    private static AchievementDefinitionDto ToDto(AchievementDefinition d, bool hasIcon, int grantedCount) =>
        new(d.Id, d.Name, d.Description, d.AppliesToPlayers, d.AppliesToTeams, d.IsRetired, hasIcon, grantedCount, d.CreatedDate);
}
