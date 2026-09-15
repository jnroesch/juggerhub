using System.ComponentModel.DataAnnotations;

namespace JuggerHub.Dtos.Admin;

// Feature 050 — the platform admins' queue of tournament placements to connect to teams.

/// <summary>
/// One placement in the admin queue: where and how it placed, the name exactly as recorded
/// (<see cref="SourceName"/>, what a connection is judged against), and its current connection.
/// </summary>
public sealed record AdminPlacementDto(
    Guid Id,
    Guid EventId,
    string EventName,
    DateOnly EventDate,
    int Position,
    int RankedCount,
    string SourceName,
    string Name,
    AdminPlacementTeamDto? Team,
    string? ConnectedBy,
    DateTime? ConnectedAt,
    bool FromTugeny);

/// <summary>The team a placement is connected to.</summary>
public sealed record AdminPlacementTeamDto(string Slug, string Name);

/// <summary>Connect one placement to one existing team.</summary>
public sealed record ConnectTeamRequest([Required, MaxLength(30)] string TeamSlug);
