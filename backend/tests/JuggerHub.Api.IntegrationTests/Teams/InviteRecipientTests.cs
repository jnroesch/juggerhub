using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// A targeted team invitation is bound to its recipient (GH #401): holding the token is not enough
/// to accept it or to use it up. A shared link stays a bearer credential. Exercises the real API +
/// Postgres container.
/// </summary>
[Collection("Teams")]
public sealed class InviteRecipientTests
{
    private readonly JuggerHubApiFactory _factory;

    public InviteRecipientTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Another_account_cannot_accept_a_targeted_invitation()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (alice, aliceId, _, _) = await NewUserAsync();
        await InviteAsync(admin, slug, aliceId);
        var token = await FirstTokenAsync(alice);

        var (mallory, malloryId, _, _) = await NewUserAsync();
        var accept = await mallory.PostAsync($"/api/v1/invitations/{token}/accept", null);

        // Refused exactly as a token that does not exist is, so the answer confirms nothing.
        var unknown = await mallory.PostAsync($"/api/v1/invitations/{NewSlug()}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        await AssertSameProblemAsync(unknown, accept);

        Assert.False(await IsMemberAsync(slug, malloryId));
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(token));

        // The recipient is unaffected.
        Assert.Equal(HttpStatusCode.OK, (await alice.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);
        Assert.True(await IsMemberAsync(slug, aliceId));
        Assert.Equal(InvitationStatus.Accepted, await StatusAsync(token));
    }

    [Fact]
    public async Task Another_account_cannot_decline_a_targeted_invitation()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (alice, aliceId, _, _) = await NewUserAsync();
        await InviteAsync(admin, slug, aliceId);
        var token = await FirstTokenAsync(alice);

        var (mallory, _, _, _) = await NewUserAsync();
        var decline = await mallory.PostAsync($"/api/v1/invitations/{token}/decline", null);

        var unknown = await mallory.PostAsync($"/api/v1/invitations/{NewSlug()}/decline", null);
        Assert.Equal(HttpStatusCode.NotFound, decline.StatusCode);
        await AssertSameProblemAsync(unknown, decline);

        // Not used up: the recipient still has it and can still turn it down.
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(token));
        Assert.Equal(token, await FirstTokenAsync(alice));
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync($"/api/v1/invitations/{token}/decline", null)).StatusCode);
        Assert.Equal(InvitationStatus.Declined, await StatusAsync(token));
    }

    [Fact]
    public async Task A_member_who_is_not_the_recipient_cannot_use_a_targeted_invitation_up()
    {
        // The "already a member" shortcut used to mark a targeted invitation accepted for whoever
        // asked. The recipient check has to come before it.
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (alice, aliceId, _, _) = await NewUserAsync();
        await InviteAsync(admin, slug, aliceId);
        var token = await FirstTokenAsync(alice);

        var accept = await admin.PostAsync($"/api/v1/invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(token));
    }

    [Fact]
    public async Task Another_account_learns_nothing_about_a_revoked_targeted_invitation()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var (alice, aliceId, _, _) = await NewUserAsync();
        await InviteAsync(admin, slug, aliceId);
        var token = await FirstTokenAsync(alice);

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/invitations");
        var inviteId = list.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("kind").GetString() == "Targeted").GetProperty("id").GetString();
        (await admin.DeleteAsync($"/api/v1/teams/{slug}/invitations/{inviteId}")).EnsureSuccessStatusCode();

        // The recipient is told it is no longer usable; anyone else is told it does not exist.
        var (mallory, _, _, _) = await NewUserAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await mallory.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);
    }

    [Fact]
    public async Task A_shared_link_is_still_usable_by_whoever_holds_it()
    {
        var (admin, _, _, _) = await NewUserAsync();
        var slug = NewSlug();
        await CreateTeamAsync(admin, slug);
        var link = await admin.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        link.EnsureSuccessStatusCode();
        var token = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        var (first, firstId, _, _) = await NewUserAsync();
        var (second, secondId, _, _) = await NewUserAsync();
        var (third, _, _, _) = await NewUserAsync();

        Assert.Equal(HttpStatusCode.OK, (await first.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);
        // Declining a link is a no-op for everyone else.
        Assert.Equal(HttpStatusCode.NoContent, (await third.PostAsync($"/api/v1/invitations/{token}/decline", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsync($"/api/v1/invitations/{token}/accept", null)).StatusCode);

        Assert.True(await IsMemberAsync(slug, firstId));
        Assert.True(await IsMemberAsync(slug, secondId));
        Assert.Equal(InvitationStatus.Pending, await StatusAsync(token));
    }

    // --- helpers --------------------------------------------------------------

    /// <summary>Same status, title and detail: the refusal is the unknown-token answer, not one like it.</summary>
    private static async Task AssertSameProblemAsync(HttpResponseMessage expected, HttpResponseMessage actual)
    {
        Assert.Equal(expected.StatusCode, actual.StatusCode);
        var want = await expected.Content.ReadFromJsonAsync<JsonElement>();
        var got = await actual.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(want.GetProperty("title").GetString(), got.GetProperty("title").GetString());
        Assert.Equal(want.GetProperty("detail").GetString(), got.GetProperty("detail").GetString());
    }

    private async Task<InvitationStatus> StatusAsync(string token)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TeamInvitations.AsNoTracking().Where(i => i.Token == token).Select(i => i.Status).SingleAsync();
    }

    private async Task<bool> IsMemberAsync(string slug, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TeamMemberships.AnyAsync(m => m.Team.Slug == slug && m.UserId == userId);
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

    private static string NewSlug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private static async Task CreateTeamAsync(HttpClient client, string slug)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/teams", new { name = "Rheinfeuer", slug, type = "Mixteam", location = (object?)null });
        resp.EnsureSuccessStatusCode();
    }

    private static async Task InviteAsync(HttpClient admin, string slug, Guid targetUserId)
    {
        var resp = await admin.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = targetUserId });
        resp.EnsureSuccessStatusCode();
    }

    private static async Task<string> FirstTokenAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me/invitations");
        return page.GetProperty("items").EnumerateArray().First().GetProperty("token").GetString()!;
    }
}
