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

/// <summary>Outcome of an attempt to edit a team news post (feature 057).</summary>
public enum TeamNewsEditStatus
{
    /// <summary>Saved — or the text was already the same, in which case nothing was written.</summary>
    Updated,
    NotFoundOrNotMember,
    Forbidden,
    /// <summary>No such post in this team: deleted, never existed, or another team's.</summary>
    PostNotFound,
}

/// <summary>The post as it now stands, plus the outcome.</summary>
public sealed record TeamNewsEditResult(TeamNewsEditStatus Status, TeamNewsDto? Post);

/// <summary>
/// Team news feed. Reading is member-scoped; posting (feature 010) is admin-only and fans out an
/// in-app notification to every other current member; editing and deleting (feature 057) are
/// open to any current admin, for any post, and notify nobody.
/// </summary>
public interface ITeamNewsService
{
    /// <summary>News posts for a team (members only, newest-first, paginated); null if unknown/not a member.</summary>
    Task<PagedResult<TeamNewsDto>?> GetFeedAsync(string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Post a news update (admin-only); persists it and notifies the rest of the roster.</summary>
    Task<TeamNewsPostResult> PostAsync(string slug, Guid actorUserId, string body, CancellationToken ct = default);

    /// <summary>
    /// Replace a post's text (any current admin, any post). The post keeps its author, date and
    /// place in the feed and is marked edited. Nobody is notified. Text equal to the current text
    /// (after trimming) writes nothing.
    /// </summary>
    Task<TeamNewsEditResult> EditAsync(string slug, Guid postId, Guid actorUserId, string body, CancellationToken ct = default);
}
