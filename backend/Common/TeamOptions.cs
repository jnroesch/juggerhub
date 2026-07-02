namespace JuggerHub.Common;

/// <summary>
/// Configuration for the team feature (invite-link lifetime, slug length bounds, news
/// body cap). Bound from the <c>Teams</c> config section with safe defaults so the
/// feature works with zero configuration. No secrets here.
/// </summary>
public sealed class TeamOptions
{
    public const string SectionName = "Teams";

    /// <summary>Days an invitation (link or targeted) stays valid after issuance.</summary>
    public int InviteLinkTtlDays { get; set; } = 7;

    /// <summary>Minimum team-slug length (inclusive).</summary>
    public int SlugMinLength { get; set; } = 3;

    /// <summary>Maximum team-slug length (inclusive).</summary>
    public int SlugMaxLength { get; set; } = 30;

    /// <summary>Maximum team display-name length (inclusive).</summary>
    public int NameMaxLength { get; set; } = 50;

    /// <summary>Maximum news-post body length (inclusive).</summary>
    public int NewsBodyMaxLength { get; set; } = 1000;
}
