using JuggerHub.Services.Results;

namespace JuggerHub.Api.IntegrationTests.Results;

/// <summary>
/// Research R6: the pasted address is the only user input that reaches an outbound request, and only
/// a validated slug survives it — the host is always configuration.
/// </summary>
public sealed class TugenyLinkParserTests
{
    [Theory]
    [InlineData("https://tugeny.org/tournaments/25-deutsche-meisterschaft")]
    [InlineData("https://tugeny.org/tournaments/25-deutsche-meisterschaft/")]
    [InlineData("https://tugeny.org/tournaments/25-deutsche-meisterschaft/all-teams")]
    [InlineData("https://tugeny.org/tournaments/25-deutsche-meisterschaft/live-view")]
    [InlineData("https://tugeny.org/tournaments/25-deutsche-meisterschaft/tournament-tree")]
    [InlineData("http://tugeny.org/tournaments/25-deutsche-meisterschaft")]
    [InlineData("https://www.tugeny.org/tournaments/25-deutsche-meisterschaft")]
    [InlineData("  https://tugeny.org/tournaments/25-deutsche-meisterschaft  ")]
    [InlineData("25-deutsche-meisterschaft")]
    public void A_tugeny_tournament_address_or_slug_yields_the_slug(string input)
    {
        Assert.True(TugenyLinkParser.TryParseSlug(input, out var slug));
        Assert.Equal("25-deutsche-meisterschaft", slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("https://evil.example/tournaments/25-deutsche-meisterschaft")]
    [InlineData("https://tugeny.org.evil.example/tournaments/x")]
    [InlineData("https://user@tugeny.org/tournaments/x")]
    [InlineData("https://tugeny.org/api/persistent/teams")]
    [InlineData("https://tugeny.org/tournaments")]
    [InlineData("ftp://tugeny.org/tournaments/x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("under_score")]
    [InlineData("per%20cent")]
    [InlineData("Upper-Case")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--dash")]
    [InlineData("../tournaments")]
    public void Anything_else_is_refused(string? input) =>
        Assert.False(TugenyLinkParser.TryParseSlug(input, out _));

    [Fact]
    public void A_slug_longer_than_150_characters_is_refused()
    {
        Assert.True(TugenyLinkParser.TryParseSlug(new string('a', 150), out _));
        Assert.False(TugenyLinkParser.TryParseSlug(new string('a', 151), out _));
    }
}
