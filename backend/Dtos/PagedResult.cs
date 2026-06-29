namespace JuggerHub.Dtos;

/// <summary>
/// Shared paged-list envelope for list-returning endpoints (constitution
/// Principle III). Uniform shape so every future list endpoint reports its page
/// the same way.
/// </summary>
/// <param name="Items">The page of results.</param>
/// <param name="TotalCount">Total matching rows before paging.</param>
/// <param name="Skip">The applied skip (echoed).</param>
/// <param name="Take">The applied take (echoed).</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Skip,
    int Take);
