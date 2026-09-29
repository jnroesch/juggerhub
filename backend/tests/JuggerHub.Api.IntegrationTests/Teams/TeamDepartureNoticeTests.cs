using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Home;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Feature 064 (GH #385): a player an admin removes is told, and the team's other admins are told of
/// every departure, whether the player left or was removed. The fakes on the factory (email, push) are
/// shared by every test in the collection, so each assertion is scoped to this test's own users and
/// team (team names are unique per test).
/// </summary>
[Collection("Teams")]
public sealed class TeamDepartureNoticeTests
{
    private const string RemovedType = "TeamMemberRemoved";
    private const string DepartedType = "TeamMemberDeparted";

    private readonly JuggerHubApiFactory _factory;

    public TeamDepartureNoticeTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US2: the removed player is told -----------------------------------------------------

    [Fact]
    public async Task A_removed_player_gets_one_alert_naming_the_team_and_nobody_else()
    {
        var team = await NewTeamAsync();
        var membershipId = await MembershipIdAsync(team.Id, team.M.Id);

        await RemoveAsync(team.A, team.Slug, team.M);

        var alert = Assert.Single(await AlertsAsync(team.M, RemovedType));
        Assert.Equal(JsonValueKind.Null, alert.GetProperty("actorDisplayName").ValueKind);
        Assert.Equal(team.Name, alert.GetProperty("payload").GetProperty("teamName").GetString());

        // The stored row: the team and nothing else — no actor, no admin in the payload (FR-011).
        var row = await RowAsync(team.M.Id, NotificationType.TeamMemberRemoved);
        using var payload = JsonDocument.Parse(row.Payload);
        Assert.Equal(new[] { "teamName", "teamSlug" }, PropertyNames(payload));
        Assert.Null(row.ActorUserId);
        Assert.Equal($"team-removed:{membershipId}", row.DedupeKey);
        Assert.DoesNotContain(await DisplayNameAsync(team.A.Id), row.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(team.A.Handle, row.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_removed_player_gets_one_email_and_one_push_that_name_no_admin()
    {
        var team = await NewTeamAsync();
        var adminName = await DisplayNameAsync(team.A.Id);

        await RemoveAsync(team.A, team.Slug, team.M);

        var email = Assert.Single(EmailsTo(team.M, team.Name));
        Assert.Equal($"You're no longer a member of {team.Name} — JuggerHub", email.Subject);
        Assert.Contains($"/t/{team.Slug}", email.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain(adminName, email.HtmlBody, StringComparison.Ordinal);

        var push = Assert.Single(PushesTo(team.M, "team-removed:"));
        Assert.Equal(team.Name, push.Content.Title);
        Assert.Equal("You're no longer a member of this team", push.Content.Body);
        Assert.Equal($"/t/{team.Slug}", push.Content.Url);
        Assert.DoesNotContain(adminName, push.Content.Title + push.Content.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_removal_email_is_in_the_removed_players_language()
    {
        var team = await NewTeamAsync();
        await SetLanguageAsync(team.M, "de");

        await RemoveAsync(team.A, team.Slug, team.M);

        var email = Assert.Single(EmailsTo(team.M, team.Name));
        Assert.Equal($"Du bist kein Mitglied von {team.Name} mehr — JuggerHub", email.Subject);
        Assert.Contains("Zur Teamseite", email.HtmlBody, StringComparison.Ordinal);
        Assert.Equal("Du bist kein Mitglied dieses Teams mehr", Assert.Single(PushesTo(team.M, "team-removed:")).Content.Body);
    }

    [Theory]
    [InlineData("InApp")]
    [InlineData("Email")]
    [InlineData("Push")]
    public async Task Each_channel_follows_only_the_removed_players_own_setting(string channelOff)
    {
        var team = await NewTeamAsync();
        await SetPreferenceAsync(team.M, "InvitesAndRoster", channelOff, enabled: false);

        await RemoveAsync(team.A, team.Slug, team.M);

        Assert.Equal(channelOff == "InApp" ? 0 : 1, (await RowsAsync(team.M.Id, NotificationType.TeamMemberRemoved)).Count);
        Assert.Equal(channelOff == "Email" ? 0 : 1, EmailsTo(team.M, team.Name).Count);
        Assert.Equal(channelOff == "Push" ? 0 : 1, PushesTo(team.M, "team-removed:").Count);
    }

    [Fact]
    public async Task A_member_who_leaves_is_not_told_about_their_own_departure()
    {
        var team = await NewTeamAsync();

        await LeaveAsync(team.M, team.Slug);

        Assert.Empty(await RowsAsync(team.M.Id, NotificationType.TeamMemberRemoved));
        Assert.Empty(await RowsAsync(team.M.Id, NotificationType.TeamMemberDeparted));
        Assert.Empty(EmailsTo(team.M, team.Name));
        Assert.Empty(PushesTo(team.M, "team-"));
    }

    [Fact]
    public async Task Removing_twice_tells_the_player_once()
    {
        var team = await NewTeamAsync();

        await RemoveAsync(team.A, team.Slug, team.M);
        var again = await team.B.Client.DeleteAsync($"/api/v1/teams/{team.Slug}/members/{team.M.Id}");

        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Single(await RowsAsync(team.M.Id, NotificationType.TeamMemberRemoved));
        Assert.Single(EmailsTo(team.M, team.Name));
        Assert.Single(PushesTo(team.M, "team-removed:"));
        // The admins were told once too.
        Assert.Single(await RowsAsync(team.B.Id, NotificationType.TeamMemberDeparted));
    }

    [Fact]
    public async Task A_player_who_rejoins_and_is_removed_again_is_told_again()
    {
        var team = await NewTeamAsync();
        var first = await MembershipIdAsync(team.Id, team.M.Id);
        await RemoveAsync(team.A, team.Slug, team.M);

        await JoinByLinkAsync(team.A, team.M, team.Slug);
        var second = await MembershipIdAsync(team.Id, team.M.Id);
        await RemoveAsync(team.A, team.Slug, team.M);

        Assert.NotEqual(first, second);
        var rows = await RowsAsync(team.M.Id, NotificationType.TeamMemberRemoved);
        Assert.Equal(
            new[] { $"team-removed:{first}", $"team-removed:{second}" }.Order(),
            rows.Select(r => r.DedupeKey).Order());
        Assert.Equal(2, (await RowsAsync(team.B.Id, NotificationType.TeamMemberDeparted)).Count);
    }

    [Fact]
    public async Task Answers_are_unchanged()
    {
        // FR-021: who may remove whom, the last-admin rule, and the answers themselves.
        var team = await NewTeamAsync();
        var plain = await NewUserAsync();
        await HomeTestSupport.AddMemberAsync(_factory, team.Id, plain.Id);

        var byMember = await team.M.Client.DeleteAsync($"/api/v1/teams/{team.Slug}/members/{plain.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);

        var soleAdminTeam = await NewSoloTeamAsync();
        var lastAdminLeaves = await soleAdminTeam.A.Client.DeleteAsync($"/api/v1/teams/{soleAdminTeam.Slug}/members/{soleAdminTeam.A.Id}");
        Assert.Equal(HttpStatusCode.Conflict, lastAdminLeaves.StatusCode);

        // Nothing was sent for either refusal.
        Assert.Empty(await RowsAsync(plain.Id, NotificationType.TeamMemberRemoved));
        Assert.Empty(await RowsAsync(team.A.Id, NotificationType.TeamMemberDeparted));
        Assert.Empty(await RowsAsync(team.B.Id, NotificationType.TeamMemberDeparted));
    }

    [Fact]
    public async Task Deleting_the_team_or_an_account_sends_none_of_these_notices()
    {
        // FR-019: only leaving and removal are departures.
        var team = await NewTeamAsync();
        var deletion = await team.M.Client.PostAsJsonAsync(
            "/api/v1/account/deletion", new { password = AuthTestHelpers.ValidPassword, confirmation = "DELETE" });
        deletion.EnsureSuccessStatusCode();

        Assert.Empty(await RowsAsync(team.A.Id, NotificationType.TeamMemberDeparted));
        Assert.Empty(await RowsAsync(team.B.Id, NotificationType.TeamMemberDeparted));

        var other = await NewUserAsync();
        await HomeTestSupport.AddMemberAsync(_factory, team.Id, other.Id);
        (await team.A.Client.DeleteAsync($"/api/v1/teams/{team.Slug}")).EnsureSuccessStatusCode();

        Assert.Empty(await RowsAsync(other.Id, NotificationType.TeamMemberRemoved));
        Assert.Empty(await RowsAsync(team.B.Id, NotificationType.TeamMemberDeparted));
        Assert.Empty(EmailsTo(other, team.Name));
    }

    [Fact]
    public async Task A_failed_push_or_email_never_fails_the_removal()
    {
        var team = await NewTeamAsync();
        _factory.PushDispatcher.ThrowOnDispatch = true;
        _factory.EmailSender.FailFor(team.M.Email);

        try
        {
            var response = await team.A.Client.DeleteAsync($"/api/v1/teams/{team.Slug}/members/{team.M.Id}");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        finally
        {
            _factory.PushDispatcher.ThrowOnDispatch = false;
            _factory.EmailSender.Heal(team.M.Email);
        }

        Assert.False(await IsMemberAsync(team.Id, team.M.Id));
        Assert.Single(await RowsAsync(team.M.Id, NotificationType.TeamMemberRemoved));
    }

    // --- US3: the other admins learn about departures ------------------------------------------

    [Fact]
    public async Task A_removal_tells_every_other_admin_and_not_the_one_who_did_it()
    {
        var team = await NewTeamAsync(thirdAdmin: true);

        await RemoveAsync(team.A, team.Slug, team.M);

        Assert.Empty(await RowsAsync(team.A.Id, NotificationType.TeamMemberDeparted));
        Assert.Empty(await RowsAsync(team.M.Id, NotificationType.TeamMemberDeparted));
        foreach (var admin in new[] { team.B, team.C! })
        {
            var alert = Assert.Single(await AlertsAsync(admin, DepartedType));
            Assert.True(alert.GetProperty("payload").GetProperty("removed").GetBoolean());
            Assert.Equal(await DisplayNameAsync(team.M.Id), alert.GetProperty("actorDisplayName").GetString());
        }
    }

    [Fact]
    public async Task Leaving_tells_every_admin_and_not_the_leaver()
    {
        var team = await NewTeamAsync(thirdAdmin: true);

        await LeaveAsync(team.M, team.Slug);

        foreach (var admin in new[] { team.A, team.B, team.C! })
        {
            var alert = Assert.Single(await AlertsAsync(admin, DepartedType));
            Assert.False(alert.GetProperty("payload").GetProperty("removed").GetBoolean());
        }
        Assert.Empty(await RowsAsync(team.M.Id, NotificationType.TeamMemberDeparted));
    }

    [Fact]
    public async Task An_admin_who_leaves_is_not_told_but_the_others_are()
    {
        var team = await NewTeamAsync(thirdAdmin: true);

        await LeaveAsync(team.B, team.Slug);

        Assert.Single(await RowsAsync(team.A.Id, NotificationType.TeamMemberDeparted));
        Assert.Single(await RowsAsync(team.C!.Id, NotificationType.TeamMemberDeparted));
        Assert.Empty(await RowsAsync(team.B.Id, NotificationType.TeamMemberDeparted));
    }

    [Fact]
    public async Task The_admins_row_names_the_player_only_through_its_actor()
    {
        // 037 FR-023: an admin's row outlives the player's account, so it keeps no copy of them.
        var team = await NewTeamAsync();
        var membershipId = await MembershipIdAsync(team.Id, team.M.Id);
        var name = await DisplayNameAsync(team.M.Id);

        await RemoveAsync(team.A, team.Slug, team.M);

        var row = await RowAsync(team.B.Id, NotificationType.TeamMemberDeparted);
        using var payload = JsonDocument.Parse(row.Payload);
        Assert.Equal(new[] { "removed", "teamName", "teamSlug" }, PropertyNames(payload));
        Assert.DoesNotContain(name, row.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(team.M.Handle, row.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(await DisplayNameAsync(team.A.Id), row.Payload, StringComparison.Ordinal);
        Assert.Equal(team.M.Id, row.ActorUserId);
        Assert.Equal($"team-departure:{membershipId}:{team.B.Id}", row.DedupeKey);
    }

    [Fact]
    public async Task The_admins_alert_follows_the_players_current_name_and_forgets_it_with_them()
    {
        var team = await NewTeamAsync();
        var banned = await NewUserAsync();
        var erased = await NewUserAsync();
        await HomeTestSupport.AddMemberAsync(_factory, team.Id, banned.Id);
        await HomeTestSupport.AddMemberAsync(_factory, team.Id, erased.Id);

        await LeaveAsync(team.M, team.Slug);
        await LeaveAsync(banned, team.Slug);
        await LeaveAsync(erased, team.Slug);

        await HomeTestSupport.WithDbAsync(_factory, db => db.PlayerProfiles.Where(p => p.UserId == team.M.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.DisplayName, "Renamed Rita")
                .SetProperty(p => p.ModifiedDate, DateTime.UtcNow)));
        await SetStatusAsync(banned.Id, AccountStatus.Banned);
        (await erased.Client.PostAsJsonAsync(
            "/api/v1/account/deletion", new { password = AuthTestHelpers.ValidPassword, confirmation = "DELETE" }))
            .EnsureSuccessStatusCode();

        var names = (await AlertsAsync(team.A, DepartedType))
            .Select(a => a.GetProperty("actorDisplayName").ValueKind == JsonValueKind.Null
                ? null
                : a.GetProperty("actorDisplayName").GetString())
            .ToList();
        Assert.Equal(3, names.Count);
        Assert.Contains("Renamed Rita", names);
        Assert.Equal(2, names.Count(n => n is null));
    }

    [Fact]
    public async Task A_sole_admin_removing_someone_tells_no_admin()
    {
        var team = await NewSoloTeamAsync();
        var m = await NewUserAsync();
        await HomeTestSupport.AddMemberAsync(_factory, team.Id, m.Id);

        await RemoveAsync(team.A, team.Slug, m);

        Assert.Equal(0, await HomeTestSupport.WithDbAsync(_factory, db =>
            db.Notifications.CountAsync(n => n.Type == NotificationType.TeamMemberDeparted && n.ActorUserId == m.Id)));
        Assert.Single(await RowsAsync(m.Id, NotificationType.TeamMemberRemoved));
    }

    [Fact]
    public async Task A_banned_admin_is_not_told()
    {
        var team = await NewTeamAsync(thirdAdmin: true);
        await SetStatusAsync(team.C!.Id, AccountStatus.Banned);

        await RemoveAsync(team.A, team.Slug, team.M);

        Assert.Single(await RowsAsync(team.B.Id, NotificationType.TeamMemberDeparted));
        Assert.Empty(await RowsAsync(team.C.Id, NotificationType.TeamMemberDeparted));
    }

    [Fact]
    public async Task The_admins_email_and_push_name_the_player_in_each_admins_language()
    {
        var team = await NewTeamAsync();
        await SetLanguageAsync(team.B, "de");
        var player = await DisplayNameAsync(team.M.Id);
        var remover = await DisplayNameAsync(team.A.Id);

        await RemoveAsync(team.A, team.Slug, team.M);

        var email = Assert.Single(EmailsTo(team.B, team.Name));
        Assert.Equal($"{player} wurde aus {team.Name} entfernt — JuggerHub", email.Subject);
        Assert.Contains(player, email.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain(remover, email.HtmlBody, StringComparison.Ordinal);
        Assert.Empty(EmailsTo(team.A, team.Name));

        var push = Assert.Single(PushesTo(team.B, "team-departure:"));
        Assert.Equal($"{player} wurde aus dem Team entfernt", push.Content.Body);
        Assert.Equal($"/t/{team.Slug}", push.Content.Url);
        Assert.DoesNotContain(team.A.Id, push.RecipientUserIds);
    }

    [Fact]
    public async Task Leaving_emails_the_admins_that_the_player_left()
    {
        var team = await NewTeamAsync();
        var player = await DisplayNameAsync(team.M.Id);

        await LeaveAsync(team.M, team.Slug);

        Assert.Equal($"{player} left {team.Name} — JuggerHub", Assert.Single(EmailsTo(team.A, team.Name)).Subject);
        Assert.Equal($"{player} left {team.Name} — JuggerHub", Assert.Single(EmailsTo(team.B, team.Name)).Subject);
    }

    [Fact]
    public async Task One_admins_failing_address_does_not_stop_the_others_email()
    {
        var team = await NewTeamAsync(thirdAdmin: true);
        _factory.EmailSender.FailFor(team.B.Email);

        try
        {
            var response = await team.A.Client.DeleteAsync($"/api/v1/teams/{team.Slug}/members/{team.M.Id}");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        finally
        {
            _factory.EmailSender.Heal(team.B.Email);
        }

        Assert.Single(EmailsTo(team.C!, team.Name));
    }

    [Theory]
    [InlineData("InApp")]
    [InlineData("Email")]
    [InlineData("Push")]
    public async Task Each_channel_follows_only_each_admins_own_setting(string channelOff)
    {
        var team = await NewTeamAsync();
        await SetPreferenceAsync(team.B, "InvitesAndRoster", channelOff, enabled: false);

        await LeaveAsync(team.M, team.Slug);

        Assert.Equal(channelOff == "InApp" ? 0 : 1, (await RowsAsync(team.B.Id, NotificationType.TeamMemberDeparted)).Count);
        Assert.Equal(channelOff == "Email" ? 0 : 1, EmailsTo(team.B, team.Name).Count);
        Assert.Equal(channelOff == "Push" ? 0 : 1, PushesTo(team.B, "team-departure:").Count);
        // A's settings are untouched, so A gets all three.
        Assert.Single(await RowsAsync(team.A.Id, NotificationType.TeamMemberDeparted));
        Assert.Single(EmailsTo(team.A, team.Name));
        Assert.Single(PushesTo(team.A, "team-departure:"));
    }

    // --- helpers ------------------------------------------------------------

    private sealed record Actor(HttpClient Client, Guid Id, string Handle, string Email);

    private sealed record TeamSetup(Guid Id, string Slug, string Name, Actor A, Actor B, Actor M, Actor? C);

    private sealed record Row(string Payload, Guid? ActorUserId, string? DedupeKey);

    /// <summary>A team with two admins (A created it; B was made admin), optionally a third (C), and one plain member M.</summary>
    private async Task<TeamSetup> NewTeamAsync(bool thirdAdmin = false)
    {
        var solo = await NewSoloTeamAsync();
        var b = await NewUserAsync();
        var m = await NewUserAsync();
        await HomeTestSupport.AddMemberAsync(_factory, solo.Id, b.Id, TeamRole.Admin);
        await HomeTestSupport.AddMemberAsync(_factory, solo.Id, m.Id);
        Actor? c = null;
        if (thirdAdmin)
        {
            c = await NewUserAsync();
            await HomeTestSupport.AddMemberAsync(_factory, solo.Id, c.Id, TeamRole.Admin);
        }

        return new TeamSetup(solo.Id, solo.Slug, solo.Name, solo.A, b, m, c);
    }

    private sealed record SoloTeam(Guid Id, string Slug, string Name, Actor A);

    /// <summary>A team whose only member is its creator and only admin, A.</summary>
    private async Task<SoloTeam> NewSoloTeamAsync()
    {
        var a = await NewUserAsync();
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var name = "Rheinfeuer " + slug[1..7];
        var created = await a.Client.PostAsJsonAsync("/api/v1/teams", new
        {
            name,
            slug,
            type = "CityTeam",
            location = new { cityExternalId = "TEST:berlin" },
        });
        created.EnsureSuccessStatusCode();

        var teamId = await HomeTestSupport.WithDbAsync(_factory, db =>
            db.Teams.Where(t => t.Slug == slug).Select(t => t.Id).SingleAsync());
        return new SoloTeam(teamId, slug, name, a);
    }

    private async Task<Actor> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return new Actor(client, userId, handle, email);
    }

    private static async Task RemoveAsync(Actor admin, string slug, Actor target)
    {
        var response = await admin.Client.DeleteAsync($"/api/v1/teams/{slug}/members/{target.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static Task LeaveAsync(Actor member, string slug) => RemoveAsync(member, slug, member);

    private static async Task JoinByLinkAsync(Actor admin, Actor player, string slug)
    {
        var link = await admin.Client.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        link.EnsureSuccessStatusCode();
        var token = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        (await player.Client.PostAsync($"/api/v1/invitations/{token}/accept", null)).EnsureSuccessStatusCode();
    }

    private static async Task SetLanguageAsync(Actor actor, string language) =>
        (await actor.Client.PutAsJsonAsync("/api/v1/account/language", new { language })).EnsureSuccessStatusCode();

    private static async Task SetPreferenceAsync(Actor actor, string category, string channel, bool enabled) =>
        (await actor.Client.PutAsJsonAsync($"/api/v1/notification-preferences/{category}/{channel}", new { enabled }))
            .EnsureSuccessStatusCode();

    /// <summary>The actor's alerts of the given type, as the inbox serves them.</summary>
    private static async Task<List<JsonElement>> AlertsAsync(Actor actor, string type)
    {
        var page = await actor.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?take=100");
        return page.GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("type").GetString() == type)
            .ToList();
    }

    private Task<List<Row>> RowsAsync(Guid recipientId, NotificationType type) =>
        HomeTestSupport.WithDbAsync(_factory, db => db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == recipientId && n.Type == type)
            .Select(n => new Row(n.Payload, n.ActorUserId, n.DedupeKey))
            .ToListAsync());

    private async Task<Row> RowAsync(Guid recipientId, NotificationType type) =>
        Assert.Single(await RowsAsync(recipientId, type));

    private static string[] PropertyNames(JsonDocument payload) =>
        payload.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();

    /// <summary>Emails to the actor about this test's team.</summary>
    private List<CapturedEmail> EmailsTo(Actor actor, string teamName) =>
        _factory.EmailSender.Sent
            .Where(e => string.Equals(e.To, actor.Email, StringComparison.OrdinalIgnoreCase)
                && e.Subject.Contains(teamName, StringComparison.Ordinal))
            .ToList();

    private List<Push.FakePushDispatcher.Dispatch> PushesTo(Actor actor, string tagPrefix) =>
        _factory.PushDispatcher.Dispatches
            .Where(d => d.RecipientUserIds.Contains(actor.Id) && d.Content.Tag.StartsWith(tagPrefix, StringComparison.Ordinal))
            .ToList();

    private Task<string> DisplayNameAsync(Guid userId) =>
        HomeTestSupport.WithDbAsync(_factory, db =>
            db.PlayerProfiles.Where(p => p.UserId == userId).Select(p => p.DisplayName).SingleAsync());

    private Task<Guid> MembershipIdAsync(Guid teamId, Guid userId) =>
        HomeTestSupport.WithDbAsync(_factory, db => db.TeamMemberships
            .Where(m => m.TeamId == teamId && m.UserId == userId)
            .Select(m => m.Id)
            .SingleAsync());

    private Task<bool> IsMemberAsync(Guid teamId, Guid userId) =>
        HomeTestSupport.WithDbAsync(_factory, db =>
            db.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.UserId == userId));

    private Task SetStatusAsync(Guid userId, AccountStatus status) =>
        HomeTestSupport.WithDbAsync(_factory, db => db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, status)));
}
