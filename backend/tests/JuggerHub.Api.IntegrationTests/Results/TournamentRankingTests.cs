using JuggerHub.Services.Results;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>
/// Standard competition ranking (research R8): the admin types positions; the stored position is
/// always the one displayed, so a tie is followed by a skip (FR-003).
/// </summary>
public sealed class TournamentRankingTests
{
    private static int[] Positions(params int[] submitted) =>
        TournamentRanking.Normalize(submitted).Select(r => r.Position).ToArray();

    [Fact]
    public void A_tie_is_followed_by_a_skip() =>
        Assert.Equal([1, 2, 3, 3, 5], Positions(1, 2, 3, 3, 4));

    [Fact]
    public void Everyone_tied_shares_first_place() =>
        Assert.Equal([1, 1, 1], Positions(2, 2, 2));

    [Fact]
    public void Gaps_collapse_to_consecutive_places() =>
        Assert.Equal([1, 2, 3], Positions(1, 5, 9));

    [Fact]
    public void A_single_row_is_first() =>
        Assert.Equal([1], Positions(7));

    [Fact]
    public void Results_map_back_to_the_submitted_rows_whatever_their_order()
    {
        // Rows arrive out of order; each result belongs to the row at the same index.
        var ranked = TournamentRanking.Normalize([3, 1, 2]);

        Assert.Equal([3, 1, 2], ranked.Select(r => r.Position).ToArray());
        Assert.Equal([2, 0, 1], ranked.Select(r => r.SortIndex).ToArray());
    }

    [Fact]
    public void Order_within_a_tie_keeps_the_submission_order()
    {
        var ranked = TournamentRanking.Normalize([2, 1, 2]);

        // Row 0 and row 2 share second place; row 0 was submitted first, so it sorts first.
        Assert.Equal(2, ranked[0].Position);
        Assert.Equal(2, ranked[2].Position);
        Assert.True(ranked[0].SortIndex < ranked[2].SortIndex);
    }
}
