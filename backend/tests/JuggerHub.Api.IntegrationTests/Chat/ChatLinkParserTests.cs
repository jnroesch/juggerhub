using JuggerHub.Entities;
using JuggerHub.Services.Chat;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// Pure unit tests for link parsing (feature 019, User Story 7). No database, no network — and "no
/// network" is the point: this is where the SSRF surface of a conventional unfurl service is designed
/// out rather than mitigated (research §5).
/// </summary>
public sealed class ChatLinkParserTests
{
    private static readonly string[] Hosts = { "localhost", "jugger.app" };

    [Theory]
    [InlineData("/u/ada-k", ChatLinkKind.Player)]
    [InlineData("/t/rheinfeuer", ChatLinkKind.Team)]
    [InlineData("check this out /u/ada-k nice player", ChatLinkKind.Player)]
    [InlineData("https://jugger.app/t/rheinfeuer", ChatLinkKind.Team)]
    [InlineData("http://localhost:4200/t/rheinfeuer", ChatLinkKind.Team)]
    public void Recognises_our_own_route_shapes(string body, ChatLinkKind expected)
    {
        var link = ChatLinkParser.Parse(body, Hosts);

        Assert.Equal(expected, link.Kind);
    }

    [Fact]
    public void Recognises_an_event_link_by_id()
    {
        var id = Guid.CreateVersion7();

        var link = ChatLinkParser.Parse($"see /events/{id}", Hosts);

        Assert.Equal(ChatLinkKind.Event, link.Kind);
        Assert.Equal(id, link.Id);
    }

    [Fact]
    public void Recognises_a_training_session_link_by_id()
    {
        var id = Guid.CreateVersion7();

        var link = ChatLinkParser.Parse($"training: /trainings/sessions/{id}", Hosts);

        Assert.Equal(ChatLinkKind.Training, link.Kind);
        Assert.Equal(id, link.Id);
    }

    /// <summary>
    /// A training URL contains "/sessions/" under "/trainings/", and an event URL is "/events/{id}".
    /// Checking training first keeps one kind's id from being looked up as another's.
    /// </summary>
    [Fact]
    public void A_training_link_is_not_mistaken_for_an_event()
    {
        var id = Guid.CreateVersion7();

        var link = ChatLinkParser.Parse($"/trainings/sessions/{id}", Hosts);

        Assert.Equal(ChatLinkKind.Training, link.Kind);
    }

    /// <summary>FR-039: anything that is not one of our shapes is plain text.</summary>
    [Theory]
    [InlineData("just a normal message")]
    [InlineData("https://example.com/whatever")]
    [InlineData("https://en.wikipedia.org/wiki/Jugger")]
    [InlineData("see /admin/catalogue")]
    [InlineData("")]
    [InlineData("   ")]
    public void Anything_else_is_not_a_link(string body)
    {
        var link = ChatLinkParser.Parse(body, Hosts);

        Assert.Equal(ChatLinkKind.None, link.Kind);
    }

    /// <summary>
    /// <b>The spoofing check.</b> Someone else's host wearing our path shape must not unfurl as ours —
    /// otherwise a message could render a card that looks like our data but points at an attacker's site.
    /// </summary>
    [Theory]
    [InlineData("https://evil.example.com/t/rheinfeuer")]
    [InlineData("https://jugger.app.evil.com/u/ada-k")]
    [InlineData("http://notjugger.app/t/beavers")]
    public void An_absolute_url_on_a_foreign_host_never_unfurls(string body)
    {
        var link = ChatLinkParser.Parse(body, Hosts);

        Assert.Equal(ChatLinkKind.None, link.Kind);
    }

    [Fact]
    public void A_port_on_our_own_host_is_still_ours()
    {
        var link = ChatLinkParser.Parse("http://localhost:4200/u/ada-k", Hosts);

        Assert.Equal(ChatLinkKind.Player, link.Kind);
        Assert.Equal("ada-k", link.Slug);
    }

    [Fact]
    public void Extracts_the_handle_and_the_slug()
    {
        Assert.Equal("ada-k", ChatLinkParser.Parse("/u/ada-k", Hosts).Slug);
        Assert.Equal("rheinfeuer", ChatLinkParser.Parse("/t/rheinfeuer", Hosts).Slug);
    }

    /// <summary>The first recognised link wins; a message is one card at most.</summary>
    [Fact]
    public void Only_the_first_recognised_link_is_taken()
    {
        var link = ChatLinkParser.Parse("/trainings/sessions/" + Guid.CreateVersion7() + " and /t/rheinfeuer", Hosts);

        Assert.Equal(ChatLinkKind.Training, link.Kind);
    }
}
