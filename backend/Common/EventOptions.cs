namespace JuggerHub.Common;

/// <summary>
/// Configuration for the events feature (co-admin invite-link lifetime, field length/limit
/// bounds, news/contact caps). Bound from the <c>Events</c> config section with safe defaults
/// so the feature works with zero configuration. No secrets here (fee IBAN/recipient are
/// per-event content, entered by organisers — not app config).
/// </summary>
public sealed class EventOptions
{
    public const string SectionName = "Events";

    /// <summary>Days a co-admin invitation (link or targeted) stays valid after issuance.</summary>
    public int InviteLinkTtlDays { get; set; } = 7;

    /// <summary>Minimum event-name length (inclusive).</summary>
    public int NameMinLength { get; set; } = 3;

    /// <summary>Maximum event-name length (inclusive).</summary>
    public int NameMaxLength { get; set; } = 120;

    /// <summary>Maximum custom-type label length (Other type).</summary>
    public int CustomTypeLabelMaxLength { get; set; } = 40;

    /// <summary>Maximum event-description length (inclusive).</summary>
    public int DescriptionMaxLength { get; set; } = 4000;

    /// <summary>Maximum participation limit accepted at creation (guards against absurd values).</summary>
    public int MaxParticipationLimit { get; set; } = 100_000;

    /// <summary>Maximum news-post body length (inclusive).</summary>
    public int NewsBodyMaxLength { get; set; } = 2000;

    /// <summary>Maximum contact-role length (inclusive).</summary>
    public int ContactRoleMaxLength { get; set; } = 80;
}
