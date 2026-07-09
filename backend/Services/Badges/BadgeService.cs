using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Badges;
using JuggerHub.Entities;
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

    public BadgeService(AppDbContext db, IOptions<RecognitionOptions> options)
    {
        _db = db;
        _options = options.Value;
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
                d.Id, d.Name, d.Description, d.AppliesToPlayers, d.AppliesToTeams, d.IsRetired, d.Icon != null))
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
        return ToDto(definition, hasIcon: false);
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
        return ToDto(definition, hasIcon);
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

        if (!await _db.BadgeDefinitions.AnyAsync(d => d.Id == definitionId, ct))
        {
            return IconOutcome.DefinitionNotFound;
        }

        var icon = await _db.BadgeIcons.FirstOrDefaultAsync(i => i.BadgeDefinitionId == definitionId, ct);
        if (icon is null)
        {
            _db.BadgeIcons.Add(new BadgeIcon
            {
                BadgeDefinitionId = definitionId,
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
        var data = await _db.BadgeIcons
            .AsNoTracking()
            .Where(i => i.BadgeDefinitionId == definitionId)
            .Select(i => new { i.Bytes, i.ContentType })
            .FirstOrDefaultAsync(ct);

        return data is null ? null : (data.Bytes, data.ContentType);
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

    private static BadgeDefinitionDto ToDto(BadgeDefinition d, bool hasIcon) =>
        new(d.Id, d.Name, d.Description, d.AppliesToPlayers, d.AppliesToTeams, d.IsRetired, hasIcon);
}
