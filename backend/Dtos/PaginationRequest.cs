namespace JuggerHub.Dtos;

/// <summary>
/// Shared paging input for list-returning endpoints (constitution Principle III).
/// Bound from the query string. List endpoints MUST accept this and page rather
/// than return unbounded collections.
/// </summary>
/// <remarks>
/// Provided as a reusable primitive for future features; no list endpoint ships
/// in the walking skeleton, so it is not yet exercised.
/// </remarks>
public sealed record PaginationRequest
{
    public const int MaxTake = 100;
    public const int DefaultTake = 20;

    public int Skip { get; init; } = 0;

    public int Take { get; init; } = DefaultTake;

    /// <summary>Negative skip is clamped to 0.</summary>
    public int NormalizedSkip => Skip < 0 ? 0 : Skip;

    /// <summary>Take outside (0, <see cref="MaxTake"/>] falls back to <see cref="DefaultTake"/>.</summary>
    public int NormalizedTake => Take is <= 0 or > MaxTake ? DefaultTake : Take;
}
