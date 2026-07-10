using JuggerHub.Entities;

namespace JuggerHub.Dtos.Admin;

/// <summary>
/// One row of the admin teams list (feature 014): team identity plus how many members it has and
/// how many active awards it holds. Parallel to <see cref="AdminUserListItemDto"/>.
/// </summary>
public sealed record AdminTeamListItemDto(
    string Slug,
    string Name,
    string? City,
    TeamType Type,
    int MemberCount,
    int AwardCount);

/// <summary>
/// Identity for the admin team detail header (feature 014). The team's current awards + the
/// assign/revoke controls reuse the existing <c>/admin/teams/{slug}/awards</c> read and the
/// <c>teamSlug</c> grant/revoke routes, so they are not duplicated here.
/// </summary>
public sealed record AdminTeamDetailDto(
    Guid TeamId,
    string Slug,
    string Name,
    string? City,
    TeamType Type,
    int MemberCount,
    DateTime CreatedAt);
