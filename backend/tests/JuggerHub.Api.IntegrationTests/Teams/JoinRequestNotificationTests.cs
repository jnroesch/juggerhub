using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Home;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Feature 058 (GH #360): a join request reaches the people who decide it, and the answer reaches the
/// player. The fakes on the factory (email, push, realtime) are shared by every test in the
/// collection, so each assertion is scoped to this test's own users.
/// </summary>
[Collection("Teams")]
public sealed class JoinRequestNotificationTests
{
    private const string AlertType = "TeamJoinRequest";

    private readonly JuggerHubApiFactory _factory;

    public JoinRequestNotificationTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US1: the admins hear about a request ----------------------------------------------

    [Fact]
    public async Task Every_current_admin_gets_one_alert_and_nobody_else()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();

        await RequestAsync(player, team.Slug);

        Assert.Single(await AlertsAsync(team.A));
        Assert.Single(await AlertsAsync(team.B));
        Assert.Empty(await AlertsAsync(team.M));
        Assert.Empty(await AlertsAsync(player));
        Assert.Empty(EmailsTo(team.M, team.Name));
        Assert.Empty(EmailsTo(player, team.Name));
    }

    [Fact]
    public async Task The_stored_alert_carries_no_name_or_handle()
    {
        // 037 FR-023: the admin's row outlives the player's account, so it must not keep a copy of
        // who they were. The player is the row's actor; the payload says which request and team.
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        await RequestAsync(player, team.Slug);

        var displayName = await DisplayNameAsync(player.Id);
        var requestId = await RequestIdAsync(team.Id, player.Id);
        var row = await HomeTestSupport.WithDbAsync(_factory, db => db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == team.A.Id && n.Type == NotificationType.TeamJoinRequest)
            .Select(n => new { n.Payload, n.ActorUserId, n.DedupeKey })
            .SingleAsync());

        using var payload = JsonDocument.Parse(row.Payload);
        Assert.Equal(
            new[] { "requestId", "teamName", "teamSlug" },
            payload.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.DoesNotContain(displayName, row.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(player.Handle, row.Payload, StringComparison.Ordinal);
        Assert.Equal(player.Id, row.ActorUserId);
        Assert.Equal($"join-request:{requestId}:{team.A.Id}", row.DedupeKey);
    }

    [Fact]
    public async Task The_alert_shows_the_players_current_name()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        await RequestAsync(player, team.Slug);

        await HomeTestSupport.WithDbAsync(_factory, db => db.PlayerProfiles
            .Where(p => p.UserId == player.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.DisplayName, "Renamed Player")));

        var alert = Assert.Single(await AlertsAsync(team.A));
        Assert.Equal("Renamed Player", alert.GetProperty("actorDisplayName").GetString());
        Assert.False(alert.GetProperty("resolved").GetBoolean());
    }

    [Fact]
    public async Task Asking_again_while_waiting_announces_nothing()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();

        await RequestAsync(player, team.Slug);
        var again = await player.Client.PostAsync($"/api/v1/teams/{team.Slug}/join-requests", null);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);

        Assert.Single(await AlertsAsync(team.A));
        Assert.Single(EmailsTo(team.A, team.Name));
        Assert.Single(PushesTo(team.A, "join-request:"));
    }

    [Fact]
    public async Task Each_admin_is_emailed_in_their_own_language()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        await SetLanguageAsync(team.A, "de");
        await SetLanguageAsync(team.B, "en");

        await RequestAsync(player, team.Slug);

        var displayName = await DisplayNameAsync(player.Id);
        var german = Assert.Single(EmailsTo(team.A, team.Name));
        Assert.Equal($"{displayName} möchte {team.Name} beitreten — JuggerHub", german.Subject);
        Assert.Contains($"/t/{team.Slug}", german.HtmlBody, StringComparison.Ordinal);

        var english = Assert.Single(EmailsTo(team.B, team.Name));
        Assert.Equal($"{displayName} wants to join {team.Name} — JuggerHub", english.Subject);
    }

    [Fact]
    public async Task The_device_notification_names_the_team_and_the_player()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();

        await RequestAsync(player, team.Slug);

        var displayName = await DisplayNameAsync(player.Id);
        var requestId = await RequestIdAsync(team.Id, player.Id);
        var push = Assert.Single(PushesTo(team.A, "join-request:"));
        Assert.Equal(team.Name, push.Content.Title);
        Assert.Equal($"{displayName} wants to join the team", push.Content.Body);
        Assert.Equal($"/t/{team.Slug}", push.Content.Url);
        Assert.Equal($"join-request:{requestId}", push.Content.Tag);
        Assert.DoesNotContain(player.Id, _factory.PushDispatcher.Recipients);
    }

    [Fact]
    public async Task A_banned_or_erased_player_is_named_by_no_one()
    {
        var team = await NewTeamAsync();
        var banned = await NewUserAsync();
        var erased = await NewUserAsync();
        await RequestAsync(banned, team.Slug);
        await RequestAsync(erased, team.Slug);

        await HomeTestSupport.WithDbAsync(_factory, db => db.Users.Where(u => u.Id == banned.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, AccountStatus.Banned)));
        var deletion = await erased.Client.PostAsJsonAsync(
            "/api/v1/account/deletion", new { password = AuthTestHelpers.ValidPassword, confirmation = "DELETE" });
        deletion.EnsureSuccessStatusCode();

        var alerts = await AlertsAsync(team.A);
        Assert.Equal(2, alerts.Count);
        Assert.All(alerts, a =>
        {
            Assert.Equal(JsonValueKind.Null, a.GetProperty("actorDisplayName").ValueKind);
            Assert.True(a.GetProperty("resolved").GetBoolean());
        });
    }

    [Fact]
    public async Task An_answered_request_reads_as_no_longer_waiting()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        await RequestAsync(player, team.Slug);
        var requestId = await RequestIdAsync(team.Id, player.Id);

        (await team.A.Client.PostAsync($"/api/v1/teams/{team.Slug}/join-requests/{requestId}/approve", null))
            .EnsureSuccessStatusCode();

        Assert.True(Assert.Single(await AlertsAsync(team.A)).GetProperty("resolved").GetBoolean());
        Assert.True(Assert.Single(await AlertsAsync(team.B)).GetProperty("resolved").GetBoolean());
    }

    [Fact]
    public async Task A_failed_push_never_fails_the_request()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        _factory.PushDispatcher.ThrowOnDispatch = true;

        try
        {
            var response = await player.Client.PostAsync($"/api/v1/teams/{team.Slug}/join-requests", null);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        finally
        {
            _factory.PushDispatcher.ThrowOnDispatch = false;
        }

        Assert.Single(await AlertsAsync(team.A));
    }

    // --- US2: the player hears the answer --------------------------------------------------

    private const string AnswerType = "TeamJoinRequestAnswered";

    [Fact]
    public async Task Approving_tells_the_player_once_and_names_no_admin()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        await RequestAsync(player, team.Slug);
        var requestId = await RequestIdAsync(team.Id, player.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await AnswerAsync(team.A, team.Slug, requestId, approve: true)).StatusCode);

        var answer = Assert.Single(await AlertsAsync(player, AnswerType));
        var payload = answer.GetProperty("payload");
        Assert.True(payload.GetProperty("accepted").GetBoolean());
        Assert.Equal(team.Slug, payload.GetProperty("teamSlug").GetString());
        Assert.Equal(team.Name, payload.GetProperty("teamName").GetString());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("actorDisplayName").ValueKind);
        Assert.DoesNotContain(team.A.Handle, answer.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(await DisplayNameAsync(team.A.Id), answer.GetRawText(), StringComparison.Ordinal);
        Assert.True(await IsMemberAsync(team.Id, player.Id));
    }

    [Fact]
    public async Task Declining_tells_the_player_once()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        await RequestAsync(player, team.Slug);
        var requestId = await RequestIdAsync(team.Id, player.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await AnswerAsync(team.B, team.Slug, requestId, approve: false)).StatusCode);

        var answer = Assert.Single(await AlertsAsync(player, AnswerType));
        Assert.False(answer.GetProperty("payload").GetProperty("accepted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("actorDisplayName").ValueKind);
        Assert.False(await IsMemberAsync(team.Id, player.Id));
    }

    [Fact]
    public async Task Two_simultaneous_answers_take_effect_once()
    {
        // FR-012. Before feature 058 both answers were applied, which nobody could see; once the
        // answer is sent, the player would have been told "accepted" and "declined" at once.
        //
        // Several requests race at once, so the two answers to each really do overlap in the
        // database — a single pair can happen to run one after the other and pass either way.
        var team = await NewTeamAsync();
        var players = new List<(Actor Player, Guid RequestId)>();
        for (var i = 0; i < 4; i++)
        {
            var player = await NewUserAsync();
            await RequestAsync(player, team.Slug);
            players.Add((player, await RequestIdAsync(team.Id, player.Id)));
        }

        var responses = await Task.WhenAll(players.SelectMany(p => new[]
        {
            AnswerAsync(team.A, team.Slug, p.RequestId, approve: true),
            AnswerAsync(team.B, team.Slug, p.RequestId, approve: false),
        }));

        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.NotFound));

        foreach (var (player, requestId) in players)
        {
            var answer = Assert.Single(await AlertsAsync(player, AnswerType));
            var accepted = answer.GetProperty("payload").GetProperty("accepted").GetBoolean();
            Assert.Equal(accepted, await IsMemberAsync(team.Id, player.Id));
            Assert.Equal(
                accepted ? JoinRequestStatus.Approved : JoinRequestStatus.Declined,
                await HomeTestSupport.WithDbAsync(_factory, db =>
                    db.TeamJoinRequests.Where(r => r.Id == requestId).Select(r => r.Status).SingleAsync()));
        }
    }

    [Fact]
    public async Task Answering_a_withdrawn_request_is_refused_and_tells_no_one()
    {
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        await RequestAsync(player, team.Slug);
        var requestId = await RequestIdAsync(team.Id, player.Id);
        (await player.Client.DeleteAsync($"/api/v1/teams/{team.Slug}/join-requests/mine")).EnsureSuccessStatusCode();

        var response = await AnswerAsync(team.A, team.Slug, requestId, approve: true);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await AlertsAsync(player, AnswerType));
        Assert.False(await IsMemberAsync(team.Id, player.Id));
    }

    [Fact]
    public async Task Answering_a_banned_players_request_is_refused()
    {
        // FR-016/FR-021: only a waiting request can be answered, and a ban stops it waiting.
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        await RequestAsync(player, team.Slug);
        var requestId = await RequestIdAsync(team.Id, player.Id);
        await SetStatusAsync(player.Id, AccountStatus.Banned);

        Assert.Equal(HttpStatusCode.NotFound, (await AnswerAsync(team.A, team.Slug, requestId, approve: true)).StatusCode);
        Assert.False(await IsMemberAsync(team.Id, player.Id));

        // Lifting the ban restores the request, unchanged, and it can be answered again.
        await SetStatusAsync(player.Id, AccountStatus.Active);
        Assert.Equal(HttpStatusCode.NoContent, (await AnswerAsync(team.A, team.Slug, requestId, approve: true)).StatusCode);
        Assert.True(await IsMemberAsync(team.Id, player.Id));
    }

    [Fact]
    public async Task The_claim_sets_ModifiedDate_and_the_decision()
    {
        // The claim is an ExecuteUpdate, which the audit interceptor never sees (constitution III).
        var team = await NewTeamAsync();
        var player = await NewUserAsync();
        await RequestAsync(player, team.Slug);
        var requestId = await RequestIdAsync(team.Id, player.Id);
        var before = await RequestRowAsync(requestId);

        (await AnswerAsync(team.B, team.Slug, requestId, approve: false)).EnsureSuccessStatusCode();

        var after = await RequestRowAsync(requestId);
        Assert.Equal(JoinRequestStatus.Declined, after.Status);
        Assert.Equal(team.B.Id, after.DecidedByUserId);
        Assert.NotNull(after.DecidedDate);
        Assert.True(after.ModifiedDate > before.ModifiedDate, "ModifiedDate did not move on the claim.");
    }

    [Fact]
    public async Task The_player_is_emailed_the_answer_in_their_language()
    {
        var team = await NewTeamAsync();
        var accepted = await NewUserAsync();
        var declined = await NewUserAsync();
        await SetLanguageAsync(accepted, "es");
        await SetLanguageAsync(declined, "es");
        await RequestAsync(accepted, team.Slug);
        await RequestAsync(declined, team.Slug);

        (await AnswerAsync(team.A, team.Slug, await RequestIdAsync(team.Id, accepted.Id), approve: true)).EnsureSuccessStatusCode();
        (await AnswerAsync(team.A, team.Slug, await RequestIdAsync(team.Id, declined.Id), approve: false)).EnsureSuccessStatusCode();

        var yes = Assert.Single(EmailsTo(accepted, team.Name));
        Assert.Equal($"Ya formas parte de {team.Name} — JuggerHub", yes.Subject);
        Assert.Contains($"/t/{team.Slug}", yes.HtmlBody, StringComparison.Ordinal);

        var no = Assert.Single(EmailsTo(declined, team.Name));
        Assert.Equal($"Tu solicitud para unirte a {team.Name} — JuggerHub", no.Subject);
        Assert.Contains("/browse/teams", no.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_device_notification_says_accepted_or_declined()
    {
        var team = await NewTeamAsync();
        var accepted = await NewUserAsync();
        var declined = await NewUserAsync();
        await RequestAsync(accepted, team.Slug);
        await RequestAsync(declined, team.Slug);
        var acceptedId = await RequestIdAsync(team.Id, accepted.Id);

        (await AnswerAsync(team.A, team.Slug, acceptedId, approve: true)).EnsureSuccessStatusCode();
        (await AnswerAsync(team.A, team.Slug, await RequestIdAsync(team.Id, declined.Id), approve: false)).EnsureSuccessStatusCode();

        var yes = Assert.Single(PushesTo(accepted, "join-answer:"));
        Assert.Equal(team.Name, yes.Content.Title);
        Assert.Equal("Your request to join was accepted", yes.Content.Body);
        Assert.Equal($"/t/{team.Slug}", yes.Content.Url);
        Assert.Equal($"join-answer:{acceptedId}", yes.Content.Tag);

        var no = Assert.Single(PushesTo(declined, "join-answer:"));
        Assert.Equal("Your request to join was declined", no.Content.Body);
        Assert.Equal("/browse/teams", no.Content.Url);
    }

    // --- helpers ------------------------------------------------------------

    private sealed record Actor(HttpClient Client, Guid Id, string Handle, string Email);

    private sealed record TeamSetup(Guid Id, string Slug, string Name, Actor A, Actor B, Actor M);

    /// <summary>A team with two admins (A created it; B was made admin) and one plain member M.</summary>
    private async Task<TeamSetup> NewTeamAsync()
    {
        var a = await NewUserAsync();
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var name = "Hammers " + slug[1..7];
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
        var b = await NewUserAsync();
        var m = await NewUserAsync();
        await HomeTestSupport.AddMemberAsync(_factory, teamId, b.Id, TeamRole.Admin);
        await HomeTestSupport.AddMemberAsync(_factory, teamId, m.Id);
        return new TeamSetup(teamId, slug, name, a, b, m);
    }

    private async Task<Actor> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return new Actor(client, userId, handle, email);
    }

    private static async Task RequestAsync(Actor player, string slug)
    {
        var response = await player.Client.PostAsync($"/api/v1/teams/{slug}/join-requests", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task SetLanguageAsync(Actor actor, string language) =>
        (await actor.Client.PutAsJsonAsync("/api/v1/account/language", new { language })).EnsureSuccessStatusCode();

    /// <summary>The actor's alerts of the given type, newest first.</summary>
    private static async Task<List<JsonElement>> AlertsAsync(Actor actor, string type = AlertType)
    {
        var page = await actor.Client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?take=100");
        return page.GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("type").GetString() == type)
            .ToList();
    }

    /// <summary>Emails to the actor about this test's team (team names are unique per test).</summary>
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

    private static Task<HttpResponseMessage> AnswerAsync(Actor admin, string slug, Guid requestId, bool approve) =>
        admin.Client.PostAsync($"/api/v1/teams/{slug}/join-requests/{requestId}/{(approve ? "approve" : "decline")}", null);

    private Task<bool> IsMemberAsync(Guid teamId, Guid userId) =>
        HomeTestSupport.WithDbAsync(_factory, db =>
            db.TeamMemberships.AnyAsync(m => m.TeamId == teamId && m.UserId == userId));

    private Task SetStatusAsync(Guid userId, AccountStatus status) =>
        HomeTestSupport.WithDbAsync(_factory, db => db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, status)));

    private sealed record RequestRow(JoinRequestStatus Status, Guid? DecidedByUserId, DateTime? DecidedDate, DateTime ModifiedDate);

    private Task<RequestRow> RequestRowAsync(Guid requestId) =>
        HomeTestSupport.WithDbAsync(_factory, db => db.TeamJoinRequests.AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new RequestRow(r.Status, r.DecidedByUserId, r.DecidedDate, r.ModifiedDate))
            .SingleAsync());

    private Task<Guid> RequestIdAsync(Guid teamId, Guid userId) =>
        HomeTestSupport.WithDbAsync(_factory, db => db.TeamJoinRequests
            .Where(r => r.TeamId == teamId && r.UserId == userId)
            .OrderByDescending(r => r.CreatedDate)
            .Select(r => r.Id)
            .FirstAsync());
}
