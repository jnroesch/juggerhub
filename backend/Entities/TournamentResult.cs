namespace JuggerHub.Entities;

/// <summary>
/// The results of one tournament <see cref="Event"/> — its ranking, the matches of an imported
/// tournament, where they came from, and the event's link to its Tugeny tournament (feature 050).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifecycle.</b> Created by the first action that needs it — linking a Tugeny tournament,
/// saving a ranking, or committing an import. Never deleted by this feature: clearing the results
/// removes <see cref="Placements"/> and <see cref="Matches"/> and keeps the row, so a Tugeny link
/// survives a clear, and unlinking nulls only the four <c>Tugeny*</c> columns, so saved results
/// survive an unlink.
/// </para>
/// <para>
/// Never written into <see cref="EventParticipation"/>: that table is per profile and feeds profile
/// activity and the team "active" flag, which recording a result must not switch on.
/// </para>
/// </remarks>
public sealed class TournamentResult : BaseEntity
{
    /// <summary>The tournament event; unique — one result per event.</summary>
    public Guid EventId { get; set; }

    public ResultSource Source { get; set; } = ResultSource.None;

    /// <summary>When the last import was committed (UTC); null unless imported.</summary>
    public DateTime? ImportedAt { get; set; }

    /// <summary>True once an event admin changes an imported ranking by hand (FR-017).</summary>
    public bool EditedSinceImport { get; set; }

    /// <summary>
    /// Who last changed the ranking or matches. Restrict: account erasure (037) neutralises the
    /// user row rather than deleting it, so the reference survives and projects to a placeholder.
    /// </summary>
    public Guid? LastChangedByUserId { get; set; }

    /// <summary>
    /// When the ranking or matches last changed (UTC), shown on the event (FR-007). Distinct from
    /// <see cref="BaseEntity.ModifiedDate"/>, which linking and unlinking also touch.
    /// </summary>
    public DateTime? ResultsChangedAt { get; set; }

    /// <summary>Tugeny's own tournament id; null when the event is not linked.</summary>
    public int? TugenyTournamentId { get; set; }

    /// <summary>Tugeny's tournament slug, used to build the live and tournament links.</summary>
    public string? TugenySlug { get; set; }

    /// <summary>The Tugeny tournament's name as last seen, shown to confirm the link.</summary>
    public string? TugenyName { get; set; }

    /// <summary>The Tugeny tournament's start date as last seen, shown to confirm the link.</summary>
    public DateOnly? TugenyStartDate { get; set; }

    public Event Event { get; set; } = null!;

    public User? LastChangedBy { get; set; }

    public ICollection<TournamentPlacement> Placements { get; set; } = [];

    public ICollection<TournamentMatch> Matches { get; set; } = [];
}
