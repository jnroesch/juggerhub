using System.Net;
using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Recognition;
using JuggerHub.Dtos.Admin;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Admin;

/// <summary>
/// Feature 013 US4 — the recorded, reversible account actions: state machine +
/// idempotence conflicts, the FR-019 admin shield, AdminActionRecord rows, refresh
/// token revocation, and the admin-triggered password reset.
/// </summary>
[Collection("AdminArea")]
public sealed class AccountActionTests
{
    private readonly JuggerHubApiFactory _factory;

    public AccountActionTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Suspend_ban_lifecycle_transitions_records_and_session_revocation()
    {
        var (admin, adminId) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var (_, playerId, handle, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);

        // The player logged in → has at least one live refresh token.
        Assert.True(await LiveRefreshTokensAsync(playerId) > 0);

        // Suspend: 204, state flips, record written, sessions revoked.
        Assert.Equal(HttpStatusCode.NoContent, (await Act(admin, handle, "suspend")).StatusCode);
        Assert.Equal(AccountStatus.Suspended, (await DetailAsync(admin, handle)).Status);
        Assert.Equal(0, await LiveRefreshTokensAsync(playerId));
        await AssertRecordedAsync(adminId, playerId, AdminAccountAction.Suspend);

        // Idempotence guard: suspending again is a 409, and no second record appears.
        Assert.Equal(HttpStatusCode.Conflict, (await Act(admin, handle, "suspend")).StatusCode);
        Assert.Equal(1, await RecordCountAsync(playerId, AdminAccountAction.Suspend));

        // Ban is allowed from Suspended.
        Assert.Equal(HttpStatusCode.NoContent, (await Act(admin, handle, "ban")).StatusCode);
        Assert.Equal(AccountStatus.Banned, (await DetailAsync(admin, handle)).Status);
        await AssertRecordedAsync(adminId, playerId, AdminAccountAction.Ban);

        // Reinstate applies to Suspended only → 409 while banned; unban restores Active.
        Assert.Equal(HttpStatusCode.Conflict, (await Act(admin, handle, "reinstate")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Act(admin, handle, "unban")).StatusCode);
        Assert.Equal(AccountStatus.Active, (await DetailAsync(admin, handle)).Status);
        await AssertRecordedAsync(adminId, playerId, AdminAccountAction.Unban);
    }

    [Fact]
    public async Task Admins_are_shielded_from_suspend_and_ban()
    {
        var (admin, adminId) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var adminHandle = await AdminAreaTestSupport.WithDbAsync(_factory, db =>
            db.PlayerProfiles.IgnoreQueryFilters()
                .Where(p => p.UserId == adminId).Select(p => p.Handle).SingleAsync());

        // Self (also: a designated admin) — refused with the config explanation.
        var suspend = await Act(admin, adminHandle, "suspend");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, suspend.StatusCode);
        var ban = await Act(admin, adminHandle, "ban");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, ban.StatusCode);

        Assert.Equal(AccountStatus.Active, (await DetailAsync(admin, adminHandle)).Status);
    }

    [Fact]
    public async Task Reset_password_sends_the_standard_email_and_is_recorded()
    {
        var (admin, adminId) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        var (_, playerId, handle, email) = await AdminAreaTestSupport.PlayerClientAsync(_factory);

        Assert.Equal(HttpStatusCode.NoContent, (await Act(admin, handle, "reset-password")).StatusCode);

        // The target got the platform's standard reset email (with a parseable link).
        var mail = _factory.EmailSender.LatestFor(email);
        Assert.NotNull(mail);
        Assert.Contains("token=", mail!.HtmlBody);

        await AssertRecordedAsync(adminId, playerId, AdminAccountAction.PasswordResetSent);
    }

    [Fact]
    public async Task Unknown_handle_is_a_404_and_actions_require_admin()
    {
        var (admin, _) = await AdminAreaTestSupport.AdminClientAsync(_factory);
        Assert.Equal(HttpStatusCode.NotFound, (await Act(admin, "no-such-player", "suspend")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/v1/admin/users/no-such-player")).StatusCode);

        var (player, _, ownHandle, _) = await AdminAreaTestSupport.PlayerClientAsync(_factory);
        foreach (var action in new[] { "suspend", "reinstate", "ban", "unban", "reset-password" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Act(player, ownHandle, action)).StatusCode);
        }
    }

    private static Task<HttpResponseMessage> Act(HttpClient client, string handle, string action) =>
        client.PostAsync($"/api/v1/admin/users/{handle}/{action}", null);

    private static async Task<AdminUserDetailDto> DetailAsync(HttpClient admin, string handle)
    {
        var resp = await admin.GetAsync($"/api/v1/admin/users/{handle}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<AdminUserDetailDto>(AdminAreaTestSupport.Json))!;
    }

    private Task<int> LiveRefreshTokensAsync(Guid userId) =>
        AdminAreaTestSupport.WithDbAsync(_factory, db =>
            db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null));

    private Task<int> RecordCountAsync(Guid targetId, AdminAccountAction action) =>
        AdminAreaTestSupport.WithDbAsync(_factory, db =>
            db.AdminActionRecords.CountAsync(r => r.TargetUserId == targetId && r.Action == action));

    private async Task AssertRecordedAsync(Guid actorId, Guid targetId, AdminAccountAction action)
    {
        var recorded = await AdminAreaTestSupport.WithDbAsync(_factory, db =>
            db.AdminActionRecords.AnyAsync(r =>
                r.ActorUserId == actorId && r.TargetUserId == targetId && r.Action == action));
        Assert.True(recorded, $"Expected an AdminActionRecord for {action}.");
    }
}
