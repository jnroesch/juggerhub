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
    [InlineData("en", "Your team started a poll")]
    [InlineData("de", "Dein Team hat eine Umfrage gestartet")]
    [InlineData("es", "Tu equipo ha abierto una encuesta")]
    public void A_poll_names_the_team_and_keeps_the_question_off_the_lock_screen(string culture, string body)
    {
        // Feature 062, clarified: the question stays in the Alerts row and the email (spec FR-028).
        var payload = """{"teamSlug":"hh","teamName":"Hamburg Hammers","pollId":"0199f3c2-0000-7000-8000-000000000004","question":"Surprise party for Nia?"}""";

        var content = _composer.Compose(NotificationType.TeamPoll, payload, culture, "tag");

        Assert.Equal("Hamburg Hammers", content.Title);
        Assert.Equal(body, content.Body);
        Assert.DoesNotContain("Surprise", content.Title + content.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(NotificationType.PartyNews, """{"partyId":"0199f3c2-0000-7000-8000-000000000001"}""", "/parties/0199f3c2-0000-7000-8000-000000000001/news")]
    [InlineData(NotificationType.EventCancelled, """{"eventId":"0199f3c2-0000-7000-8000-000000000002","eventName":"Turnier"}""", "/events/0199f3c2-0000-7000-8000-000000000002")]
    [InlineData(NotificationType.TrainingUpdated, """{"teamSlug":"hh","sessionId":"0199f3c2-0000-7000-8000-000000000003"}""", "/trainings/sessions/0199f3c2-0000-7000-8000-000000000003")]
    [InlineData(NotificationType.TrainingScheduled, """{"teamSlug":"hh","trainingName":"Dienstag"}""", "/t/hh/trainings")]
    [InlineData(NotificationType.TeamPoll, """{"teamSlug":"hh","pollId":"0199f3c2-0000-7000-8000-000000000004","question":"Q?"}""", "/t/hh#poll-0199f3c2-0000-7000-8000-000000000004")]
    [InlineData(NotificationType.TeamPoll, """{"teamSlug":"hh","pollId":"not-a-guid"}""", "/t/hh")]
    [InlineData(NotificationType.TeamPoll, """{"teamSlug":"../evil","pollId":"0199f3c2-0000-7000-8000-000000000004"}""", "/")]
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

    // --- Feature 058: join requests ---------------------------------------------------------

    private const string JoinRequestPayload =
        """{"requestId":"0199f3c2-0000-7000-8000-000000000010","teamSlug":"hamburg-hammers","teamName":"Hamburg Hammers"}""";

    [Theory]
    [InlineData("en", "Jonas wants to join the team")]
    [InlineData("de", "Jonas möchte dem Team beitreten")]
    [InlineData("es", "Jonas quiere unirse al equipo")]
    public void A_join_request_names_the_team_and_the_player(string culture, string expected)
    {
        // The player's name reaches the composer from the actor, at send time — the payload does
        // not carry it and must not (037 FR-023).
        var content = _composer.Compose(NotificationType.TeamJoinRequest, JoinRequestPayload, culture, "tag", actorName: "Jonas");

        Assert.Equal("Hamburg Hammers", content.Title);
        Assert.Equal(expected, content.Body);
        Assert.Equal("/t/hamburg-hammers", content.Url);
    }

    [Fact]
    public void A_join_request_without_a_name_still_says_what_happened()
    {
        var content = _composer.Compose(NotificationType.TeamJoinRequest, JoinRequestPayload, "en", "tag", actorName: null);

        Assert.Equal("Someone wants to join the team", content.Body);
        Assert.NotEqual("You have a new notification", content.Body);
    }

    [Theory]
    [InlineData(true, "Your request to join was accepted", "/t/hamburg-hammers")]
    [InlineData(false, "Your request to join was declined", "/browse/teams")]
    public void An_answer_says_accepted_or_declined_and_where_to_go(bool accepted, string body, string url)
    {
        var flag = accepted ? "true" : "false";
        var payload = $$"""{"teamSlug":"hamburg-hammers","teamName":"Hamburg Hammers","accepted":{{flag}}}""";

        var content = _composer.Compose(NotificationType.TeamJoinRequestAnswered, payload, "en", "tag");

        Assert.Equal("Hamburg Hammers", content.Title);
        Assert.Equal(body, content.Body);
        Assert.Equal(url, content.Url);
    }

    /// <summary>
    /// One representative payload per type — the shape its producer writes. The guard below fails
    /// for any type missing here, so a new type cannot ship on the composer's generic fallbacks.
    /// </summary>
    private static readonly IReadOnlyDictionary<NotificationType, string> Samples = new Dictionary<NotificationType, string>
    {
        [NotificationType.TeamInvite] = """{"teamSlug":"hh","teamName":"Hamburg Hammers","inviterName":"Anna"}""",
        [NotificationType.TeamRoleChanged] = """{"teamSlug":"hh","teamName":"Hamburg Hammers","newRole":1}""",
        [NotificationType.TeamNews] = """{"teamSlug":"hh","teamName":"Hamburg Hammers","excerpt":"Saturday"}""",
        [NotificationType.PartyRequest] = """{"partyId":"0199f3c2-0000-7000-8000-000000000001","eventName":"Turnier","teamName":"Hamburg Hammers"}""",
        [NotificationType.PartyNews] = """{"partyId":"0199f3c2-0000-7000-8000-000000000001","eventName":"Turnier","teamName":"Hamburg Hammers"}""",
        [NotificationType.MarketInvite] = """{"eventId":"0199f3c2-0000-7000-8000-000000000002","eventName":"Turnier","teamName":"Hamburg Hammers"}""",
        [NotificationType.TrainingScheduled] = """{"teamSlug":"hh","trainingName":"Dienstag","sessionId":null}""",
        [NotificationType.TrainingUpdated] = """{"teamSlug":"hh","trainingName":"Dienstag","sessionId":"0199f3c2-0000-7000-8000-000000000003"}""",
        [NotificationType.EventCancelled] = """{"eventId":"0199f3c2-0000-7000-8000-000000000002","eventName":"Turnier"}""",
        [NotificationType.TeamJoinRequest] = JoinRequestPayload,
        [NotificationType.TeamJoinRequestAnswered] = """{"teamSlug":"hh","teamName":"Hamburg Hammers","accepted":true}""",
        [NotificationType.TeamPoll] = """{"teamSlug":"hh","teamName":"Hamburg Hammers","pollId":"0199f3c2-0000-7000-8000-000000000004","question":"Grillen am Samstag?"}""",
    };

    [Fact]
    public void Every_notification_type_has_its_own_title_body_and_url()
    {
        // The composer's three switches end in `_ =>` fallbacks, so a type added without its own
        // arms compiles, passes every other test, and reaches lock screens as "JuggerHub — You have
        // a new notification", opening the home page (GH #360, point 4). This fails instead —
        // the NotificationCategoryMappingTests pattern, applied to the composer.
        var fallbackTitle = _composer.Compose(NotificationType.TeamInvite, "not json", "en", "tag").Title;
        var fallbackBody = _composer.Compose(NotificationType.TeamInvite, "not json", "en", "tag").Body;

        foreach (var type in Enum.GetValues<NotificationType>())
        {
            Assert.True(Samples.ContainsKey(type), $"{type} has no sample here: add one, and its composer arms.");

            var content = _composer.Compose(type, Samples[type], "en", "tag", actorName: "Anna");

            Assert.True(content.Title != fallbackTitle, $"{type} falls back to the generic title.");
            Assert.True(content.Body != fallbackBody, $"{type} falls back to the generic body.");
            Assert.True(content.Url != "/", $"{type} opens nowhere in particular.");
        }
    }
}
