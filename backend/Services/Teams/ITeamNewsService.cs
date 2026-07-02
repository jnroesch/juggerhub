using JuggerHub.Common;
using JuggerHub.Dtos.Teams;

namespace JuggerHub.Services.Teams;

/// <summary>
/// The read-only team news feed. Creating/editing posts (the composer) is a later iteration;
/// posts are seeded in Development for now.
/// </summary>
public interface ITeamNewsService
{
    /// <summary>News posts for a team (members only, newest-first, paginated); null if unknown/not a member.</summary>
    Task<PagedResult<TeamNewsDto>?> GetFeedAsync(string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default);
}
