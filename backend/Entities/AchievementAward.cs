namespace JuggerHub.Entities;

/// <summary>
/// An achievement granted to a <see cref="PlayerProfile"/> OR a <see cref="Team"/> — exactly one
/// subject is set (DB CHECK). Same lifecycle as <see cref="BadgeAward"/> plus optional
/// accomplishment context (e.g. "National Championship", 2026) recorded at grant time. v1 only
/// writes <see cref="AwardSource.Manual"/>; a filtered unique index keeps at most one ACTIVE award
/// per (definition, subject).
/// </summary>
public sealed class AchievementAward : BaseEntity
{
    public Guid AchievementDefinitionId { get; set; }

    /// <summary>Player subject (null for a team award).</summary>
    public Guid? PlayerProfileId { get; set; }

    /// <summary>Team subject (null for a player award).</summary>
    public Guid? TeamId { get; set; }

    public AwardSource Source { get; set; } = AwardSource.Manual;

    public AwardStatus Status { get; set; } = AwardStatus.Active;

    public DateTime EarnedAt { get; set; }

    public Guid GrantedByUserId { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? RevokedByUserId { get; set; }

    public string? RevokedReason { get; set; }

    /// <summary>Optional accomplishment year, e.g. 2026.</summary>
    public int? ContextYear { get; set; }

    /// <summary>Optional accomplishment label, e.g. "National Championship".</summary>
    public string? ContextLabel { get; set; }

    public AchievementDefinition Definition { get; set; } = null!;

    public PlayerProfile? PlayerProfile { get; set; }

    public Team? Team { get; set; }
}
