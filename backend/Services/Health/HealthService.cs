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

    private async Task<bool> CanReachDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken);
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
