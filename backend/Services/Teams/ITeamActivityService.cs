using JuggerHub.Common;
using JuggerHub.Dtos.Profile;

namespace JuggerHub.Services.Teams;

/// <summary>
/// The events a team has taken part in (the team-side counterpart of profile activity),
/// derived from real <c>EventParticipation.TeamId</c> rows and projected to the shared
/// <see cref="ActivityItemDto"/> so the DTO shape matches the profile.
/// </summary>
public interface ITeamActivityService
{
    /// <summary>Recent events for a team (members only, newest-first, paginated); null if unknown/not a member.</summary>
    Task<PagedResult<ActivityItemDto>?> GetForTeamAsync(string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default);
}
