using System.ComponentModel.DataAnnotations;
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Results;

// Feature 050 — tournament results. Shapes: specs/050-tournament-results/contracts/results-api.md.
// Validation attributes go on record constructor parameters (MVC reads parameter-level metadata for
// positional records). The rules that need the database — the signed-up-team rule, the unchanged-
// connection rule, the 128 cap, one placement per team — are enforced in TournamentResultService.

// --- Reads (any signed-in user) ---------------------------------------------------------------

/// <summary>The results section of a tournament event.</summary>
public sealed record TournamentResultDto(
    ResultSource Source,
    DateTime? ImportedAt,
    bool EditedSinceImport,
    DateTime? ResultsChangedAt,
    TugenyLinkDto? Tugeny,
    IReadOnlyList<PlacementDto> Placements,
    int RankedCount,
    int MatchCount,
    ResultViewerDto Viewer);

/// <summary>A linked Tugeny tournament, with the links JuggerHub builds from its slug.</summary>
public sealed record TugenyLinkDto(
    int TournamentId,
    string Slug,
    string Name,
    DateOnly? StartDate,
    string LiveUrl,
    string TournamentUrl);

/// <summary>One placement as everyone sees it. Connection attribution is for admins only.</summary>
public sealed record PlacementDto(Guid Id, int Position, string Name, string? TeamSlug);

/// <summary>What the viewer may do with the results.</summary>
public sealed record ResultViewerDto(bool CanEdit);

/// <summary>One imported match.</summary>
public sealed record TournamentMatchDto(
    Guid Id,
    string? Stage,
    string Name,
    MatchSideDto First,
    MatchSideDto Second,
    IReadOnlyList<int> FirstScores,
    IReadOnlyList<int> SecondScores,
    MatchWinner Winner);

/// <summary>One side of a match; <see cref="TeamSlug"/> is set when its placement is connected.</summary>
public sealed record MatchSideDto(string Name, string? TeamSlug);

/// <summary>A team's placement in one tournament, for the team page.</summary>
public sealed record TeamPlacementDto(
    Guid EventId,
    string EventName,
    DateOnly Date,
    int Position,
    int RankedCount);

// --- The results page (event admins) ----------------------------------------------------------

/// <summary>Everything the results page needs, for an admin of the event.</summary>
public sealed record ResultEditorDto(
    Guid EventId,
    string EventName,
    ParticipantMode ParticipantMode,
    bool IsTournament,
    bool IsCancelled,
    bool HasStarted,
    bool HasEnded,
    bool CanRecord,
    ResultSource Source,
    DateTime? ImportedAt,
    bool EditedSinceImport,
    DateTime? ResultsChangedAt,
    TugenyLinkDto? Tugeny,
    bool LinkedElsewhere,
    IReadOnlyList<EditorPlacementDto> Placements,
    IReadOnlyList<SignedUpTeamDto> SignedUpTeams);

/// <summary>
/// One placement as an event admin sees it: the name as recorded and who connected it. A row whose
/// team is not among <see cref="ResultEditorDto.SignedUpTeams"/> was connected by a platform admin;
/// the event admin can keep it (by sending its <see cref="Id"/> back) but never re-point it.
/// </summary>
public sealed record EditorPlacementDto(
    Guid Id,
    int Position,
    string Name,
    string SourceName,
    Guid? TeamId,
    string? TeamSlug,
    string? ConnectedBy,
    DateTime? ConnectedAt);

/// <summary>A team with a confirmed (<c>Joined</c>) JuggerHub sign-up for the event.</summary>
public sealed record SignedUpTeamDto(Guid TeamId, string TeamSlug, string TeamName);

/// <summary>Replace the ranking. At most 128 rows; the service re-checks everything.</summary>
public sealed record SaveRankingRequest([Required] IReadOnlyList<RankingRowRequest> Placements);

/// <summary>
/// One ranking row. <see cref="Id"/> identifies an existing placement to keep (and, with it, a
/// connection a platform admin made). <see cref="Position"/> is what the admin typed; the stored
/// position is normalised to standard competition ranking.
/// </summary>
public sealed record RankingRowRequest(
    Guid? Id,
    [Range(1, 999)] int Position,
    [Required, MaxLength(200)] string Name,
    Guid? TeamId);

// --- Tugeny ----------------------------------------------------------------------------------

/// <summary>A tugeny.org tournament address, or a bare slug.</summary>
public sealed record LinkTugenyRequest([Required, MaxLength(500)] string Address);

/// <summary>The outcome of linking: the Tugeny tournament to confirm, and a duplicate warning.</summary>
public sealed record TugenyLinkedDto(
    int TournamentId,
    string Slug,
    string Name,
    DateOnly? StartDate,
    bool LinkedElsewhere);

/// <summary>A stateless draft of a Tugeny import — nothing is saved.</summary>
public sealed record TugenyImportPreviewDto(
    string TournamentName,
    IReadOnlyList<ImportPlacementDto> Placements,
    int MatchCount,
    IReadOnlyList<SignedUpTeamDto> SignedUpTeams,
    bool ReplacesExisting);

/// <summary>One placement in an import draft, keyed by Tugeny's own team id.</summary>
public sealed record ImportPlacementDto(int Position, string Name, int TugenyTeamId);

/// <summary>Commit an import. Each connection is an explicit choice by the admin; may be empty.</summary>
public sealed record ImportCommitRequest(IReadOnlyList<ImportConnectionRequest>? Connections);

/// <summary>Connect one imported Tugeny team to one signed-up JuggerHub team, for this commit only.</summary>
public sealed record ImportConnectionRequest(int TugenyTeamId, Guid TeamId);
