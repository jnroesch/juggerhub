using JuggerHub.Common;
using JuggerHub.Dtos.Home;

namespace JuggerHub.Services.Home;

/// <summary>
/// Composes the logged-in Home dashboard from existing data (feature 008). Every read is
/// entitlement-scoped to the caller server-side: their own sign-ups, their member teams'
/// news/activity, connected-event news, and public tournaments. Read-only — no writes.
/// </summary>
public interface IHomeService
{
    /// <summary>The composite dashboard: viewer summary + capped top-N per module.</summary>
    Task<HomeDto> GetHomeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The caller's full upcoming-events list ("see all"), soonest-first, paginated.</summary>
    Task<PagedResult<UpNextItemDto>> ListUpNextAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The caller's aggregated news feed ("see all"), newest-first, paginated within the window.</summary>
    Task<PagedResult<HomeNewsDto>> ListNewsAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The caller's team memberships (drives the nav "My team" + snapshots).</summary>
    Task<PagedResult<MyTeamDto>> ListMyTeamsAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default);
}
