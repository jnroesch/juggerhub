using JuggerHub.Entities;

namespace JuggerHub.Dtos.Admin;

/// <summary>
/// One row of the admin users list (feature 013 US3, wireframe 1c). Unlike every
/// player-facing surface, this list includes Banned accounts — the one place a
/// soft-deleted player remains findable so a mistaken ban can be undone.
/// </summary>
public sealed record AdminUserListItemDto(
    string Handle,
    string DisplayName,
    AccountStatus Status,
    bool IsAdmin,
    IReadOnlyList<string> Teams,
    int BadgeCount,
    DateTime JoinedAt);

/// <summary>A team the player belongs to, linkable from the admin detail.</summary>
public sealed record AdminUserTeamDto(string Name, string Slug);

/// <summary>One recent-activity line on the admin player detail (feature-003 data).</summary>
public sealed record AdminActivityItemDto(string Title, DateTime Date);

/// <summary>
/// Everything an admin needs about one player (feature 013 US4, wireframe 1d).
/// <see cref="UserId"/> is the wireframe's "player id"; <see cref="LastActiveAt"/> and
/// <see cref="RecentActivity"/> derive from existing participation data (no new tracking).
/// </summary>
public sealed record AdminUserDetailDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    string? Hometown,
    DateTime JoinedAt,
    AccountStatus Status,
    DateTime? StatusChangedAt,
    bool IsAdmin,
    IReadOnlyList<AdminUserTeamDto> Teams,
    IReadOnlyList<Pompfe> Pompfen,
    DateTime? LastActiveAt,
    IReadOnlyList<AdminActivityItemDto> RecentActivity);
