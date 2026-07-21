using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// The teamless "My team" home read (feature 023): GET /api/v1/profiles/me/invitations.
/// Returns only the caller's own usable (pending + unexpired) TARGETED invitations, with the
/// token the UI uses to accept/decline via the existing invitee endpoints. Exercises the real
/// API + Postgres container.
/// </summary>
[Collection("Teams")]
public sealed class MyInvitationsTests
{
    private readonly JuggerHubApiFactory _factory;

    public MyInvitationsTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Me_invitations_requires_authentication()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/v1/profiles/me/invitations");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Lists_only_the_callers_usable_targeted_invitations()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);

        var (alice, aliceId, _, _) = await NewUserAsync();
        var (_, bobId, _, _) = await NewUserAsync();

        // Admin invites both Alice and Bob, and also creates a shared LINK invite (not targeted).
        await InviteAsync(admin, slug, aliceId);
        await InviteAsync(admin, slug, bobId);
        await CreateLinkAsync(admin, slug);

        var page = await alice.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me/invitations");
        var items = page.GetProperty("items").EnumerateArray().ToArray();

        // Alice sees exactly her one targeted invite — not Bob's, not the link invite.
        Assert.Single(items);
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        var inv = items[0];
        Assert.Equal(slug, inv.GetProperty("teamSlug").GetString());
        Assert.Equal("Rheinfeuer", inv.GetProperty("teamName").GetString());
        Assert.Equal("CityTeam", inv.GetProperty("teamType").GetString());
        Assert.False(string.IsNullOrEmpty(inv.GetProperty("token").GetString()));
        Assert.False(string.IsNullOrEmpty(inv.GetProperty("inviterDisplayName").GetString()));
    }

    [Fact]
    public async Task Take_is_bounded_by_pagination()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var (alice, aliceId, _, _) = await NewUserAsync();

        // Two teams both invite Alice → two usable invitations.
        var slugA = NewSlug();
        var slugB = NewSlug();
        await CreateTeamAsync(admin, slugA);
        await CreateTeamAsync(admin, slugB);
        await InviteAsync(admin, slugA, aliceId);
        await InviteAsync(admin, slugB, aliceId);

        var page = await alice.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me/invitations?skip=0&take=1");
        Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Accepting_from_the_list_joins_and_removes_it()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (alice, aliceId, _, _) = await NewUserAsync();
        await InviteAsync(admin, slug, aliceId);

        // Alice reads her invite from the list and accepts it with the returned token.
        var token = await FirstTokenAsync(alice);
        var accept = await alice.PostAsync($"/api/v1/invitations/{token}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // She is now a member, and the consumed invite no longer appears.
        var detail = await alice.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}");
        Assert.Equal("Member", detail.GetProperty("myRole").GetString());

        var after = await alice.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me/invitations");
        Assert.Empty(after.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Declining_removes_it_without_joining()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (alice, aliceId, _, _) = await NewUserAsync();
        await InviteAsync(admin, slug, aliceId);

        var token = await FirstTokenAsync(alice);
        var decline = await alice.PostAsync($"/api/v1/invitations/{token}/decline", null);
        Assert.Equal(HttpStatusCode.NoContent, decline.StatusCode);

        var after = await alice.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me/invitations");
        Assert.Empty(after.GetProperty("items").EnumerateArray());
        // Not a member (internal team read is members-only → 404 for a non-member).
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/v1/teams/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Revoked_targeted_invite_is_not_listed()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (alice, aliceId, _, _) = await NewUserAsync();
        await InviteAsync(admin, slug, aliceId);

        // Admin revokes the targeted invite before Alice acts.
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/invitations");
        var inviteId = list.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("kind").GetString() == "Targeted").GetProperty("id").GetString();
        var revoke = await admin.DeleteAsync($"/api/v1/teams/{slug}/invitations/{inviteId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var page = await alice.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me/invitations");
        Assert.Empty(page.GetProperty("items").EnumerateArray());
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

    private static async Task InviteAsync(HttpClient admin, string slug, Guid targetUserId)
    {
        var resp = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetUserId });
        resp.EnsureSuccessStatusCode();
    }

    private async Task CreateLinkAsync(HttpClient admin, string slug)
    {
        var resp = await admin.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task<string> FirstTokenAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me/invitations");
        return page.GetProperty("items").EnumerateArray().First().GetProperty("token").GetString()!;
    }
}
