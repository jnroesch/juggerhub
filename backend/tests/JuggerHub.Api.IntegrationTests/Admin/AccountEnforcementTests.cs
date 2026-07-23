using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Recognition;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Admin;

/// <summary>
/// Feature 013 US4 — server-side enforcement of account states across the platform:
/// login/refresh refusal per status (with the right disclosure level), ban
/// invisibility on public surfaces (SC-005), the implicit re-registration block, and
/// full restoration on reversal. Suspension deliberately leaves everything visible.
/// </summary>
[Collection("AdminArea")]
public sealed class AccountEnforcementTests
{
    private readonly JuggerHubApiFactory _factory;

    public AccountEnforcementTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Suspended_login_gets_a_distinct_message_banned_gets_generic_failure()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, email) = await AdminAreaTestSupport.PlayerClientAsync(_factory);

        // Suspended → 403 with the coded "account_suspended" body (post-password disclosure).
        (await admin.PostAsync($"/api/v1/admin/users/{handle}/suspend", null)).EnsureSuccessStatusCode();
        var suspendedLogin = await AuthTestHelpers.LoginAsync(_factory.CreateClient(), email, AuthTestHelpers.ValidPassword);
        Assert.Equal(HttpStatusCode.Forbidden, suspendedLogin.StatusCode);
        var body = await suspendedLogin.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("account_suspended", body.GetProperty("status").GetString());

        // Suspension blocks ONLY login — the profile stays visible (unlike a ban, it isn't hidden
        // by the global filter). Read as an authenticated viewer (profiles are not anonymous by
        // default since feature 026).
        var profile = await admin.GetAsync($"/api/v1/profiles/{handle}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        // Reinstate → login works again.
        (await admin.PostAsync($"/api/v1/admin/users/{handle}/reinstate", null)).EnsureSuccessStatusCode();
        var reinstatedLogin = await AuthTestHelpers.LoginAsync(_factory.CreateClient(), email, AuthTestHelpers.ValidPassword);
        Assert.Equal(HttpStatusCode.OK, reinstatedLogin.StatusCode);

        // Banned → the generic 401, indistinguishable from wrong credentials.
        (await admin.PostAsync($"/api/v1/admin/users/{handle}/ban", null)).EnsureSuccessStatusCode();
        var bannedLogin = await AuthTestHelpers.LoginAsync(_factory.CreateClient(), email, AuthTestHelpers.ValidPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, bannedLogin.StatusCode);
    }

    [Fact]
    public async Task Suspension_kills_the_live_session_at_the_next_refresh()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        // This client holds live auth cookies from login.
        var (playerClient, _, handle, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);

        (await admin.PostAsync($"/api/v1/admin/users/{handle}/suspend", null)).EnsureSuccessStatusCode();

        var refresh = await playerClient.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Ban_hides_the_player_everywhere_and_unban_restores_intact()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var (playerClient, _, handle, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        var (observer, _, _, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);

        // Fixtures: found a team (owner roster entry); second member views roster. The player is
        // directory-visible by default (the search opt-in was removed in feature 020).
        var teamSlug = await RecognitionTestSupport.CreateTeamAsync(playerClient);
        var (viewerClient, viewerId, _, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        await AdminAreaTestSupport.WithDbAsync(_factory, async db =>
        {
            var teamId = await db.Teams.Where(t => t.Slug == teamSlug).Select(t => t.Id).SingleAsync();
            db.TeamMemberships.Add(new TeamMembership { TeamId = teamId, UserId = viewerId });
            await db.SaveChangesAsync();
        });

        // Baseline: visible on profile, browse, and roster to an authenticated observer (profile
        // reads + browse are authenticated-only since feature 026; a signed-in viewer sees any
        // profile — ban-hiding, tested here, is a separate global filter).
        Assert.Equal(HttpStatusCode.OK, (await observer.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);
        Assert.Contains(handle, await BrowseAsync(observer, handle));
        Assert.Contains(handle, await RosterAsync(viewerClient, teamSlug));

        // Ban → gone from every player-facing surface.
        (await admin.PostAsync($"/api/v1/admin/users/{handle}/ban", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await observer.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);
        Assert.DoesNotContain(handle, await BrowseAsync(observer, handle));
        Assert.DoesNotContain(handle, await RosterAsync(viewerClient, teamSlug));

        // ...but still findable in the admin users list (the one place).
        var adminList = await admin.GetStringAsync($"/api/v1/admin/users?q={handle}&status=Banned");
        Assert.Contains(handle, adminList);

        // Unban → everything returns intact (profile, browse, roster, membership).
        (await admin.PostAsync($"/api/v1/admin/users/{handle}/unban", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await observer.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);
        Assert.Contains(handle, await BrowseAsync(observer, handle));
        Assert.Contains(handle, await RosterAsync(viewerClient, teamSlug));
    }

    [Fact]
    public async Task Banned_email_cannot_register_again_and_gets_no_mail()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, email) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        (await admin.PostAsync($"/api/v1/admin/users/{handle}/ban", null)).EnsureSuccessStatusCode();

        var mailsBefore = _factory.EmailSender.Sent.Count(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));

        // Neutral response (no oracle), but nothing is created and nothing is sent.
        var register = await AuthTestHelpers.RegisterAsync(_factory.CreateClient(), email);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var accounts = await AdminAreaTestSupport.WithDbAsync(_factory, db =>
            db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == email));
        Assert.Equal(1, accounts);

        var mailsAfter = _factory.EmailSender.Sent.Count(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(mailsBefore, mailsAfter);
    }

    private static Task<string> BrowseAsync(HttpClient client, string q) =>
        client.GetStringAsync($"/api/v1/profiles?q={q}");

    private static async Task<string> RosterAsync(HttpClient member, string slug)
    {
        var resp = await member.GetAsync($"/api/v1/teams/{slug}/members");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }
}
