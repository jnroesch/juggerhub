using JuggerHub.Common;
using JuggerHub.Dtos.Admin;

namespace JuggerHub.Services.Admin;

/// <summary>
/// Admin team browse for award assignment (feature 014): a paginated search over teams and a
/// per-team detail. The team's awards + grant/revoke reuse feature-012's existing team-awards
/// read and <c>teamSlug</c> grant/revoke endpoints. Callers are gated by the
/// <c>PlatformAdmin</c> policy at the controller.
/// </summary>
public interface IAdminTeamService
{
    Task<PagedResult<AdminTeamListItemDto>> SearchAsync(
        string? q, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The team's admin detail; null if no team matches that slug.</summary>
    Task<AdminTeamDetailDto?> GetDetailAsync(string slug, CancellationToken ct = default);
}
