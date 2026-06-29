namespace JuggerHub.Dtos;

/// <summary>
/// Transient read model for the public health endpoint. Describes overall status
/// and database reachability so the dashboard can render the live condition.
/// </summary>
/// <param name="Status">"healthy" | "degraded" | "unhealthy".</param>
/// <param name="Database">"reachable" | "unreachable".</param>
/// <param name="Version">The API assembly's informational version (diagnostics).</param>
/// <param name="Timestamp">When the check ran (UTC).</param>
public sealed record HealthDto(
    string Status,
    string Database,
    string Version,
    DateTime Timestamp);
