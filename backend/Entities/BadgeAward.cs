namespace JuggerHub.Entities;

/// <summary>
/// A badge granted to a <see cref="PlayerProfile"/> OR a <see cref="Team"/> — exactly one subject
/// is set (DB CHECK), mirroring the polymorphic <see cref="EventSignup"/> pattern. v1 only ever
/// writes <see cref="AwardSource.Manual"/>. A filtered unique index keeps at most one ACTIVE award
/// per (definition, subject); revoked rows are retained for audit and allow a later re-grant.
/// </summary>
public sealed class BadgeAward : BaseEntity
{
    public Guid BadgeDefinitionId { get; set; }

    /// <summary>Player subject (null for a team award).</summary>
    public Guid? PlayerProfileId { get; set; }

    /// <summary>Team subject (null for a player award).</summary>
    public Guid? TeamId { get; set; }

    public AwardSource Source { get; set; } = AwardSource.Manual;

    public AwardStatus Status { get; set; } = AwardStatus.Active;

    /// <summary>When the badge was earned (UTC), set at grant time.</summary>
    public DateTime EarnedAt { get; set; }

    /// <summary>The administrator who granted it.</summary>
    public Guid GrantedByUserId { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? RevokedByUserId { get; set; }

    public string? RevokedReason { get; set; }

    public BadgeDefinition Definition { get; set; } = null!;

    public PlayerProfile? PlayerProfile { get; set; }

    public Team? Team { get; set; }
}
