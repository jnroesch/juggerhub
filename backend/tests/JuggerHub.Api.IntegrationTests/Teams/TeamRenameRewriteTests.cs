using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Home;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Feature 061, FR-009/FR-011: a rename brings the team's name up to date in every delivered alert
/// that names the team — all nine kinds, for every recipient including people who have since left —
/// silently: nothing becomes unread, moves, or is sent again. Rows are found by the team's slug in
/// the payload, never through the roster, and never through a list of types. Real API + Postgres.
/// </summary>
/// <remarks>
/// The five team kinds are produced by the real flows. The four party/market kinds need an event,
/// a party and a market listing, so their rows are inserted directly, shaped exactly as their
/// producers build them (PartyService, PartyRosterService's nudge, PartyNewsService,
/// MarketRequestService) — including the nudge's null dedupe key, which a prefix lookup could never
/// find.
/// </remarks>
[Collection("Teams")]
public sealed class TeamRenameRewriteTests
{
    private const string OldName = "Rheinfuer";
    private const string NewName = "Rheinfeuer";

    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    private readonly JuggerHubApiFactory _factory;

    public TeamRenameRewriteTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_rename_shows_the_new_name_in_every_alert_that_names_the_team()
    {
        var s = await ArrangeAsync();
        var before = await RowsAsync(s.Recipients);

        (await RenameAsync(s, NewName)).EnsureSuccessStatusCode();

        var after = await RowsAsync(s.Recipients);
        var ours = after.Values.Where(r => r.TeamSlug == s.Slug).ToList();

        // Every kind is present, and every one of them now carries the new name.
        NotificationType[] kinds =
        [
            NotificationType.TeamInvite, NotificationType.TeamRoleChanged, NotificationType.TeamNews,
            NotificationType.TeamJoinRequest, NotificationType.TeamJoinRequestAnswered,
            NotificationType.PartyRequest, NotificationType.PartyNews, NotificationType.MarketInvite,
        ];
        Assert.Equal(kinds.OrderBy(t => t), ours.Select(r => r.Type).Distinct().OrderBy(t => t));
        Assert.Equal(2, ours.Count(r => r.Type == NotificationType.PartyRequest && r.Payload["teamName"] is not null));
        Assert.All(ours, r => Assert.Equal(NewName, (string?)r.Payload["teamName"]));

        // Including the member who has left since the alert arrived.
        Assert.Contains(ours, r => r.RecipientUserId == s.Leaver.Id && r.Type == NotificationType.TeamNews);

        // Only the name changed inside each payload — every other key is byte-identical.
        foreach (var row in ours)
        {
            Assert.Equal(WithoutTeamName(before[row.Id].Payload), WithoutTeamName(row.Payload));
        }
    }

    [Fact]
    public async Task A_rename_is_silent()
    {
        var s = await ArrangeAsync();
        var before = await RowsAsync(s.Recipients);
        var total = await TotalNotificationsAsync();
        var emails = EmailsTo(s.Recipients);
        var pushes = PushesTo(s.Recipients);
        var realtime = RealtimeTo(s.Recipients);

        (await RenameAsync(s, NewName)).EnsureSuccessStatusCode();

        var after = await RowsAsync(s.Recipients);
        foreach (var (id, row) in after.Where(r => r.Value.TeamSlug == s.Slug))
        {
            var was = before[id];
            // Not unread again, not moved, not re-keyed (FR-009).
            Assert.Equal(was.IsRead, row.IsRead);
            Assert.Equal(was.CreatedDate, row.CreatedDate);
            Assert.Equal(was.DedupeKey, row.DedupeKey);
            Assert.Equal(was.Type, row.Type);
            // ...but written: the statement bypasses the audit interceptor (Gate 2).
            Assert.True(row.ModifiedDate > was.ModifiedDate);
        }

        Assert.Contains(after.Values, r => r.TeamSlug == s.Slug && r.IsRead);
        Assert.Equal(total, await TotalNotificationsAsync());
        Assert.Equal(emails, EmailsTo(s.Recipients));
        Assert.Equal(pushes, PushesTo(s.Recipients));
        Assert.Equal(realtime, RealtimeTo(s.Recipients));
    }

    [Fact]
    public async Task Another_team_with_the_same_name_keeps_its_alerts()
    {
        var s = await ArrangeAsync();
        var before = await RowsAsync(s.Recipients);

        (await RenameAsync(s, NewName)).EnsureSuccessStatusCode();

        var after = await RowsAsync(s.Recipients);
        var theirs = after.Values.Where(r => r.TeamSlug == s.OtherSlug).ToList();
        Assert.NotEmpty(theirs);
        Assert.All(theirs, r =>
        {
            Assert.Equal(OldName, (string?)r.Payload["teamName"]);
            Assert.Equal(before[r.Id].ModifiedDate, r.ModifiedDate);
        });
    }

    [Fact]
    public async Task Home_shows_the_new_name_on_a_role_change()
    {
        var s = await ArrangeAsync();

        (await RenameAsync(s, NewName)).EnsureSuccessStatusCode();

        var home = await s.Promoted.Client.GetFromJsonAsync<JsonElement>("/api/v1/home");
        var roleChanges = home.GetProperty("activity").EnumerateArray()
            .Where(a => a.GetProperty("kind").GetString() == "RoleChanged"
                && a.GetProperty("linkTarget").GetString() == s.Slug)
            .ToList();
        Assert.NotEmpty(roleChanges);
        Assert.All(roleChanges, a => Assert.Equal(NewName, a.GetProperty("params").GetProperty("teamName").GetString()));
    }

    [Fact]
    public async Task Saving_with_the_same_name_touches_no_alert()
    {
        var s = await ArrangeAsync();
        var before = await RowsAsync(s.Recipients);

        // Only the description changes: the name is resent unchanged (FR-011).
        (await RenameAsync(s, OldName, description: "Neu hier.")).EnsureSuccessStatusCode();

        var after = await RowsAsync(s.Recipients);
        Assert.All(after.Values, r => Assert.Equal(before[r.Id].ModifiedDate, r.ModifiedDate));
    }

    // --- arrange -------------------------------------------------------------------------------------

    private sealed record Player(HttpClient Client, Guid Id, string Email);

    private sealed record Setup(
        string Slug,
        string OtherSlug,
        Player Admin,
        Player Promoted,
        Player Leaver,
        Player Invitee,
        Player Requester)
    {
        public IReadOnlyList<Guid> Recipients => [Admin.Id, Promoted.Id, Leaver.Id, Invitee.Id, Requester.Id];
    }

    /// <summary>
    /// Team <see cref="OldName"/> with one delivered alert of every kind: a role change (Promoted), team
    /// news (Promoted, and Leaver — who then leaves), an invitation (Invitee), a join request (to both
    /// admins) and its answer (Requester), and seeded party/market alerts to the admin — one of them
    /// read. Plus a second team with the same display name, with an alert of its own.
    /// </summary>
    private async Task<Setup> ArrangeAsync()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        var promoted = await NewUserAsync();
        var leaver = await NewUserAsync();
        await JoinAsync(admin, slug, promoted);
        await JoinAsync(admin, slug, leaver);

        (await admin.Client.PatchAsJsonAsync($"/api/v1/teams/{slug}/members/{promoted.Id}/role", new { role = "Admin" }))
            .EnsureSuccessStatusCode();
        (await admin.Client.PostAsJsonAsync($"/api/v1/teams/{slug}/news", new { body = "Training moves to Thursday." }))
            .EnsureSuccessStatusCode();
        (await leaver.Client.DeleteAsync($"/api/v1/teams/{slug}/members/{leaver.Id}")).EnsureSuccessStatusCode();

        var invitee = await NewUserAsync();
        (await admin.Client.PostAsJsonAsync($"/api/v1/teams/{slug}/invitations", new { userId = invitee.Id }))
            .EnsureSuccessStatusCode();

        var requester = await NewUserAsync();
        (await requester.Client.PostAsync($"/api/v1/teams/{slug}/join-requests", null)).EnsureSuccessStatusCode();
        var queue = await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/join-requests");
        var requestId = queue.GetProperty("items")[0].GetProperty("id").GetString();
        (await admin.Client.PostAsync($"/api/v1/teams/{slug}/join-requests/{requestId}/decline", null))
            .EnsureSuccessStatusCode();

        var otherAdmin = await NewUserAsync();
        var otherSlug = await CreateTeamAsync(otherAdmin);

        var partyId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        await HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            db.Notifications.AddRange(
                // PartyService: the request, keyed per party.
                Seeded(admin.Id, NotificationType.PartyRequest, $"party-request:{partyId}",
                    new { partyId, eventId, teamSlug = slug, eventName = "Nordcup", teamName = OldName }, read: true),
                // PartyRosterService: the nudge — a fresh, NULL dedupe key.
                Seeded(admin.Id, NotificationType.PartyRequest, null,
                    new { partyId, EventId = eventId, TeamSlug = slug, EventName = "Nordcup", TeamName = OldName }),
                // PartyNewsService.
                Seeded(admin.Id, NotificationType.PartyNews, $"party-news:{Guid.CreateVersion7()}",
                    new { partyId, EventId = eventId, TeamSlug = slug, EventName = "Nordcup", TeamName = OldName }),
                // MarketRequestService.
                Seeded(admin.Id, NotificationType.MarketInvite, $"market-invite:{Guid.CreateVersion7()}",
                    new
                    {
                        requestId = Guid.CreateVersion7(), partyId, TeamName = OldName, TeamSlug = slug, eventId,
                        EventName = "Nordcup", positions = new[] { "Laeufer" },
                    }),
                // Another team that happens to share the display name.
                Seeded(admin.Id, NotificationType.TeamNews, $"news:{Guid.CreateVersion7()}:{admin.Id}",
                    new { teamSlug = otherSlug, teamName = OldName, newsPostId = Guid.CreateVersion7(), excerpt = "Hallo" }));
            await db.SaveChangesAsync();
        });

        return new Setup(slug, otherSlug, admin, promoted, leaver, invitee, requester);
    }

    private static Notification Seeded(Guid recipient, NotificationType type, string? dedupeKey, object payload, bool read = false) =>
        new()
        {
            RecipientUserId = recipient,
            Type = type,
            DedupeKey = dedupeKey,
            Payload = JsonSerializer.Serialize(payload, PayloadJson),
            IsRead = read,
            ReadDate = read ? DateTime.UtcNow : null,
        };

    // --- helpers -------------------------------------------------------------------------------------

    private sealed record Row(
        Guid Id, Guid RecipientUserId, NotificationType Type, JsonObject Payload, string? TeamSlug,
        bool IsRead, DateTime CreatedDate, DateTime ModifiedDate, string? DedupeKey);

    private Task<Dictionary<Guid, Row>> RowsAsync(IReadOnlyList<Guid> recipients) =>
        HomeTestSupport.WithDbAsync(_factory, async db =>
        {
            var rows = await db.Notifications.AsNoTracking()
                .Where(n => recipients.Contains(n.RecipientUserId))
                .Select(n => new { n.Id, n.RecipientUserId, n.Type, n.Payload, n.IsRead, n.CreatedDate, n.ModifiedDate, n.DedupeKey })
                .ToListAsync();
            return rows.ToDictionary(r => r.Id, r =>
            {
                var payload = JsonNode.Parse(r.Payload)!.AsObject();
                return new Row(r.Id, r.RecipientUserId, r.Type, payload, (string?)payload["teamSlug"],
                    r.IsRead, r.CreatedDate, r.ModifiedDate, r.DedupeKey);
            });
        });

    private static string WithoutTeamName(JsonObject payload)
    {
        var copy = payload.DeepClone().AsObject();
        copy.Remove("teamName");
        return copy.ToJsonString();
    }

    private Task<int> TotalNotificationsAsync() =>
        HomeTestSupport.WithDbAsync(_factory, db => db.Notifications.CountAsync());

    private int EmailsTo(IReadOnlyList<Guid> recipients)
    {
        var addresses = recipients.Select(id => _emails[id]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _factory.EmailSender.Sent.Count(e => addresses.Contains(e.To));
    }

    private readonly Dictionary<Guid, string> _emails = [];

    private int PushesTo(IReadOnlyList<Guid> recipients) =>
        _factory.PushDispatcher.Recipients.Count(recipients.Contains);

    private int RealtimeTo(IReadOnlyList<Guid> recipients) =>
        recipients.Sum(id => _factory.NotificationRealtime.CreatedFor(id).Count + _factory.NotificationRealtime.UnreadCountsFor(id).Count);

    private async Task<Player> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: AuthTestHelpers.NewHandle());
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        _emails[userId] = email;
        return new Player(client, userId, email);
    }

    private static async Task<string> CreateTeamAsync(Player admin)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        (await admin.Client.PostAsJsonAsync("/api/v1/teams", new
        {
            name = OldName,
            slug,
            type = "CityTeam",
            location = new { cityExternalId = "TEST:berlin" },
        })).EnsureSuccessStatusCode();
        return slug;
    }

    private static async Task JoinAsync(Player admin, string slug, Player joiner)
    {
        var link = await admin.Client.PostAsync($"/api/v1/teams/{slug}/invitations/link", null);
        link.EnsureSuccessStatusCode();
        var token = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        (await joiner.Client.PostAsync($"/api/v1/invitations/{token}/accept", null)).EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> RenameAsync(Setup s, string name, string? description = null) =>
        s.Admin.Client.PutAsJsonAsync($"/api/v1/teams/{s.Slug}/details",
            TeamDetailsTests.Details(name, description: description));
}
