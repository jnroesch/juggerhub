namespace JuggerHub.Entities;

/// <summary>
/// One match of a tournament imported from Tugeny (feature 050). There is no hand entry of match
/// results; a hand-entered ranking has no matches.
/// </summary>
/// <remarks>
/// Each side keeps a name snapshot plus a SetNull reference to a <see cref="TournamentPlacement"/>
/// of the same result, resolved at import through Tugeny's team id. Connecting that placement
/// therefore links its matches too, with no per-match action — the same entity, not another
/// placement. Tugeny's zoneless local timestamps are not stored; they only decide
/// <see cref="SortIndex"/>.
/// </remarks>
public sealed class TournamentMatch : BaseEntity
{
    public Guid TournamentResultId { get; set; }

    /// <summary>Order by Tugeny's timestamp, then its response order. The only order used.</summary>
    public int SortIndex { get; set; }

    /// <summary>Tugeny's group name (e.g. "Group 1"); null for knockout matches.</summary>
    public string? Stage { get; set; }

    /// <summary>Tugeny's match name, which carries the round (e.g. "QF1 1-8", "F 1-2").</summary>
    public string Name { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string SecondName { get; set; } = string.Empty;

    public Guid? FirstPlacementId { get; set; }

    public Guid? SecondPlacementId { get; set; }

    /// <summary>Points per set for the first side; empty when Tugeny's score text was unreadable.</summary>
    public int[] FirstScores { get; set; } = [];

    /// <summary>Points per set for the second side; same length as <see cref="FirstScores"/>.</summary>
    public int[] SecondScores { get; set; } = [];

    public MatchWinner Winner { get; set; }

    public TournamentResult TournamentResult { get; set; } = null!;

    public TournamentPlacement? FirstPlacement { get; set; }

    public TournamentPlacement? SecondPlacement { get; set; }
}
