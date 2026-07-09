using System.ComponentModel.DataAnnotations;
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Badges;

// Validation attributes go on record constructor parameters (MVC reads parameter-level
// metadata for positional records) — matching the Dtos/Auth + Dtos/Profile convention.

/// <summary>Admin create/edit of a badge definition. At least one subject type must apply.</summary>
public sealed record BadgeDefinitionUpsertRequest(
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
                "A badge must apply to players, teams, or both.",
                [nameof(AppliesToPlayers), nameof(AppliesToTeams)]);
        }
    }
}

/// <summary>A badge definition as returned to the admin catalog. <see cref="HasIcon"/> avoids shipping bytes.</summary>
public sealed record BadgeDefinitionDto(
    Guid Id,
    string Name,
    string Description,
    bool AppliesToPlayers,
    bool AppliesToTeams,
    bool IsRetired,
    bool HasIcon);

/// <summary>Grant a badge to exactly one subject — a player (by handle) OR a team (by slug), with an optional note.</summary>
public sealed record GrantBadgeRequest(
    string? PlayerHandle,
    string? TeamSlug,
    [MaxLength(280)] string? Note = null) : IValidatableObject
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

/// <summary>A granted badge award.</summary>
public sealed record BadgeAwardDto(
    Guid Id,
    Guid DefinitionId,
    SubjectType SubjectType,
    string SubjectRef,
    AwardSource Source,
    AwardStatus Status,
    DateTime EarnedAt);
