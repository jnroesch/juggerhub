using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Public team page + request-to-join (feature 009): public visibility + viewer relation, the
/// request workflow (idempotent, duplicate-blocked), admin approve/decline, and the access gates.
/// </summary>
[Collection("Teams")]
public sealed class JoinRequestTests
{
    private readonly JuggerHubApiFactory _factory;

    public JoinRequestTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_page_is_anonymous_and_shows_roster_but_no_internal_data()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await NewTeamAsync(admin);

        var anon = _factory.CreateClient();
        var res = await anon.GetAsync($"/api/v1/teams/{slug}/public");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var dto = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Anonymous", dto.GetProperty("viewerRelation").GetString());
        Assert.True(dto.GetProperty("roster").GetArrayLength() >= 1); // the admin
        // No internal fields leak into the public payload.
        var raw = dto.GetRawText();
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"news\"", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Viewer_relation_tracks_request_then_membership()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await NewTeamAsync(admin);
        var (member, _, _, _) = await NewUserAsync();

        // Non-member.
        Assert.Equal("NonMember", await RelationAsync(member, slug));

        // Request → Requested (idempotent).
        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"/api/v1/teams/{slug}/join-requests", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"/api/v1/teams/{slug}/join-requests", null)).StatusCode);
        Assert.Equal("Requested", await RelationAsync(member, slug));

        // Exactly one pending request in the admin queue.
        var queue = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/join-requests");
        Assert.Equal(1, queue.GetProperty("totalCount").GetInt32());
        var requestId = queue.GetProperty("items")[0].GetProperty("id").GetString();

        // Approve → member.
        var approve = await admin.PostAsync($"/api/v1/teams/{slug}/join-requests/{requestId}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        Assert.Equal("Member", await RelationAsync(member, slug));

        // Admin relation is Admin.
        Assert.Equal("Admin", await RelationAsync(admin, slug));
    }

    [Fact]
    public async Task Member_cannot_request_and_anonymous_is_unauthorized()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await NewTeamAsync(admin);

        // The admin is already a member → 409.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/v1/teams/{slug}/join-requests", null)).StatusCode);

        // Anonymous cannot post a request.
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync($"/api/v1/teams/{slug}/join-requests", null)).StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_see_or_decide_requests()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await NewTeamAsync(admin);
        var (requester, _, _, _) = await NewUserAsync();
        await requester.PostAsync($"/api/v1/teams/{slug}/join-requests", null);

        // The requester (a non-admin non-member) cannot view the queue.
        Assert.Equal(HttpStatusCode.Forbidden, (await requester.GetAsync($"/api/v1/teams/{slug}/join-requests")).StatusCode);

        // Nor decide a request.
        var queue = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/join-requests");
        var requestId = queue.GetProperty("items")[0].GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await requester.PostAsync($"/api/v1/teams/{slug}/join-requests/{requestId}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task Decline_leaves_membership_unchanged()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = await NewTeamAsync(admin);
        var (requester, _, _, _) = await NewUserAsync();
        await requester.PostAsync($"/api/v1/teams/{slug}/join-requests", null);

        var queue = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/join-requests");
        var requestId = queue.GetProperty("items")[0].GetProperty("id").GetString();

        var decline = await admin.PostAsync($"/api/v1/teams/{slug}/join-requests/{requestId}/decline", null);
        Assert.Equal(HttpStatusCode.NoContent, decline.StatusCode);

        Assert.Equal("NonMember", await RelationAsync(requester, slug));
        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/join-requests");
        Assert.Equal(0, after.GetProperty("totalCount").GetInt32());
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<string> RelationAsync(HttpClient client, string slug)
    {
        var dto = await client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/public");
        return dto.GetProperty("viewerRelation").GetString()!;
    }

    private async Task<string> NewTeamAsync(HttpClient admin)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var resp = await admin.PostAsJsonAsync("/api/v1/teams",
            new { name = "Public FC", slug, type = "CityTeam", city = "Berlin" });
        resp.EnsureSuccessStatusCode();
        return slug;
    }

    private async Task<(HttpClient Client, Guid UserId, string Handle, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, userId, handle, email);
    }
}
