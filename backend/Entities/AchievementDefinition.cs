namespace JuggerHub.Entities;

/// <summary>
/// The catalog template for an achievement — a competitive accomplishment (e.g. winning a
/// championship). Admin-curated (feature 012). Achievements are a SEPARATE system from badges
/// (spec FR-003); accomplishment context (year / competition) is recorded per-award on
/// <see cref="AchievementAward"/>, not here.
/// </summary>
public sealed class AchievementDefinition : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this achievement may be granted to player subjects. At least one is true.</summary>
    public bool AppliesToPlayers { get; set; }

    /// <summary>Whether this achievement may be granted to team subjects.</summary>
    public bool AppliesToTeams { get; set; }

    /// <summary>Retired achievements are hidden from grant pickers but existing awards are preserved.</summary>
    public bool IsRetired { get; set; }

    public AchievementIcon? Icon { get; set; }

    public ICollection<AchievementAward> Awards { get; set; } = [];
}
