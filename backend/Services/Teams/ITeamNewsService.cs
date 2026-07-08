using JuggerHub.Common;
using JuggerHub.Dtos.Teams;

namespace JuggerHub.Services.Teams;

/// <summary>Outcome of an attempt to post team news (feature 010).</summary>
public enum TeamNewsPostStatus
{
    Posted,
    NotFoundOrNotMember,
    Forbidden,
}

/// <summary>The posted news item plus the authorization outcome.</summary>
public sealed record TeamNewsPostResult(TeamNewsPostStatus Status, TeamNewsDto? Post);

/// <summary>
/// Team news feed. Reading is member-scoped; posting (feature 010) is admin-only and fans out an
/// in-app notification to every other current member.
/// </summary>
public interface ITeamNewsService
{
    /// <summary>News posts for a team (members only, newest-first, paginated); null if unknown/not a member.</summary>
    Task<PagedResult<TeamNewsDto>?> GetFeedAsync(string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Post a news update (admin-only); persists it and notifies the rest of the roster.</summary>
    Task<TeamNewsPostResult> PostAsync(string slug, Guid actorUserId, string body, CancellationToken ct = default);
}
