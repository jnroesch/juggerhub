using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// Team browse/search (007, US1): active derivation (created OR participated in an event within
/// the last 12 months), the beginners-welcome + city filters, accent/case-insensitive name+city search,
/// anonymous access + pagination, and the admin-only beginners-welcome settings write.
/// </summary>
[Collection("Search")]
public sealed class TeamBrowseTests
{
    private readonly JuggerHubApiFactory _factory;

    public TeamBrowseTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Active_only_hides_teams_without_recent_participation()
    {
        var active = await SearchTestSupport.SeedTeamAsync(_factory, "Active FC " + Rnd(), "Bremen");
        var dormant = await SearchTestSupport.SeedTeamAsync(_factory, "Dormant FC " + Rnd(), "Bremen");
        // Both are seeded "now"; backdate the dormant team past the window so only participation
        // (or recent creation) makes a team active.
        await SearchTestSupport.BackdateTeamCreatedAsync(_factory, active.Id, DateTime.UtcNow.AddMonths(-18));
        await SearchTestSupport.BackdateTeamCreatedAsync(_factory, dormant.Id, DateTime.UtcNow.AddMonths(-18));

        // Give the active team a participation in a recent event.
        var (_, userId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var profileId = await SearchTestSupport.ProfileIdAsync(_factory, userId);
        var recent = DateTime.UtcNow.AddMonths(-2);
        var eventId = await SearchTestSupport.SeedEventAsync(_factory, "Recent Cup " + Rnd(), recent, recent.AddHours(6));
        await SearchTestSupport.AddParticipationAsync(_factory, profileId, eventId, active.Id, "Active FC");

        var anon = _factory.CreateClient();
        var activeOnly = await SlugsAsync(anon, "/api/v1/teams?activeOnly=true&city=Bremen&take=100");
        var all = await SlugsAsync(anon, "/api/v1/teams?activeOnly=false&city=Bremen&take=100");

        Assert.Contains(active.Slug, activeOnly);
        Assert.DoesNotContain(dormant.Slug, activeOnly);
        Assert.Contains(dormant.Slug, all);
    }

    [Fact]
    public async Task Old_participation_does_not_make_a_team_active()
    {
        var team = await SearchTestSupport.SeedTeamAsync(_factory, "Stale FC " + Rnd(), "Ulm");
        // Backdate creation past the window so only the (old) participation is in play.
        await SearchTestSupport.BackdateTeamCreatedAsync(_factory, team.Id, DateTime.UtcNow.AddMonths(-18));
        var (_, userId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var profileId = await SearchTestSupport.ProfileIdAsync(_factory, userId);
        var old = DateTime.UtcNow.AddMonths(-18); // outside the 12-month window
        var eventId = await SearchTestSupport.SeedEventAsync(_factory, "Old Cup " + Rnd(), old, old.AddHours(6));
        await SearchTestSupport.AddParticipationAsync(_factory, profileId, eventId, team.Id, "Stale FC");

        var anon = _factory.CreateClient();
        var activeOnly = await SlugsAsync(anon, "/api/v1/teams?activeOnly=true&city=Ulm&take=100");
        Assert.DoesNotContain(team.Slug, activeOnly);
    }

    [Fact]
    public async Task Recently_created_team_is_active_without_any_participation()
    {
        // A brand-new team (created now, no events yet) counts as active (feature 008).
        var fresh = await SearchTestSupport.SeedTeamAsync(_factory, "Fresh FC " + Rnd(), "Trier");

        var anon = _factory.CreateClient();
        var activeOnly = await SlugsAsync(anon, "/api/v1/teams?activeOnly=true&city=Trier&take=100");
        Assert.Contains(fresh.Slug, activeOnly);
    }

    [Fact]
    public async Task Beginners_filter_returns_only_beginners_welcome_teams()
    {
        var city = "Kassel";
        var welcoming = await SearchTestSupport.SeedTeamAsync(_factory, "Newbies FC " + Rnd(), city, beginnersWelcome: true);
        var closed = await SearchTestSupport.SeedTeamAsync(_factory, "Vets FC " + Rnd(), city, beginnersWelcome: false);

        var anon = _factory.CreateClient();
        var beginners = await SlugsAsync(anon, $"/api/v1/teams?activeOnly=false&beginnersWelcome=true&city={city}&take=100");

        Assert.Contains(welcoming.Slug, beginners);
        Assert.DoesNotContain(closed.Slug, beginners);
    }

    [Fact]
    public async Task Search_is_accent_and_case_insensitive_over_name_and_city()
    {
        var team = await SearchTestSupport.SeedTeamAsync(_factory, "Kölner Kettenhunde " + Rnd(), "Köln");
        var anon = _factory.CreateClient();

        foreach (var q in new[] { "koln", "KÖLN", "kölner", "KOLNER" })
        {
            var slugs = await SlugsAsync(anon, $"/api/v1/teams?activeOnly=false&q={Uri.EscapeDataString(q)}&take=100");
            Assert.Contains(team.Slug, slugs);
        }
    }

    [Fact]
    public async Task Browse_is_anonymous_and_paginates()
    {
        for (var i = 0; i < 3; i++)
        {
            await SearchTestSupport.SeedTeamAsync(_factory, $"Page FC {Rnd()}", "Aachen");
        }

        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/v1/teams?activeOnly=false&city=Aachen&take=2");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var page = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, page.GetProperty("take").GetInt32());
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 3);
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());
    }

    // --- Beginners-welcome settings write (PATCH /teams/{slug}) ----------------

    [Fact]
    public async Task Admin_can_set_beginners_welcome_and_it_shows_in_browse()
    {
        var (admin, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        var create = await admin.PostAsJsonAsync("/api/v1/teams",
            new { name = "Setters FC", slug, type = "CityTeam", city = "Trier" });
        create.EnsureSuccessStatusCode();

        var patch = await admin.PatchAsJsonAsync($"/api/v1/teams/{slug}", new { beginnersWelcome = true });
        Assert.Equal(HttpStatusCode.NoContent, patch.StatusCode);

        var anon = _factory.CreateClient();
        var beginners = await SlugsAsync(anon, "/api/v1/teams?activeOnly=false&beginnersWelcome=true&city=Trier&take=100");
        Assert.Contains(slug, beginners);
    }

    [Fact]
    public async Task Non_admin_cannot_set_beginners_welcome_and_unknown_team_is_404()
    {
        var (admin, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        (await admin.PostAsJsonAsync("/api/v1/teams",
            new { name = "Guarded FC", slug, type = "Mixteam", city = (string?)null })).EnsureSuccessStatusCode();

        var (outsider, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        // Non-member → 404 (no membership oracle), matching the rest of the team surface.
        var byOutsider = await outsider.PatchAsJsonAsync($"/api/v1/teams/{slug}", new { beginnersWelcome = true });
        Assert.Equal(HttpStatusCode.NotFound, byOutsider.StatusCode);

        var unknown = await admin.PatchAsJsonAsync($"/api/v1/teams/t{Guid.NewGuid():N}", new { beginnersWelcome = true });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    private static string Rnd() => Guid.NewGuid().ToString("N")[..6];

    private static async Task<List<string>> SlugsAsync(HttpClient client, string url)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(url);
        return page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("slug").GetString()!)
            .ToList();
    }
}
