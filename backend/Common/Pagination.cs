namespace JuggerHub.Common;

/// <summary>
/// Shared pagination request (constitution Principle III — every list endpoint
/// paginates via Skip/Take with a hard maximum page size). Bound from the query
/// string; use the normalized accessors so out-of-range input is clamped.
/// </summary>
public sealed record PaginationRequest
{
    private const int MaxTake = 100;
    private const int DefaultTake = 20;

    public int Skip { get; init; } = 0;
    public int Take { get; init; } = DefaultTake;

    public int NormalizedSkip => Skip < 0 ? 0 : Skip;
    public int NormalizedTake => Take is <= 0 or > MaxTake ? DefaultTake : Take;
}

/// <summary>Uniform paged-result envelope so list contracts stay consistent.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Skip, int Take);
