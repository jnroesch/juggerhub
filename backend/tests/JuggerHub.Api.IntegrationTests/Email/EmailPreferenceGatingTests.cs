using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Parties;

namespace JuggerHub.Api.IntegrationTests.Email;

/// <summary>
/// The four emails migrated by feature 039 honour the recipient's Email-channel preference
/// (FR-012, FR-027).
///
/// Until this feature none of them consulted the preference system at all — they mailed everyone
/// unconditionally. Giving them the shared footer's "Manage notifications" link without this gate
/// would have made an explicit promise the product did not keep.
///
/// Collected in one file on purpose: the point being proved is that *all* the party/market send
/// sites are gated, including the nudge, which is a second call site that is easy to miss. Event
/// cancellation is gated too and is covered in <c>Events/EventTests</c>, where the cancel flow
/// already has its fixtures.
/// </summary>
[Collection("Parties")]
public sealed class EmailPreferenceGatingTests : PartyTestSupport
{
    public EmailPreferenceGatingTests(JuggerHubApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Party_request_email_is_suppressed_when_invites_email_is_off()
    {
        var (adminClient, _, _, _) = await NewUserAsync();
        var (memberClient, memberId, _, memberEmail) = await NewUserAsync();

        var (teamId, _) = await CreateTeamAsync(adminClient);
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(adminClient);

        await DisableEmailAsync(memberClient, "InvitesAndRoster");

        Factory.EmailSender.Clear();
        await FormPartyAsync(adminClient, eventId, teamId);

        Assert.Null(Factory.EmailSender.LatestFor(memberEmail));

        // ...but the in-app notification is a separate channel and still arrives (FR-016).
        var feed = await memberClient.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        Assert.Contains(
            feed.GetProperty("items").EnumerateArray(),
            n => n.GetProperty("type").GetString() == "PartyRequest");
    }

    [Fact]
    public async Task Party_request_email_is_sent_when_the_member_never_touched_preferences()
    {
        var (adminClient, _, _, _) = await NewUserAsync();
        var (_, memberId, _, memberEmail) = await NewUserAsync();

        var (teamId, _) = await CreateTeamAsync(adminClient);
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(adminClient);

        Factory.EmailSender.Clear();
        await FormPartyAsync(adminClient, eventId, teamId);

        // Sparse preferences: no stored row means enabled (FR-014).
        Assert.NotNull(Factory.EmailSender.LatestFor(memberEmail));
    }

    /// <summary>
    /// The nudge is the second party-request send site. Gating the fan-out but not this one would
    /// leave a muted member still receiving mail — the exact gap this assertion exists to catch.
    /// </summary>
    [Fact]
    public async Task Party_request_nudge_email_is_suppressed_when_invites_email_is_off()
    {
        var (adminClient, _, _, _) = await NewUserAsync();
        var (memberClient, memberId, _, memberEmail) = await NewUserAsync();

        var (teamId, _) = await CreateTeamAsync(adminClient);
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(adminClient);
        var partyId = await FormPartyAsync(adminClient, eventId, teamId);

        await DisableEmailAsync(memberClient, "InvitesAndRoster");

        Factory.EmailSender.Clear();
        var nudge = await adminClient.PostAsync($"/api/v1/parties/{partyId}/members/{memberId}/nudge", null);
        nudge.EnsureSuccessStatusCode();

        Assert.Null(Factory.EmailSender.LatestFor(memberEmail));
    }

    [Fact]
    public async Task Party_news_email_is_suppressed_when_team_news_email_is_off()
    {
        var (adminClient, adminId, _, _) = await NewUserAsync();
        var (memberClient, memberId, _, memberEmail) = await NewUserAsync();
        _ = adminId;

        var (teamId, _) = await CreateTeamAsync(adminClient);
        await AddTeamMemberAsync(teamId, memberId);
        var eventId = await CreateTeamsEventAsync(adminClient);
        var partyId = await FormPartyAsync(adminClient, eventId, teamId);

        // The member has to be in the crew to receive party news.
        var join = await memberClient.PostAsync($"/api/v1/parties/{partyId}/join", null);
        join.EnsureSuccessStatusCode();

        await DisableEmailAsync(memberClient, "TeamNews");

        Factory.EmailSender.Clear();
        var post = await adminClient.PostAsJsonAsync(
            $"/api/v1/parties/{partyId}/news", new { body = "Bring the spare chains." });
        post.EnsureSuccessStatusCode();

        Assert.Null(Factory.EmailSender.LatestFor(memberEmail));
    }

    /// <summary>The new Events category is exposed with both channels defaulting to on (FR-019/FR-021).</summary>
    [Fact]
    public async Task Events_category_is_offered_with_both_channels_on_by_default()
    {
        var (client, _, _, _) = await NewUserAsync();

        var matrix = await client.GetFromJsonAsync<JsonElement>("/api/v1/notification-preferences");
        var events = matrix.GetProperty("categories").EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("category").GetString() == "Events");

        Assert.NotEqual(JsonValueKind.Undefined, events.ValueKind);
        Assert.True(events.GetProperty("channels").GetProperty("inApp").GetBoolean());
        Assert.True(events.GetProperty("channels").GetProperty("email").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(events.GetProperty("label").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(events.GetProperty("description").GetString()));
    }

    /// <summary>
    /// The matrix renders for every supported language. This guards the category-copy lookup: it
    /// used to be a bare indexer, so a category missing one language's entry took the whole
    /// settings page down for those users instead of falling back.
    /// </summary>
    [Theory]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Preference_matrix_renders_in_every_supported_language(string language)
    {
        var (client, _, _, _) = await NewUserAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notification-preferences");
        request.Headers.Add("Accept-Language", language);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var matrix = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            matrix.GetProperty("categories").EnumerateArray(),
            c => c.GetProperty("category").GetString() == "Events");
    }

    private static async Task DisableEmailAsync(HttpClient client, string category)
    {
        var response = await client.PutAsJsonAsync(
            $"/api/v1/notification-preferences/{category}/Email", new { enabled = false });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
