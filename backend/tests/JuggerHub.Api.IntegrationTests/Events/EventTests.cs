using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Events;

/// <summary>
/// Events (006). US1 — creation via the wizard: the create validation matrix and the creator
/// becoming the first admin. Exercises the real API + Postgres container.
/// </summary>
[Collection("Events")]
public sealed class EventTests
{
    private readonly JuggerHubApiFactory _factory;

    public EventTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US1: create ----------------------------------------------------------

    [Fact]
    public async Task Create_in_person_paid_teams_event_makes_creator_admin()
    {
        var (client, _, _, _) = await NewUserAsync();

        var resp = await client.PostAsJsonAsync("/api/v1/events", ValidInPersonPaidTeams());

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Tournament", dto.GetProperty("type").GetString());
        Assert.Equal("Teams", dto.GetProperty("participantMode").GetString());
        Assert.Equal("InPerson", dto.GetProperty("locationKind").GetString());
        Assert.Equal("Deutschland", dto.GetProperty("country").GetString());
        Assert.True(dto.GetProperty("isPaid").GetBoolean());
        Assert.Equal(0, dto.GetProperty("occupiedSpots").GetInt32());
        Assert.False(dto.GetProperty("isFull").GetBoolean());
        Assert.True(dto.GetProperty("viewer").GetProperty("isAdmin").GetBoolean());
        Assert.NotEqual(Guid.Empty, dto.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Create_virtual_free_individuals_event()
    {
        var (client, _, _, _) = await NewUserAsync();

        var resp = await client.PostAsJsonAsync("/api/v1/events", ValidVirtualFreeIndividuals());

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Virtual", dto.GetProperty("locationKind").GetString());
        Assert.Equal("Individuals", dto.GetProperty("participantMode").GetString());
        Assert.False(dto.GetProperty("isPaid").GetBoolean());
        Assert.False(string.IsNullOrEmpty(dto.GetProperty("virtualLink").GetString()));
    }

    [Fact]
    public async Task End_before_start_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidInPersonPaidTeams(), new { startsAt = "2026-09-06T18:00:00Z", endsAt = "2026-09-05T09:00:00Z" });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task In_person_without_country_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidInPersonPaidTeams(), new { country = (string?)null });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Virtual_without_link_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidVirtualFreeIndividuals(), new { virtualLink = (string?)null });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Non_positive_limit_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidInPersonPaidTeams(), new { participationLimit = 0 });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Paid_without_recipient_or_iban_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidInPersonPaidTeams(), new { feeRecipientName = (string?)null, feeIban = (string?)null });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Other_type_without_custom_label_is_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();
        var body = Merge(ValidVirtualFreeIndividuals(), new { type = "Other", customTypeLabel = (string?)null });

        var resp = await client.PostAsJsonAsync("/api/v1/events", body);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_create_is_rejected()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/events", ValidVirtualFreeIndividuals());

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // --- US2: public page -----------------------------------------------------

    [Fact]
    public async Task Public_event_detail_is_readable_anonymously()
    {
        var (creator, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(creator, ValidInPersonPaidTeams());

        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/api/v1/events/{id}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Berlin Cup", dto.GetProperty("name").GetString());
        Assert.Equal("Deutschland", dto.GetProperty("country").GetString());
        Assert.Equal("DE89370400440532013000", dto.GetProperty("feeIban").GetString());
        var viewer = dto.GetProperty("viewer");
        Assert.False(viewer.GetProperty("isAuthenticated").GetBoolean());
        Assert.False(viewer.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Unknown_event_detail_is_404()
    {
        var anon = _factory.CreateClient();

        var resp = await anon.GetAsync($"/api/v1/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Creator_sees_admin_viewer_relation()
    {
        var (creator, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(creator, ValidVirtualFreeIndividuals());

        var dto = await creator.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}");

        var viewer = dto.GetProperty("viewer");
        Assert.True(viewer.GetProperty("isAuthenticated").GetBoolean());
        Assert.True(viewer.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Participant_groups_are_public_and_start_empty()
    {
        var (creator, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(creator, ValidVirtualFreeIndividuals());
        var anon = _factory.CreateClient();

        var joined = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/participants?group=joined");
        Assert.Equal(0, joined.GetProperty("totalCount").GetInt32());

        var bad = await anon.GetAsync($"/api/v1/events/{id}/participants?group=nope");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var unknown = await anon.GetAsync($"/api/v1/events/{Guid.NewGuid()}/participants?group=joined");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // --- US3: sign up ---------------------------------------------------------

    [Fact]
    public async Task Free_individual_signup_joins()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (u, _, _, _) = await NewUserAsync();

        var resp = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Joined", dto.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Paid_individual_signup_awaits_approval()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsPaid(4));
        var (u, _, _, _) = await NewUserAsync();

        var resp = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AwaitingApproval", dto.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Full_event_puts_signup_on_waitlist()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(1));
        var (u1, _, _, _) = await NewUserAsync();
        var (u2, _, _, _) = await NewUserAsync();

        var r1 = await u1.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var r2 = await u2.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        Assert.Equal("Joined", (await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal("Waitlisted", (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Team_direct_signup_is_refused_use_party_instead()
    {
        // Feature 016: teams-only events are entered via a party, not a direct team sign-up.
        var (admin, _, _, _) = await NewUserAsync();
        var teamId = await CreateTeamAsync(admin);
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, TeamsFree(4));

        var refused = await admin.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task Mode_mismatch_is_rejected()
    {
        var (u, _, _, _) = await NewUserAsync();
        var teamId = await CreateTeamAsync(u);

        var individualsId = await CreateEventAsync(u, IndividualsFree(4));
        var teamsId = await CreateEventAsync(u, TeamsFree(4));

        var teamIntoIndividuals = await u.PostAsJsonAsync($"/api/v1/events/{individualsId}/signup", new { teamId });
        var individualIntoTeams = await u.PostAsJsonAsync($"/api/v1/events/{teamsId}/signup", new { teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.BadRequest, teamIntoIndividuals.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, individualIntoTeams.StatusCode);
    }

    [Fact]
    public async Task Duplicate_signup_is_rejected()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (u, _, _, _) = await NewUserAsync();

        var first = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var second = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Withdraw_releases_the_spot()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (u, _, _, _) = await NewUserAsync();

        var join = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var signupId = (await join.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var withdraw = await u.DeleteAsync($"/api/v1/events/{id}/signup/{signupId}");
        Assert.Equal(HttpStatusCode.NoContent, withdraw.StatusCode);

        var anon = _factory.CreateClient();
        var joined = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/participants?group=joined");
        Assert.Equal(0, joined.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Signup_on_ended_event_is_refused()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, EndedIndividuals());
        var (u, _, _, _) = await NewUserAsync();

        var resp = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Concurrent_last_spot_signups_never_exceed_the_limit()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(1));

        var clients = new List<HttpClient>();
        for (var i = 0; i < 5; i++)
        {
            var (c, _, _, _) = await NewUserAsync();
            clients.Add(c);
        }

        var results = await Task.WhenAll(clients.Select(c =>
            c.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null })));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var anon = _factory.CreateClient();
        var joined = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/participants?group=joined");
        var waitlist = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/participants?group=waitlist");

        Assert.Equal(1, joined.GetProperty("totalCount").GetInt32());
        Assert.Equal(4, waitlist.GetProperty("totalCount").GetInt32());
    }

    // --- US4: participant administration + edit --------------------------------

    [Fact]
    public async Task Admin_approves_awaiting_signup_and_non_admin_cannot()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsPaid(4));
        var (u, _, _, _) = await NewUserAsync();

        var join = await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var sid = (await join.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var byNonAdmin = await u.PostAsync($"/api/v1/events/{id}/participants/{sid}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, byNonAdmin.StatusCode);

        var byAdmin = await org.PostAsync($"/api/v1/events/{id}/participants/{sid}/approve", null);
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);
        Assert.Equal("Joined", (await byAdmin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Promote_respects_capacity_and_admits_when_a_spot_opens()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(1));
        var (u1, _, _, _) = await NewUserAsync();
        var (u2, _, _, _) = await NewUserAsync();

        var j1 = await u1.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var sid1 = (await j1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var j2 = await u2.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var sid2 = (await j2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Full: promoting the waitlisted entry has no open spot.
        var whenFull = await org.PostAsync($"/api/v1/events/{id}/participants/{sid2}/promote", null);
        Assert.Equal(HttpStatusCode.Conflict, whenFull.StatusCode);

        // Free a spot, then promotion admits into it.
        var remove = await org.DeleteAsync($"/api/v1/events/{id}/signup/{sid1}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var promoted = await org.PostAsync($"/api/v1/events/{id}/participants/{sid2}/promote", null);
        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        Assert.Equal("Joined", (await promoted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Non_admin_cannot_remove_another_participant()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (u1, _, _, _) = await NewUserAsync();
        var (u2, _, _, _) = await NewUserAsync();

        var j1 = await u1.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        var sid1 = (await j1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var forbidden = await u2.DeleteAsync($"/api/v1/events/{id}/signup/{sid1}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Edit_raises_limit_but_refuses_below_occupied_and_non_admin()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));

        var raise = await PatchJsonAsync(org, $"/api/v1/events/{id}", EditBody(10));
        Assert.Equal(HttpStatusCode.OK, raise.StatusCode);
        Assert.Equal(10, (await raise.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("participationLimit").GetInt32());

        var (a, _, _, _) = await NewUserAsync();
        var (b, _, _, _) = await NewUserAsync();
        await a.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        await b.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });

        var lower = await PatchJsonAsync(org, $"/api/v1/events/{id}", EditBody(1));
        Assert.Equal(HttpStatusCode.Conflict, lower.StatusCode);

        var (outsider, _, _, _) = await NewUserAsync();
        var forbidden = await PatchJsonAsync(outsider, $"/api/v1/events/{id}", EditBody(5));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    // --- US5: news ------------------------------------------------------------

    [Fact]
    public async Task Admin_posts_news_visible_to_everyone_and_non_admin_cannot()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));

        var post = await org.PostAsJsonAsync($"/api/v1/events/{id}/news", new { body = "First whistle 10:00 sharp." });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var anon = _factory.CreateClient();
        var feed = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/news");
        Assert.Equal(1, feed.GetProperty("totalCount").GetInt32());
        Assert.Equal("First whistle 10:00 sharp.", feed.GetProperty("items")[0].GetProperty("body").GetString());

        var (outsider, _, _, _) = await NewUserAsync();
        var forbidden = await outsider.PostAsJsonAsync($"/api/v1/events/{id}/news", new { body = "nope" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task News_feed_is_empty_for_a_new_event()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var anon = _factory.CreateClient();

        var feed = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/news");

        Assert.Equal(0, feed.GetProperty("totalCount").GetInt32());
    }

    // --- US6: contacts --------------------------------------------------------

    [Fact]
    public async Task Admin_manages_contacts_shown_publicly()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));

        var add = await org.PostAsJsonAsync($"/api/v1/events/{id}/contacts",
            new { name = "Ada K.", role = "Location host", phone = (string?)null, email = "ada@example.org" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var contactId = (await add.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anon = _factory.CreateClient();
        var list = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/contacts");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());

        var update = await PatchJsonAsync(org, $"/api/v1/events/{id}/contacts/{contactId}",
            new { name = "Ada K.", role = "Location host", phone = "+49 30 123456", email = "ada@example.org" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var remove = await org.DeleteAsync($"/api/v1/events/{id}/contacts/{contactId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var after = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/contacts");
        Assert.Equal(0, after.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Contact_without_any_method_is_rejected_and_non_admin_forbidden()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));

        var noMethod = await org.PostAsJsonAsync($"/api/v1/events/{id}/contacts",
            new { name = "Nobody", role = "Ghost", phone = (string?)null, email = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, noMethod.StatusCode);

        var (outsider, _, _, _) = await NewUserAsync();
        var forbidden = await outsider.PostAsJsonAsync($"/api/v1/events/{id}/contacts",
            new { name = "X", role = "Y", phone = "123", email = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    // --- US7: co-admins -------------------------------------------------------

    [Fact]
    public async Task Coadmin_link_can_be_created_and_rotated()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));

        var create = await org.PostAsync($"/api/v1/events/{id}/invitations/link", null);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var token1 = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        var rotate = await org.PostAsync($"/api/v1/events/{id}/invitations/link", null);
        var token2 = (await rotate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        Assert.NotEqual(token1, token2);

        var active = await org.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}/invitations/link");
        Assert.Equal(token2, active.GetProperty("token").GetString());
    }

    [Fact]
    public async Task Targeted_invite_emails_and_accept_grants_admin()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (target, targetId, _, targetEmail) = await NewUserAsync();
        _factory.EmailSender.Clear();

        var invite = await org.PostAsJsonAsync($"/api/v1/events/{id}/invitations", new { userId = targetId });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);

        var mail = _factory.EmailSender.LatestFor(targetEmail);
        Assert.NotNull(mail);
        var token = ExtractInviteToken(mail!.HtmlBody);

        var anon = _factory.CreateClient();
        var preview = await anon.GetFromJsonAsync<JsonElement>($"/api/v1/event-invitations/{token}");
        Assert.Equal("Usable", preview.GetProperty("state").GetString());

        var accept = await target.PostAsync($"/api/v1/event-invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var detail = await target.GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}");
        Assert.True(detail.GetProperty("viewer").GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Non_admin_cannot_invite_coadmin()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (outsider, outsiderId, _, _) = await NewUserAsync();

        var resp = await outsider.PostAsJsonAsync($"/api/v1/events/{id}/invitations", new { userId = outsiderId });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Last_admin_cannot_step_down_until_another_exists()
    {
        var (org, orgId, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));

        var soleAdmin = await org.DeleteAsync($"/api/v1/events/{id}/admins/{orgId}");
        Assert.Equal(HttpStatusCode.Conflict, soleAdmin.StatusCode);

        await AddCoAdminAsync(org, id);

        var stepDown = await org.DeleteAsync($"/api/v1/events/{id}/admins/{orgId}");
        Assert.Equal(HttpStatusCode.NoContent, stepDown.StatusCode);
    }

    // --- US8: cancel ----------------------------------------------------------

    [Fact]
    public async Task Cancel_blocks_signups_notifies_and_non_admin_forbidden()
    {
        var (org, _, _, _) = await NewUserAsync();
        var id = await CreateEventAsync(org, IndividualsFree(4));
        var (u, _, _, uEmail) = await NewUserAsync();
        await u.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        _factory.EmailSender.Clear();

        var byNonAdmin = await u.PostAsync($"/api/v1/events/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.Forbidden, byNonAdmin.StatusCode);

        var byAdmin = await org.PostAsync($"/api/v1/events/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, byAdmin.StatusCode);

        Assert.NotNull(_factory.EmailSender.LatestFor(uEmail));

        var (u2, _, _, _) = await NewUserAsync();
        var again = await u2.PostAsJsonAsync($"/api/v1/events/{id}/signup", new { teamId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var detail = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/events/{id}");
        Assert.Equal("Cancelled", detail.GetProperty("status").GetString());
    }

    // --- Helpers --------------------------------------------------------------

    private static string ExtractInviteToken(string html)
    {
        const string marker = "/event-invite/";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOfAny(['"', '<', ' ', '\''], start);
        return html[start..end];
    }

    private async Task AddCoAdminAsync(HttpClient adminClient, Guid eventId)
    {
        var (target, targetId, _, targetEmail) = await NewUserAsync();
        _factory.EmailSender.Clear();
        var invite = await adminClient.PostAsJsonAsync($"/api/v1/events/{eventId}/invitations", new { userId = targetId });
        invite.EnsureSuccessStatusCode();
        var token = ExtractInviteToken(_factory.EmailSender.LatestFor(targetEmail)!.HtmlBody);
        var accept = await target.PostAsync($"/api/v1/event-invitations/{token}/accept", null);
        accept.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> PatchJsonAsync(HttpClient client, string url, object body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body) });

    private static object EditBody(int limit) =>
        Merge(ValidVirtualFreeIndividuals(), new { participationLimit = limit });

    private async Task<Guid> CreateTeamAsync(HttpClient client)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var resp = await client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Crew", slug, type = "Mixteam", city = (string?)null });
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Teams.Where(t => t.Slug == slug).Select(t => t.Id).FirstAsync();
    }

    private static object IndividualsFree(int limit) =>
        Merge(ValidVirtualFreeIndividuals(), new { participationLimit = limit });

    private static object IndividualsPaid(int limit) =>
        Merge(ValidVirtualFreeIndividuals(), new
        {
            participationLimit = limit,
            isPaid = true,
            feeRecipientName = "Organiser e.V.",
            feeIban = "DE89370400440532013000",
            feeCurrency = "EUR",
        });

    private static object TeamsFree(int limit) =>
        Merge(ValidVirtualFreeIndividuals(), new { participantMode = "Teams", participationLimit = limit });

    private static object EndedIndividuals() =>
        Merge(ValidVirtualFreeIndividuals(), new { startsAt = "2020-01-01T09:00:00Z", endsAt = "2020-01-01T18:00:00Z" });

    private async Task<Guid> CreateEventAsync(HttpClient client, object body)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/events", body);
        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return dto.GetProperty("id").GetGuid();
    }

    private static object ValidInPersonPaidTeams() => new
    {
        name = "Berlin Cup",
        type = "Tournament",
        description = "Two days of open Jugger on the old airfield. All divisions welcome.",
        startsAt = "2026-09-05T09:00:00Z",
        endsAt = "2026-09-06T18:00:00Z",
        locationKind = "InPerson",
        venueName = "Altes Flugfeld",
        street = "Hauptstrasse 1",
        postalCode = "10115",
        city = "Berlin",
        country = "Deutschland",
        virtualLink = (string?)null,
        participantMode = "Teams",
        participationLimit = 16,
        isPaid = true,
        feeAmount = 40m,
        feeCurrency = "EUR",
        feeRecipientName = "JSC Berlin e.V.",
        feeIban = "DE89370400440532013000",
        feePaymentDeadline = "2026-08-20",
    };

    private static object ValidVirtualFreeIndividuals() => new
    {
        name = "Pompfen Skills Session",
        type = "Workshop",
        description = "Online technique clinic for runners and chains.",
        startsAt = "2026-07-20T18:00:00Z",
        endsAt = "2026-07-20T20:00:00Z",
        locationKind = "Virtual",
        venueName = (string?)null,
        street = (string?)null,
        postalCode = (string?)null,
        city = (string?)null,
        country = (string?)null,
        virtualLink = "https://zoom.us/j/1234567890",
        participantMode = "Individuals",
        participationLimit = 30,
        isPaid = false,
        feeAmount = (decimal?)null,
        feeCurrency = (string?)null,
        feeRecipientName = (string?)null,
        feeIban = (string?)null,
        feePaymentDeadline = (string?)null,
    };

    /// <summary>Serialize the base request, overlay the override object's properties, return a JsonElement body.</summary>
    private static JsonElement Merge(object baseBody, object overrides)
    {
        var map = new Dictionary<string, JsonElement>();
        foreach (var p in JsonSerializer.SerializeToElement(baseBody).EnumerateObject())
        {
            map[p.Name] = p.Value.Clone();
        }

        foreach (var p in JsonSerializer.SerializeToElement(overrides).EnumerateObject())
        {
            map[p.Name] = p.Value.Clone();
        }

        return JsonSerializer.SerializeToElement(map);
    }

    private async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }
}
