using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Team space + member handling (005): creation/slug identity, member-only visibility,
/// invitations (link + emailed targeted), accept/decline, the last-admin guard, and delete.
/// Exercises the real API + Postgres container.
/// </summary>
[Collection("Teams")]
public sealed class TeamTests
{
    private readonly JuggerHubApiFactory _factory;

    public TeamTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US1: create & identity -----------------------------------------------

    [Fact]
    public async Task Create_city_team_makes_creator_the_admin()
    {
        var (client, _, _, _) = await NewUserAsync();
        var slug = NewSlug();

        var resp = await client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Rheinfeuer", slug, type = "CityTeam", city = "Berlin" });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Admin", dto.GetProperty("myRole").GetString());
        Assert.Equal(1, dto.GetProperty("memberCount").GetInt32());
        Assert.Equal("CityTeam", dto.GetProperty("type").GetString());
        Assert.Equal("Berlin", dto.GetProperty("city").GetString());
    }

    [Fact]
    public async Task Create_mixteam_has_no_city()
    {
        var (client, _, _, _) = await NewUserAsync();
        var slug = NewSlug();

        var resp = await client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Chaos Crew", slug, type = "Mixteam", city = (string?)null });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Mixteam", dto.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("city").ValueKind);
    }

    [Fact]
    public async Task Duplicate_slug_is_rejected_with_409()
    {
        var (client, _, _, _) = await NewUserAsync();
        var slug = NewSlug();

        var first = await CreateTeamAsync(client, slug);
        var second = await client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Other", slug, type = "Mixteam", city = (string?)null });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("Has Space")]
    [InlineData("UPPER")]
    [InlineData("ab")]
    [InlineData("new")]   // reserved
    [InlineData("join")]  // reserved
    public async Task Malformed_or_reserved_slug_is_rejected_with_400(string slug)
    {
        var (client, _, _, _) = await NewUserAsync();

        var resp = await client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Team", slug, type = "Mixteam", city = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task City_team_without_city_and_mixteam_with_city_are_rejected()
    {
        var (client, _, _, _) = await NewUserAsync();

        var noCity = await client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Team", slug = NewSlug(), type = "CityTeam", city = (string?)null });
        var mixWithCity = await client.PostAsJsonAsync("/api/v1/teams",
            new { name = "Team", slug = NewSlug(), type = "Mixteam", city = "Berlin" });

        Assert.Equal(HttpStatusCode.BadRequest, noCity.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mixWithCity.StatusCode);
    }

    [Fact]
    public async Task Slug_available_reflects_taken_and_reserved()
    {
        var (client, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(client, slug);

        var taken = await client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/slug-available?slug={slug}");
        var reserved = await client.GetFromJsonAsync<JsonElement>("/api/v1/teams/slug-available?slug=admin");
        var free = await client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/slug-available?slug={NewSlug()}");

        Assert.False(taken.GetProperty("available").GetBoolean());
        Assert.False(reserved.GetProperty("available").GetBoolean());
        Assert.True(free.GetProperty("available").GetBoolean());
    }

    // --- US2: visibility ------------------------------------------------------

    [Fact]
    public async Task Non_member_gets_404_on_internal_but_public_is_open()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        var (outsider, _, _, _) = await NewUserAsync();
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/teams/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/teams/{slug}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/teams/{slug}/news")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/teams/{slug}/public")).StatusCode);
    }

    [Fact]
    public async Task Roster_lists_the_creator_as_admin()
    {
        var (admin, _, handle, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/members");
        var items = page.GetProperty("items").EnumerateArray().ToArray();

        Assert.Single(items);
        Assert.Equal(handle, items[0].GetProperty("handle").GetString());
        Assert.Equal("Admin", items[0].GetProperty("role").GetString());
    }

    // --- US3: invitations -----------------------------------------------------

    [Fact]
    public async Task Admin_can_rotate_and_revoke_the_invite_link()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        var first = await CreateLinkAsync(admin, slug);
        var second = await CreateLinkAsync(admin, slug);
        Assert.NotEqual(first.Token, second.Token);

        // Old token is no longer usable.
        var oldPreview = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/invitations/{first.Token}");
        Assert.Equal("Invalid", oldPreview.GetProperty("state").GetString());

        // Revoke the current active link.
        var active = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/invitations");
        var linkId = active.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("kind").GetString() == "Link").GetProperty("id").GetString();
        var revoke = await admin.DeleteAsync($"/api/v1/teams/{slug}/invitations/{linkId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
    }

    [Fact]
    public async Task Targeted_invite_emails_the_user_and_prevents_duplicates_and_members()
    {
        var (admin, adminId, _, _) = await NewUserAsync();
        var (_, targetId, _, targetEmail) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        _factory.EmailSender.Clear();

        var invite = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var mail = _factory.EmailSender.LatestFor(targetEmail);
        Assert.NotNull(mail);
        Assert.Contains("/join/", mail!.HtmlBody);

        // Re-inviting the same user does not duplicate (200 already-invited).
        var again = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetId });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        // Inviting an existing member (the admin themselves) → 400.
        var selfInvite = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = adminId });
        Assert.Equal(HttpStatusCode.BadRequest, selfInvite.StatusCode);
    }

    [Fact]
    public async Task Non_admins_cannot_invite()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (outsider, outsiderId, _, _) = await NewUserAsync();

        // Non-member → 404 (no oracle).
        var byOutsider = await outsider.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        Assert.Equal(HttpStatusCode.NotFound, byOutsider.StatusCode);

        // A plain member → 403.
        var token = (await CreateLinkAsync(admin, slug)).Token;
        await outsider.PostAsync($"/api/v1/invitations/{token}/accept", null);
        var byMember = await outsider.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);
    }

    // --- US4: accept / decline ------------------------------------------------

    [Fact]
    public async Task Accepting_a_link_joins_as_a_member_and_is_idempotent()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var token = (await CreateLinkAsync(admin, slug)).Token;

        var (joiner, _, _, _) = await NewUserAsync();
        var preview = await joiner.GetFromJsonAsync<JsonElement>($"/api/v1/invitations/{token}");
        Assert.Equal("Usable", preview.GetProperty("state").GetString());

        var accept = await joiner.PostAsync($"/api/v1/invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var detail = await joiner.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}");
        Assert.Equal("Member", detail.GetProperty("myRole").GetString());
        Assert.Equal(2, detail.GetProperty("memberCount").GetInt32());

        // Second accept is a no-op success.
        var again = await joiner.PostAsync($"/api/v1/invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Unknown_token_is_404_and_revoked_invite_cannot_be_accepted()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var first = await CreateLinkAsync(admin, slug);
        await CreateLinkAsync(admin, slug); // rotate → first is now revoked

        var (joiner, _, _, _) = await NewUserAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await joiner.GetAsync("/api/v1/invitations/not-a-real-token")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await joiner.PostAsync($"/api/v1/invitations/{first.Token}/accept", null)).StatusCode);
    }

    // --- US5: roles & last-admin guard ----------------------------------------

    [Fact]
    public async Task Sole_admin_cannot_step_down_or_leave()
    {
        var (admin, adminId, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        var stepDown = await admin.PostAsync($"/api/v1/teams/{slug}/members/me/step-down", null);
        var leave = await admin.DeleteAsync($"/api/v1/teams/{slug}/members/{adminId}");

        Assert.Equal(HttpStatusCode.Conflict, stepDown.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, leave.StatusCode);
    }

    [Fact]
    public async Task Admin_can_promote_then_step_down_when_another_admin_exists()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var token = (await CreateLinkAsync(admin, slug)).Token;

        var (member, memberId, _, _) = await NewUserAsync();
        await member.PostAsync($"/api/v1/invitations/{token}/accept", null);

        var promote = await admin.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{memberId}/role", new { role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        var stepDown = await admin.PostAsync($"/api/v1/teams/{slug}/members/me/step-down", null);
        Assert.Equal(HttpStatusCode.NoContent, stepDown.StatusCode);

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}");
        Assert.Equal("Member", detail.GetProperty("myRole").GetString());
    }

    [Fact]
    public async Task Two_admins_demoting_each_other_leaves_exactly_one_admin()
    {
        var (a, aId, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(a, slug);
        var token = (await CreateLinkAsync(a, slug)).Token;

        var (b, bId, _, _) = await NewUserAsync();
        await b.PostAsync($"/api/v1/invitations/{token}/accept", null);
        await a.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{bId}/role", new { role = "Admin" });

        // Concurrent mutual demotion.
        var demoteB = a.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{bId}/role", new { role = "Member" });
        var demoteA = b.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{aId}/role", new { role = "Member" });
        var results = await Task.WhenAll(demoteB, demoteA);

        // Exactly one demotion wins; the other is rejected. Which rejection depends
        // on interleaving, and both are correct: the loser gets 409 Conflict if it
        // reached the last-admin guard, or 403 Forbidden if the winner demoted it
        // before its own admin check ran. Either way exactly one admin remains.
        var ok = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = results.Count(r =>
            r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Forbidden);
        Assert.Equal(1, ok);
        Assert.Equal(1, rejected);

        var page = await a.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/members");
        var admins = page.GetProperty("items").EnumerateArray().Count(i => i.GetProperty("role").GetString() == "Admin");
        Assert.Equal(1, admins);
    }

    [Fact]
    public async Task Non_admin_cannot_change_roles()
    {
        var (admin, adminId, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var token = (await CreateLinkAsync(admin, slug)).Token;
        var (member, _, _, _) = await NewUserAsync();
        await member.PostAsync($"/api/v1/invitations/{token}/accept", null);

        var resp = await member.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{adminId}/role", new { role = "Member" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // --- US6: delete ----------------------------------------------------------

    [Fact]
    public async Task Admin_deletes_team_and_it_is_gone()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        var del = await admin.DeleteAsync($"/api/v1/teams/{slug}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/teams/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/v1/teams/{slug}/public")).StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_delete_team()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var token = (await CreateLinkAsync(admin, slug)).Token;
        var (member, _, _, _) = await NewUserAsync();
        await member.PostAsync($"/api/v1/invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.DeleteAsync($"/api/v1/teams/{slug}")).StatusCode);
    }

    // --- Profile integration (teams on the profile) ---------------------------

    [Fact]
    public async Task Profile_lists_the_players_teams_on_owner_and_public_views()
    {
        var (client, _, handle, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(client, slug);

        var mine = await client.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me");
        var owned = mine.GetProperty("teams").EnumerateArray().ToArray();
        var team = owned.First(t => t.GetProperty("slug").GetString() == slug);
        Assert.Equal("Admin", team.GetProperty("role").GetString());
        Assert.Equal("Rheinfeuer", team.GetProperty("name").GetString());

        var pub = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/profiles/{handle}");
        var publicTeams = pub.GetProperty("teams").EnumerateArray()
            .Select(t => t.GetProperty("slug").GetString()).ToArray();
        Assert.Contains(slug, publicTeams);
    }

    // --- helpers --------------------------------------------------------------

    private async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }

    private static string NewSlug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private static Task<HttpResponseMessage> CreateTeamAsync(HttpClient client, string slug) =>
        client.PostAsJsonAsync("/api/v1/teams", new { name = "Rheinfeuer", slug, type = "CityTeam", city = "Berlin" });

    private async Task<(string Token, string Url)> CreateLinkAsync(HttpClient admin, string slug)
    {
        var resp = await admin.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (dto.GetProperty("token").GetString()!, dto.GetProperty("url").GetString()!);
    }
}
