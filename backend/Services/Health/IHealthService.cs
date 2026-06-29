using JuggerHub.Dtos;

namespace JuggerHub.Services.Health;

/// <summary>
/// Reports overall system health, including live database reachability.
/// </summary>
public interface IHealthService
{
    /// <summary>
    /// Returns the current health. Degrades gracefully to "unhealthy" /
    /// "unreachable" when the database cannot be reached — never throws (FR-004).
    /// </summary>
    Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken = default);
}
