using System.Net;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.AccountDeletion;

/// <summary>
/// Feature 037 T017 — erasure is all-or-nothing (FR-038), repeat-safe (FR-039), and refuses cleanly
/// (FR-004, FR-003).
/// </summary>
/// <remarks>
/// A partially erased account is the worst outcome this feature can produce: the member cannot sign
/// in, cannot ask for help through an account they no longer have, and their data is half there. So
/// every refusal path is asserted to leave the account <em>completely</em> untouched, not merely
/// "still present".
/// </remarks>
[Collection("AccountDeletion")]
public sealed class ErasureAtomicityTests : AccountDeletionTestSupport
{
    public ErasureAtomicityTests(JuggerHubApiFactory factory) : base(factory) { }

    /// <summary>Everything a live account should still have. Used to prove "untouched", not "present".</summary>
    private async Task AssertFullyIntactAsync(Guid userId, string email)
    {
        await WithDbAsync(async db =>
        {
            var user = await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Status, u.Email, u.PasswordHash, u.UserName })
                .SingleAsync();

            Assert.Equal(AccountStatus.Active, user.Status);
            Assert.Equal(email, user.Email);
            Assert.NotNull(user.PasswordHash);
            Assert.DoesNotContain("deleted-", user.UserName!);

            Assert.True(await db.PlayerProfiles.IgnoreQueryFilters().AnyAsync(p => p.UserId == userId));
            Assert.True(await db.RefreshTokens.AnyAsync(t => t.UserId == userId));
        });
    }

    [Fact]
    public async Task A_wrong_confirmation_changes_nothing_and_costs_no_password_attempt()
    {
        var (client, userId, _, email) = await NewMemberAsync();

        var resp = await DeleteAccountAsync(client, confirmation: "yes please");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        await AssertFullyIntactAsync(userId, email);

        // The confirmation is checked BEFORE the password, so a typo here must not have consumed a
        // lockout attempt — the correct call still works.
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(client)).StatusCode);
    }

    [Fact]
    public async Task A_wrong_password_changes_nothing()
    {
        var (client, userId, _, email) = await NewMemberAsync();

        var resp = await DeleteAccountAsync(client, password: "N0t-the-right-one!");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        await AssertFullyIntactAsync(userId, email);
    }

    [Fact]
    public async Task A_blocked_erasure_changes_nothing()
    {
        var (client, userId, _, email) = await NewMemberAsync();
        await CreateTeamWithSoleAdminAsync(userId, "Alleinverwaltet");

        var resp = await DeleteAccountAsync(client);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        // 409 means NOTHING happened — the precondition is re-checked inside the transaction, so a
        // blocker cannot be discovered halfway through.
        await AssertFullyIntactAsync(userId, email);
    }

    [Fact]
    public async Task A_second_erasure_is_harmless()
    {
        var (client, userId, _, _) = await NewMemberAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(client)).StatusCode);
        var statusAfterFirst = await StatusAsync(userId);

        // The same client still holds an access token whose lifetime has not expired. The second call
        // must not erase anything a second time, and must not succeed.
        var second = await DeleteAccountAsync(client);
        Assert.False(second.IsSuccessStatusCode);

        Assert.Equal(statusAfterFirst, await StatusAsync(userId));
        Assert.False(await HasProfileAsync(userId));
    }

    [Fact]
    public async Task Erasure_ends_every_session_on_every_device()
    {
        var (first, userId, _, email) = await NewMemberAsync();

        // A second signed-in device for the same member.
        var second = Factory.CreateClient();
        (await AuthTestHelpers.LoginAsync(second, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        Assert.True(await WithDbAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == userId)) >= 2);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(first)).StatusCode);

        // No session records survive at all — they carry a per-session originating IP (FR-016).
        Assert.Equal(0, await WithDbAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == userId)));

        // And the other device cannot refresh its way back in.
        var refresh = await second.PostAsync("/api/v1/auth/refresh", null);
        Assert.False(refresh.IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_member_cannot_erase_anyone_else()
    {
        var (attacker, _, _, _) = await NewMemberAsync();
        var (_, victimId, _, victimEmail) = await NewMemberAsync();

        // There is no request shape that names another account — the endpoint takes only a password
        // and a confirmation (FR-002). The attacker's own correct credentials erase only themselves.
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAccountAsync(attacker)).StatusCode);

        await AssertFullyIntactAsync(victimId, victimEmail);
    }
}
