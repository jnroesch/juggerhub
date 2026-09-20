using JuggerHub.Entities;
using JuggerHub.Services.Notifications.Push;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Push;

/// <summary>
/// What a member actually reads, and where it takes them (feature 055). Resolved from the real
/// container so the localizer and composer under test are the ones the app uses.
/// </summary>
[Collection("Teams")]
public sealed class PushComposerTests
{
    private readonly IPushContentComposer _composer;

    public PushComposerTests(JuggerHubApiFactory factory)
    {
        var scope = factory.Services.CreateScope();
        _composer = scope.ServiceProvider.GetRequiredService<IPushContentComposer>();
    }

    [Theory]
    [InlineData("en", "invited you to join")]
    [InlineData("de", "eingeladen beizutreten")]
    [InlineData("es", "invitado a unirte")]
    public void A_team_invite_names_the_team_and_the_inviter(string culture, string expected)
    {
        var payload = """{"teamSlug":"hamburg-hammers","teamName":"Hamburg Hammers","inviterName":"Anna"}""";

        var content = _composer.Compose(NotificationType.TeamInvite, payload, culture, "tag");

        Assert.Equal("Hamburg Hammers", content.Title);
        Assert.Contains("Anna", content.Body);
        Assert.Contains(expected, content.Body);
        Assert.Equal("/t/hamburg-hammers", content.Url);
    }

    [Fact]
    public void An_unknown_culture_falls_back_to_english_rather_than_failing()
    {
        // The bare dictionary indexer this replaces once took the settings page down for a whole
        // language (feature 039). A missing translation is a gap in copy, never an outage.
        var payload = """{"teamSlug":"t","teamName":"T","inviterName":"Anna"}""";

        var content = _composer.Compose(NotificationType.TeamInvite, payload, "fr", "tag");

        Assert.Contains("invited you to join", content.Body);
    }

    [Fact]
    public void Team_news_names_the_team_but_never_carries_the_post_itself()
    {
        // The owner's decision was that a notification NAMES ITS SUBJECT. An excerpt is the content,
        // which is a separate decision belonging to #309.
        var payload = """{"teamSlug":"hh","teamName":"Hamburg Hammers","excerpt":"Secret plans for Saturday"}""";

        var content = _composer.Compose(NotificationType.TeamNews, payload, "en", "tag");

        Assert.Equal("Hamburg Hammers", content.Title);
        Assert.DoesNotContain("Secret plans", content.Body);
    }

    [Theory]
    [InlineData(NotificationType.PartyNews, """{"partyId":"0199f3c2-0000-7000-8000-000000000001"}""", "/parties/0199f3c2-0000-7000-8000-000000000001/news")]
    [InlineData(NotificationType.EventCancelled, """{"eventId":"0199f3c2-0000-7000-8000-000000000002","eventName":"Turnier"}""", "/events/0199f3c2-0000-7000-8000-000000000002")]
    [InlineData(NotificationType.TrainingUpdated, """{"teamSlug":"hh","sessionId":"0199f3c2-0000-7000-8000-000000000003"}""", "/trainings/sessions/0199f3c2-0000-7000-8000-000000000003")]
    [InlineData(NotificationType.TrainingScheduled, """{"teamSlug":"hh","trainingName":"Dienstag"}""", "/t/hh/trainings")]
    public void The_url_mirrors_the_in_app_rows_own_link(NotificationType type, string payload, string expected)
    {
        var content = _composer.Compose(type, payload, "en", "tag");

        Assert.Equal(expected, content.Url);
    }

    [Theory]
    [InlineData("""{"teamSlug":"../admin","teamName":"X","inviterName":"A"}""")]
    [InlineData("""{"teamSlug":"https://evil.example.com","teamName":"X","inviterName":"A"}""")]
    [InlineData("""{"teamSlug":"a/b","teamName":"X","inviterName":"A"}""")]
    public void A_slug_that_is_not_a_slug_cannot_reach_the_url(string payload)
    {
        // Slugs come from the database and are already constrained, so this is defence in depth —
        // but a notification is the one place a path is rebuilt away from the router.
        var content = _composer.Compose(NotificationType.TeamInvite, payload, "en", "tag");

        Assert.Equal("/", content.Url);
    }

    [Fact]
    public void Malformed_payload_still_produces_something_readable()
    {
        var content = _composer.Compose(NotificationType.TeamInvite, "not json at all", "en", "tag");

        Assert.False(string.IsNullOrWhiteSpace(content.Title));
        Assert.False(string.IsNullOrWhiteSpace(content.Body));
        Assert.Equal("/", content.Url);
        Assert.Equal("tag", content.Tag);
    }
}
