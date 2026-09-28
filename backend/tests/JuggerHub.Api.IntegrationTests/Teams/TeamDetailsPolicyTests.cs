using JuggerHub.Services.Teams;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Feature 061, research R6: what a team's description and links may be. Pure — the rules never
/// touch the database, so neither do these tests.
/// </summary>
public sealed class TeamDetailsPolicyTests
{
    // --- Addresses ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("instagram.com/elbwoelfe", "https://instagram.com/elbwoelfe")]
    [InlineData("  https://elbwoelfe.de  ", "https://elbwoelfe.de/")]
    [InlineData("HTTPS://Elbwoelfe.DE/Training", "https://elbwoelfe.de/Training")]
    [InlineData("www.instagram.com/x", "https://www.instagram.com/x")]
    // A scheme-less address whose query contains "https://" is still scheme-less (the "://" check
    // this replaces would have refused it).
    [InlineData("a.de/?u=https://b.de", "https://a.de/?u=https://b.de")]
    [InlineData("https://discord.gg/abc123", "https://discord.gg/abc123")]
    public void An_address_is_stored_as_a_normalised_secure_address(string input, string expected)
    {
        Assert.Equal(expected, TeamDetailsPolicy.NormalizeUrl(input));
    }

    [Theory]
    [InlineData("http://elbwoelfe.de")]
    [InlineData("javascript:alert(1)")]
    [InlineData("javascript://alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("mailto:team@elbwoelfe.de")]
    [InlineData("data:text/html,<b>x</b>")]
    [InlineData("ftp://elbwoelfe.de")]
    // A user name in front of the host: this leads to example.net, not to Instagram.
    [InlineData("https://instagram.com@example.net")]
    [InlineData("https://user:pass@elbwoelfe.de")]
    // No real site: no dot, or an IP literal.
    [InlineData("https://localhost")]
    [InlineData("elbwoelfe")]
    [InlineData("https://192.168.0.1/x")]
    [InlineData("https://[::1]/x")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_non_secure_or_disguised_address_is_refused(string? input)
    {
        Assert.Null(TeamDetailsPolicy.NormalizeUrl(input));
    }

    [Fact]
    public void An_address_longer_than_500_characters_is_refused()
    {
        var fits = "https://a.de/" + new string('x', 500 - "https://a.de/".Length);
        var tooLong = fits + "x";

        Assert.Equal(fits, TeamDetailsPolicy.NormalizeUrl(fits));
        Assert.Null(TeamDetailsPolicy.NormalizeUrl(tooLong));
    }

    [Fact]
    public void An_internationalised_host_is_accepted()
    {
        var url = TeamDetailsPolicy.NormalizeUrl("https://bücher.de/team");

        Assert.NotNull(url);
        Assert.Equal("https", new Uri(url!).Scheme);
    }

    // --- Labels ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("  Instagram  ", "Instagram")]
    [InlineData("Über uns", "Über uns")]
    [InlineData("123456789012345678901234567890", "123456789012345678901234567890")]
    public void A_label_is_trimmed_and_kept(string input, string expected)
    {
        Assert.Equal(expected, TeamDetailsPolicy.NormalizeLabel(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("Insta\ngram")]
    [InlineData("Tab\there")]
    // A right-to-left override would visually reverse the host shown next to the label (FR-017).
    [InlineData("Instagram‮")]
    [InlineData("⁧Discord")]
    [InlineData("Web‏site")]
    public void A_blank_long_or_control_bearing_label_is_refused(string? input)
    {
        Assert.Null(TeamDetailsPolicy.NormalizeLabel(input));
    }

    // --- The list ----------------------------------------------------------------------------------

    [Fact]
    public void Links_keep_their_order_and_come_back_normalised()
    {
        var (links, problem) = TeamDetailsPolicy.NormalizeLinks(
        [
            ("Website", "https://elbwoelfe.de"),
            (" Instagram ", "instagram.com/elbwoelfe"),
            ("Discord", "discord.gg/abc"),
        ]);

        Assert.Null(problem);
        Assert.Equal(
            ["Website|https://elbwoelfe.de/", "Instagram|https://instagram.com/elbwoelfe", "Discord|https://discord.gg/abc"],
            links.Select(l => $"{l.Label}|{l.Url}"));
    }

    [Fact]
    public void No_links_is_an_empty_list()
    {
        Assert.Empty(TeamDetailsPolicy.NormalizeLinks(null).Links);
        Assert.Empty(TeamDetailsPolicy.NormalizeLinks([]).Links);
    }

    [Fact]
    public void Six_links_are_refused()
    {
        var six = Enumerable.Range(0, 6).Select(i => ((string?)$"L{i}", (string?)$"https://a{i}.de")).ToList();

        var (_, problem) = TeamDetailsPolicy.NormalizeLinks(six);

        Assert.Equal(TeamDetailsCode.TooManyLinks, problem?.Code);
        Assert.Null(problem?.LinkIndex);
        Assert.Null(TeamDetailsPolicy.NormalizeLinks(six.Take(5).ToList()).Problem);
    }

    [Fact]
    public void The_offending_link_is_named()
    {
        var (_, label) = TeamDetailsPolicy.NormalizeLinks([("Website", "https://a.de"), ("", "https://b.de")]);
        var (_, url) = TeamDetailsPolicy.NormalizeLinks([("Website", "https://a.de"), ("Blog", "http://b.de")]);

        Assert.Equal((TeamDetailsCode.LinkLabelInvalid, 1), (label!.Code, label.LinkIndex!.Value));
        Assert.Equal((TeamDetailsCode.LinkUrlInvalid, 1), (url!.Code, url.LinkIndex!.Value));
    }

    [Fact]
    public void The_same_address_twice_is_refused_on_the_later_link()
    {
        // Same site after normalisation: the host's case and a missing scheme do not make it new.
        var (_, problem) = TeamDetailsPolicy.NormalizeLinks(
        [
            ("Website", "https://elbwoelfe.de/x"),
            ("Blog", "https://blog.elbwoelfe.de"),
            ("Again", "ELBWOELFE.de/x"),
        ]);

        Assert.Equal(TeamDetailsCode.LinkDuplicate, problem?.Code);
        Assert.Equal(2, problem?.LinkIndex);
    }

    [Fact]
    public void A_path_differing_only_in_case_is_a_different_address()
    {
        var (links, problem) = TeamDetailsPolicy.NormalizeLinks([("A", "https://a.de/X"), ("B", "https://a.de/x")]);

        Assert.Null(problem);
        Assert.Equal(2, links.Count);
    }

    // --- Description -------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  \r\n ")]
    public void A_blank_description_is_no_description(string? input)
    {
        Assert.Null(TeamDetailsPolicy.NormalizeDescription(input));
    }

    [Fact]
    public void A_description_keeps_its_line_breaks_and_loses_its_surrounding_space()
    {
        Assert.Equal("Founded 2019.\n\nWe train twice a week.",
            TeamDetailsPolicy.NormalizeDescription("  Founded 2019.\n\nWe train twice a week.\n "));
    }

    [Fact]
    public void A_description_may_be_1000_characters_and_no_more()
    {
        Assert.Null(TeamDetailsPolicy.ValidateDescription(new string('a', 1000)));
        Assert.Equal(TeamDetailsCode.DescriptionTooLong,
            TeamDetailsPolicy.ValidateDescription(new string('a', 1001))?.Code);
    }
}
