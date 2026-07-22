using JuggerHub.Common;
using JuggerHub.Dtos.Home;

namespace JuggerHub.Services.Home;

/// <summary>
/// Composes the logged-in Home dashboard from existing data (feature 008, reshaped by feature 025).
/// Every read is entitlement-scoped to the caller server-side: their own sign-ups + trainings, their
/// pending actionable items, their member teams' + parties' news, connected-event news, and passive
/// activity around them. Read-only — every action reuses the existing per-domain endpoints.
/// </summary>
public interface IHomeService
{
    /// <summary>The composite dashboard: viewer summary + capped top-N per section.</summary>
    Task<HomeDto> GetHomeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The caller's full upcoming participation agenda ("see all"), soonest-first, paginated.</summary>
    Task<PagedResult<AgendaItemDto>> ListUpNextAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The caller's aggregated news feed ("see all"), newest-first, paginated within the window.</summary>
    Task<PagedResult<HomeNewsDto>> ListNewsAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The caller's team memberships (drives the nav "My team").</summary>
    Task<PagedResult<MyTeamDto>> ListMyTeamsAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default);
}
