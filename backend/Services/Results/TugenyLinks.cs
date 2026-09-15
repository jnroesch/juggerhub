using JuggerHub.Common;
using JuggerHub.Dtos.Results;

namespace JuggerHub.Services.Results;

/// <summary>
/// Builds the tugeny.org links shown for a linked tournament (feature 050, contracts/tugeny.md §4).
/// Always from the configured base and the stored slug — never from anything a user typed.
/// </summary>
public static class TugenyLinks
{
    /// <summary>The link DTO, or null when the result is not linked.</summary>
    public static TugenyLinkDto? Build(TugenyOptions options, int? tournamentId, string? slug, string? name, DateOnly? startDate)
    {
        if (tournamentId is null || string.IsNullOrEmpty(slug))
        {
            return null;
        }

        var tournament = new Uri(options.BaseUri, $"tournaments/{Uri.EscapeDataString(slug)}/");
        return new TugenyLinkDto(
            tournamentId.Value,
            slug,
            name ?? slug,
            startDate,
            new Uri(tournament, "live-view").ToString(),
            new Uri(tournament, "all-teams").ToString());
    }
}
