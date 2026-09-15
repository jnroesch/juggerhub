namespace JuggerHub.Services.Results;

/// <summary>
/// Normalises submitted positions to standard competition ranking — 1, 2, 3, 3, 5 — so the stored
/// position is always the displayed one and a tie is always followed by a skip (feature 050 FR-003,
/// research R8). Pure; no I/O.
/// </summary>
public static class TournamentRanking
{
    /// <summary>
    /// Ranks <paramref name="submitted"/> positions. The result at index <c>i</c> belongs to the row
    /// at index <c>i</c>: its displayed <c>Position</c>, and a <c>SortIndex</c> that orders the whole
    /// ranking — by submitted position, then by submission order within a tie.
    /// </summary>
    /// <remarks>
    /// The submitted numbers only express order and ties; gaps collapse (1, 5, 9 ⇒ 1, 2, 3). A row's
    /// place is one plus the number of rows placed strictly ahead of it.
    /// </remarks>
    public static IReadOnlyList<(int Position, int SortIndex)> Normalize(IReadOnlyList<int> submitted)
    {
        var order = Enumerable.Range(0, submitted.Count)
            .OrderBy(i => submitted[i])
            .ThenBy(i => i)
            .ToArray();

        var result = new (int Position, int SortIndex)[submitted.Count];
        for (var sorted = 0; sorted < order.Length; sorted++)
        {
            var row = order[sorted];
            var tiedWithPrevious = sorted > 0 && submitted[order[sorted - 1]] == submitted[row];
            var position = tiedWithPrevious ? result[order[sorted - 1]].Position : sorted + 1;
            result[row] = (position, sorted);
        }

        return result;
    }
}
