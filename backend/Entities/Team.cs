namespace JuggerHub.Entities;

/// <summary>
/// A group players belong to. Addressed by an immutable, unique <see cref="Slug"/>
/// (its <c>/t/&lt;slug&gt;</c> URL and the <c>&lt;slug&gt;</c> in invite links), exactly
/// like the profile handle. The display <see cref="Name"/> is free text and need NOT be
/// unique. See specs/005-team-space/data-model.md.
/// </summary>
/// <remarks>
/// <see cref="Slug"/> is set once at creation (init-only) and there is no service or
/// endpoint that mutates it; uniqueness is enforced by a unique index.
/// </remarks>
public sealed class Team : BaseEntity
{
    /// <summary>Unique, URL-safe, IMMUTABLE address (<c>/t/&lt;slug&gt;</c>). Set once at creation.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Free-text display name; not required to be unique.</summary>
    public string Name { get; set; } = string.Empty;

    public TeamType Type { get; set; }

    /// <summary>Home city — required for <see cref="TeamType.CityTeam"/>, null for a Mixteam.</summary>
    public string? City { get; set; }

    /// <summary>
    /// Self-managed recruitment signal (feature 007). When set, the team is flagged as open
    /// to new/beginner players — surfaced as the "Beginners" browse chip and filter. Editable
    /// by a team admin; defaults to false.
    /// </summary>
    public bool BeginnersWelcome { get; set; }

    public ICollection<TeamMembership> Memberships { get; set; } = [];

    public ICollection<TeamInvitation> Invitations { get; set; } = [];

    public ICollection<TeamNewsPost> News { get; set; } = [];
}
