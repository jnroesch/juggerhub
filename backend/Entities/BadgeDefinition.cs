namespace JuggerHub.Entities;

/// <summary>
/// The catalog template for a badge — a status / membership / milestone recognition
/// (e.g. beta-tester, tenure). Admin-curated (feature 012). Badges are a SEPARATE system
/// from achievements (spec FR-003); this type deliberately has no accomplishment context.
/// </summary>
public sealed class BadgeDefinition : BaseEntity
{
    /// <summary>Catalog label shown wherever the badge appears.</summary>
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this badge may be granted to player subjects. At least one of the two is true.</summary>
    public bool AppliesToPlayers { get; set; }

    /// <summary>Whether this badge may be granted to team subjects.</summary>
    public bool AppliesToTeams { get; set; }

    /// <summary>Retired badges are hidden from grant pickers but existing awards are preserved.</summary>
    public bool IsRetired { get; set; }

    /// <summary>Optional 1:1 icon (bytes live in a side table so catalog reads stay lean).</summary>
    public BadgeIcon? Icon { get; set; }

    public ICollection<BadgeAward> Awards { get; set; } = [];
}
