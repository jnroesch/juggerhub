using System.ComponentModel.DataAnnotations;

namespace JuggerHub.Dtos.Recognition;

/// <summary>Optional reason recorded when an admin revokes a badge or achievement award.</summary>
public sealed record RevokeAwardRequest([MaxLength(280)] string? Reason);

/// <summary>
/// One earned badge or achievement as shown publicly on a profile / team page (feature 012 US2).
/// Active awards only. Exposes just the public field set — never the admin note or granter.
/// The frontend builds the icon URL from the definition id (per family).
/// </summary>
public sealed record EarnedRecognitionDto(
    Guid DefinitionId,
    string Name,
    string Description,
    bool HasIcon,
    DateTime EarnedAt,
    int? ContextYear,
    string? ContextLabel);

/// <summary>A subject's badges and achievements for display (two groups).</summary>
public sealed record SubjectRecognitionsDto(
    IReadOnlyList<EarnedRecognitionDto> Badges,
    IReadOnlyList<EarnedRecognitionDto> Achievements);

/// <summary>
/// One award in the ADMIN subject view / grant flow (feature 012 US1). Carries the award id (to
/// revoke), the admin note, and who granted it — admin-facing detail not shown on public pages.
/// </summary>
public sealed record AdminAwardDto(
    Guid AwardId,
    Guid DefinitionId,
    string Name,
    DateTime EarnedAt,
    string GrantedByName,
    string? Note,
    int? ContextYear,
    string? ContextLabel);

/// <summary>A subject's active awards for the admin grant/revoke UI.</summary>
public sealed record AdminSubjectAwardsDto(
    string SubjectRef,
    IReadOnlyList<AdminAwardDto> Badges,
    IReadOnlyList<AdminAwardDto> Achievements);
