using System.ComponentModel.DataAnnotations;
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Achievements;

/// <summary>Admin create/edit of an achievement definition. At least one subject type must apply.</summary>
public sealed record AchievementDefinitionUpsertRequest(
    [Required, MaxLength(60)] string Name,
    [Required, MaxLength(280)] string Description,
    bool AppliesToPlayers,
    bool AppliesToTeams) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AppliesToPlayers && !AppliesToTeams)
        {
            yield return new ValidationResult(
                "An achievement must apply to players, teams, or both.",
                [nameof(AppliesToPlayers), nameof(AppliesToTeams)]);
        }
    }
}

/// <summary>An achievement definition as returned to the admin catalog.</summary>
public sealed record AchievementDefinitionDto(
    Guid Id,
    string Name,
    string Description,
    bool AppliesToPlayers,
    bool AppliesToTeams,
    bool IsRetired,
    bool HasIcon);

/// <summary>
/// Grant an achievement to exactly one subject, with optional accomplishment context
/// (e.g. year 2026, label "National Championship").
/// </summary>
public sealed record GrantAchievementRequest(
    string? PlayerHandle,
    string? TeamSlug,
    int? ContextYear,
    [MaxLength(120)] string? ContextLabel) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasPlayer = !string.IsNullOrWhiteSpace(PlayerHandle);
        var hasTeam = !string.IsNullOrWhiteSpace(TeamSlug);
        if (hasPlayer == hasTeam)
        {
            yield return new ValidationResult(
                "Provide exactly one of playerHandle or teamSlug.",
                [nameof(PlayerHandle), nameof(TeamSlug)]);
        }
    }
}

/// <summary>A granted achievement award, including any accomplishment context.</summary>
public sealed record AchievementAwardDto(
    Guid Id,
    Guid DefinitionId,
    SubjectType SubjectType,
    string SubjectRef,
    AwardSource Source,
    AwardStatus Status,
    DateTime EarnedAt,
    int? ContextYear,
    string? ContextLabel);
