using System.Reflection;
using JuggerHub.Data;
using JuggerHub.Dtos;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Health;

/// <summary>
/// Determines health by probing database connectivity via EF Core's
/// <see cref="Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions"/>.
/// </summary>
public sealed class HealthService : IHealthService
{
    private static readonly string AssemblyVersion =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    private readonly AppDbContext _db;
    private readonly ILogger<HealthService> _logger;

    public HealthService(AppDbContext db, ILogger<HealthService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var databaseReachable = await CanReachDatabaseAsync(cancellationToken);

        return new HealthDto(
            Status: databaseReachable ? "healthy" : "unhealthy",
            Database: databaseReachable ? "reachable" : "unreachable",
            Version: AssemblyVersion,
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// How long the probe may spend deciding. Deliberately short — see the remarks on
    /// <see cref="CanReachDatabaseAsync"/>.
    /// </summary>
    private static readonly TimeSpan ProbeBudget = TimeSpan.FromSeconds(3);

    /// <remarks>
    /// This probe must <b>report</b> status, not survive an outage — so it deliberately opts OUT of
    /// the connection resiliency added in feature 028.
    /// <para>
    /// Found while validating that feature against a stopped database: with retry enabled,
    /// <c>CanConnectAsync</c> retries the connection for roughly 30 seconds before giving up, so
    /// the health endpoint stopped answering promptly and simply hung. That is actively harmful
    /// here. The Kubernetes liveness probe polls this endpoint with a short timeout, so a database
    /// blip would fail the probe repeatedly and get the perfectly healthy API pod **restarted** —
    /// turning a recoverable database hiccup into an application outage, the exact opposite of what
    /// this feature is for.
    /// </para>
    /// <para>
    /// Every other database path keeps the retrying strategy. Only the probe is capped, because
    /// only the probe's job is to answer quickly and truthfully.
    /// </para>
    /// </remarks>
    private async Task<bool> CanReachDatabaseAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ProbeBudget);

        try
        {
            return await _db.Database.CanConnectAsync(budget.Token);
        }
        catch (Exception ex)
        {
            // Graceful degradation: a DB-down condition is reported as unhealthy,
            // never surfaced as a raw error (FR-004). Logged server-side only.
            _logger.LogWarning(ex, "Database connectivity check failed.");
            return false;
        }
    }
}
