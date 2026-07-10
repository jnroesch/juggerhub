using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Recognition;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Recognition;

/// <inheritdoc />
public sealed class RecognitionDisplayService : IRecognitionDisplayService
{
    private readonly AppDbContext _db;
    private readonly int _cap;

    public RecognitionDisplayService(AppDbContext db, IOptions<RecognitionOptions> options)
    {
        _db = db;
        _cap = options.Value.MaxDisplayedPerGroup;
    }

    public Task<SubjectRecognitionsDto> ForPlayerAsync(Guid playerProfileId, CancellationToken ct = default) =>
        LoadAsync(b => b.PlayerProfileId == playerProfileId, a => a.PlayerProfileId == playerProfileId, ct);

    public Task<SubjectRecognitionsDto> ForTeamAsync(Guid teamId, CancellationToken ct = default) =>
        LoadAsync(b => b.TeamId == teamId, a => a.TeamId == teamId, ct);

    private async Task<SubjectRecognitionsDto> LoadAsync(
        System.Linq.Expressions.Expression<Func<BadgeAward, bool>> badgeSubject,
        System.Linq.Expressions.Expression<Func<AchievementAward, bool>> achievementSubject,
        CancellationToken ct)
    {
        var badges = await _db.BadgeAwards
            .AsNoTracking()
            .Where(a => a.Status == AwardStatus.Active)
            .Where(badgeSubject)
            .OrderByDescending(a => a.EarnedAt)
            .Take(_cap)
            .Select(a => new EarnedRecognitionDto(
                a.BadgeDefinitionId, a.Definition.Name, a.Definition.Description, a.Definition.Icon != null,
                a.EarnedAt, null, null))
            .ToListAsync(ct);

        var achievements = await _db.AchievementAwards
            .AsNoTracking()
            .Where(a => a.Status == AwardStatus.Active)
            .Where(achievementSubject)
            .OrderByDescending(a => a.EarnedAt)
            .Take(_cap)
            .Select(a => new EarnedRecognitionDto(
                a.AchievementDefinitionId, a.Definition.Name, a.Definition.Description, a.Definition.Icon != null,
                a.EarnedAt, a.ContextYear, a.ContextLabel))
            .ToListAsync(ct);

        return new SubjectRecognitionsDto(badges, achievements);
    }
}
