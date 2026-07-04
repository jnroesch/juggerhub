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

// Uniform paged-result envelope so list contracts stay consistent. NOTE: intentionally
// NOT an XML doc comment — the Microsoft.AspNetCore.OpenApi 10.0.9 XML-comment source
// generator crashes ("duplicate key PagedResult`1") when this generic type carries a doc
// comment and is referenced by many endpoints/records. Remove this note once the framework
// bug is fixed (present on eb1307e, before feature 006).
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Skip, int Take);
