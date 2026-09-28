namespace JuggerHub.Entities;

/// <summary>
/// One of a team's external links (feature 061) — a free-text <see cref="Label"/> and a secure web
/// <see cref="Url"/>, shown on the team page in <see cref="Position"/> order. A team has at most
/// five (<c>TeamDetailsPolicy.MaxLinks</c>).
/// </summary>
/// <remarks>
/// The list is replaced as a whole on every details save, so a link has no identity a client can
/// address: the rows are deleted and re-inserted, and no DTO carries their id. Validation (https
/// only, no user info, a real host, no duplicates) lives in <c>TeamDetailsPolicy</c>; what is
/// stored here is the normalised absolute address.
/// </remarks>
public sealed class TeamLink : BaseEntity
{
    public Guid TeamId { get; set; }

    public Team Team { get; set; } = null!;

    /// <summary>What the link is called on the team page (1–30 characters).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The normalised absolute <c>https</c> address (at most 500 characters).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>0-based order as the admin entered the links; unique per team.</summary>
    public int Position { get; set; }
}
