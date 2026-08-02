using System.Net;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.AccountDeletion;

/// <summary>
/// Feature 037 T018 / T066 — the ban-bars / deletion-permits asymmetry (spec FR-032), and the
/// enumeration-oracle guarantee that comes with it (SC-008).
/// </summary>
/// <remarks>
/// <para>
/// <b>Both directions are asserted, deliberately.</b> A ban must keep barring re-registration with
/// the same address; a self-deletion must release it. The two outcomes come out of one code path —
/// registration's <c>FindByEmailAsync</c> either finds a retained banned row or finds nothing — so a
/// regression in either direction looks exactly like a passing test of the other unless both are
/// pinned (SC-013).
/// </para>
/// <para>
/// <b>Never assert on the HTTP status alone.</b> Registration returns the same neutral acceptance
/// whether or not it created anything — that is its anti-enumeration design. A broken release would
/// therefore report success while creating nothing, and the returning member would be told they had
/// registered when they had not. Every test here checks the <em>row</em>, and signs in.
/// </para>
/// </remarks>
[Collection("AccountDeletion")]
public sealed class ReRegistrationTests : AccountDeletionTestSupport
{
    public ReRegistrationTests(JuggerHubApiFactory factory) : base(factory) { }

    private Task<int> AccountsForAsync(string email) =>
        WithDbAsync(db => db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == email));

    // --- 6a: deletion releases the address ------------------------------------

    [Fact]
    public async Task After_erasure_the_same_address_can_register_a_working_account()
    {
        var (client, oldUserId, _, email) = await NewMemberAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(client)).StatusCode);

        // The address is genuinely released, not merely blanked on one column.
        Assert.Equal(0, await AccountsForAsync(email));

        // Register again with the SAME address.
        var fresh = Factory.CreateClient();
        var (newUserId, _) = await AuthTestHelpers.RegisterAndVerifyAsync(fresh, Factory, email: email);

        // The account must actually EXIST — this is the assertion that catches a residual username
        // collision, which would fail CreateAsync and land on registration's neutral acceptance.
        Assert.Equal(1, await AccountsForAsync(email));
        Assert.NotEqual(oldUserId, newUserId);

        // And it must work.
        (await AuthTestHelpers.LoginAsync(fresh, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_returning_account_inherits_nothing_from_the_erased_one()
    {
        var (client, oldUserId, oldHandle, email) = await NewMemberAsync();

        var teamId = await CreateTeamWithSoleAdminAsync(oldUserId);
        var (_, keeperId, _, _) = await NewMemberAsync();
        await AddTeamAdminAsync(teamId, keeperId);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(client)).StatusCode);

        var fresh = Factory.CreateClient();
        var (newUserId, _) = await AuthTestHelpers.RegisterAndVerifyAsync(fresh, Factory, email: email);

        await WithDbAsync(async db =>
        {
            // No memberships, no history, and a different handle — a genuinely new account (FR-035).
            Assert.False(await db.TeamMemberships.AnyAsync(m => m.UserId == newUserId));
            Assert.False(await db.Notifications.AnyAsync(n => n.RecipientUserId == newUserId));

            var newHandle = await db.PlayerProfiles.IgnoreQueryFilters()
                .Where(p => p.UserId == newUserId).Select(p => p.Handle).SingleAsync();
            Assert.NotEqual(oldHandle, newHandle);

            // The old row still exists and is still terminal — the new account did not revive it.
            Assert.Equal(AccountStatus.Deleted, await db.Users.IgnoreQueryFilters()
                .Where(u => u.Id == oldUserId).Select(u => u.Status).SingleAsync());
        });
    }

    // --- 6b: a ban keeps barring the address ----------------------------------

    [Fact]
    public async Task After_a_ban_the_same_address_still_cannot_register()
    {
        var (_, bannedUserId, _, email) = await NewMemberAsync();
        await SetStatusAsync(bannedUserId, AccountStatus.Banned);

        // The banned row is RETAINED with its email — that retained address is the denylist.
        Assert.Equal(1, await AccountsForAsync(email));

        var fresh = Factory.CreateClient();
        var register = await AuthTestHelpers.RegisterAsync(fresh, email);

        // Neutral acceptance (anti-enumeration): the response must NOT reveal that the address is
        // banned. The proof is that no second account appeared.
        Assert.True(register.IsSuccessStatusCode);
        Assert.Equal(1, await AccountsForAsync(email));

        // And the new password does not work, because no new account was made.
        var login = await AuthTestHelpers.LoginAsync(fresh, email, AuthTestHelpers.ValidPassword);
        Assert.False(login.IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_banned_account_cannot_erase_itself_to_free_its_address()
    {
        var (client, userId, _, email) = await NewMemberAsync();

        // Ban AFTER signing in, so the client still holds a valid token. This is the point: the
        // refusal must come from the server-side status check, not from the sign-in gate (FR-005).
        await SetStatusAsync(userId, AccountStatus.Banned);

        var resp = await DeleteAccountAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        // Untouched — the address is still held, so the ban still bars re-registration.
        Assert.Equal(AccountStatus.Banned, await StatusAsync(userId));
        Assert.Equal(1, await AccountsForAsync(email));
        Assert.True(await HasProfileAsync(userId));
    }

    [Fact]
    public async Task A_suspended_account_cannot_erase_itself()
    {
        var (client, userId, _, _) = await NewMemberAsync();
        await SetStatusAsync(userId, AccountStatus.Suspended);

        Assert.Equal(HttpStatusCode.Forbidden, (await DeleteAccountAsync(client)).StatusCode);
        Assert.Equal(AccountStatus.Suspended, await StatusAsync(userId));
        Assert.True(await HasProfileAsync(userId));
    }

    // --- T066: no enumeration oracle (SC-008) ---------------------------------

    [Fact]
    public async Task Sign_in_with_erased_credentials_is_indistinguishable_from_an_unknown_address()
    {
        var (client, _, _, erasedEmail) = await NewMemberAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(client)).StatusCode);

        var probe = Factory.CreateClient();
        var erased = await AuthTestHelpers.LoginAsync(probe, erasedEmail, AuthTestHelpers.ValidPassword);
        var neverExisted = await AuthTestHelpers.LoginAsync(
            probe, $"nobody-{Guid.NewGuid():N}@example.test", AuthTestHelpers.ValidPassword);

        // Same status AND same body. A status check on AccountStatus.Deleted is exactly how an oracle
        // gets introduced here, so assert the equivalence rather than assuming it (Principle I).
        Assert.Equal(neverExisted.StatusCode, erased.StatusCode);
        Assert.False(erased.IsSuccessStatusCode);

        // traceId is stamped per request and differs between ANY two calls, erased or not, so it
        // carries no information about the account. Everything else must match exactly.
        Assert.Equal(
            WithoutTraceId(await neverExisted.Content.ReadAsStringAsync()),
            WithoutTraceId(await erased.Content.ReadAsStringAsync()));
    }

    private static string WithoutTraceId(string body) =>
        System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"<per-request>\"");
}
