namespace JuggerHub.Dtos.Admin;

/// <summary>
/// The admin landing aggregate (feature 013 US2, wireframe 1b): four live counts and
/// the two lists that lead into the admin's real jobs. One round trip.
/// </summary>
public sealed record AdminOverviewDto(
    int Players,
    int Teams,
    int EventsLast30Days,
    int Suspended,
    IReadOnlyList<AdminNewPlayerDto> NewPlayers,
    IReadOnlyList<AdminRecentGrantDto> RecentGrants);

/// <summary>A recently registered player (this week), linking into the admin detail.</summary>
public sealed record AdminNewPlayerDto(
    string Handle,
    string DisplayName,
    string? Hometown,
    DateTime JoinedAt);

/// <summary>
/// A recently granted badge/achievement with attribution. <see cref="SubjectHandle"/> is
/// null for team awards (012 supports both subjects); the client links into the player
/// detail only when a handle is present.
/// </summary>
public sealed record AdminRecentGrantDto(
    string Kind,
    string Name,
    string? SubjectHandle,
    string SubjectDisplayName,
    string GrantedByDisplayName,
    DateTime GrantedAt);
