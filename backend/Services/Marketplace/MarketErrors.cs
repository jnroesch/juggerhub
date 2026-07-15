using Npgsql;

namespace JuggerHub.Services.Marketplace;

/// <summary>Shared low-level error probes for the marketplace services (feature 017).</summary>
internal static class MarketErrors
{
    /// <summary>True when an <see cref="Exception"/> is (or wraps) a Postgres unique-violation.</summary>
    public static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
