using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Api.IntegrationTests.Home;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>
/// Feature 061: a team admin changes the team's name, type and city after creation, and gives the
/// team a description and links — all through <c>PUT /teams/{slug}/details</c>, a whole replacement
/// checked against create's rules. Real API + Postgres container. What a rename does to delivered
/// alerts and to tournament results is in <see cref="TeamRenameRewriteTests"/> and
/// <c>TeamRenamePlacementTests</c>.
/// </summary>
[Collection("Teams")]
public sealed class TeamDetailsTests
{
    private readonly JuggerHubApiFactory _factory;

    public TeamDetailsTests(JuggerHubApiFactory factory) => _factory = factory;

    // --- US1: name, type, city -----------------------------------------------------------------------

    [Fact]
    public async Task An_admin_renames_the_team_and_its_address_stays()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        var name = "Rheinfeuer " + Unique();

        var resp = await SaveAsync(admin, slug, Details(name));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal(slug, body.GetProperty("slug").GetString());
        Assert.Equal("Admin", body.GetProperty("myRole").GetString());

        Assert.Equal(name, (await DetailAsync(admin, slug)).GetProperty("name").GetString());
        var outsider = await NewUserAsync();
        Assert.Equal(name, (await PublicAsync(outsider, slug)).GetProperty("name").GetString());
        // Browse reads the team row live: the new name finds it.
        Assert.Contains(slug, await BrowseSlugsAsync(outsider, $"q={Uri.EscapeDataString(name)}&activeOnly=false"));
    }

    [Theory]
    [InlineData("X")]
    [InlineData("   ")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public async Task A_name_outside_2_to_50_characters_is_refused_and_nothing_changes(string name)
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        var resp = await SaveAsync(admin, slug, Details(name));

        await AssertRefusedAsync(resp, "nameInvalid");
        Assert.Equal("Rheinfeuer", (await DetailAsync(admin, slug)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_name_is_trimmed()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        var resp = await SaveAsync(admin, slug, Details("  Rheinfeuer Köln  "));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Rheinfeuer Köln", (await DetailAsync(admin, slug)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_city_team_moves_to_another_city_and_browse_finds_it_there()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        var name = "Umzug " + Unique();
        await SaveAsync(admin, slug, Details(name));

        var resp = await SaveAsync(admin, slug, Details(name, city: "TEST:hamburg"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var location = (await DetailAsync(admin, slug)).GetProperty("location");
        Assert.Equal("TEST:hamburg", location.GetProperty("externalId").GetString());
        var q = $"q={Uri.EscapeDataString(name)}&activeOnly=false";
        Assert.Contains(slug, await BrowseSlugsAsync(admin, q + "&city=Hamburg"));
        Assert.DoesNotContain(slug, await BrowseSlugsAsync(admin, q + "&city=Berlin"));
    }

    [Fact]
    public async Task Resending_the_current_city_keeps_it()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        var before = await CityIdAsync(slug);

        var resp = await SaveAsync(admin, slug, Details("Rheinfeuer", description: "Only the description changed."));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotNull(before);
        Assert.Equal(before, await CityIdAsync(slug));
    }

    [Fact]
    public async Task A_city_team_that_becomes_a_mixteam_loses_its_city()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        var resp = await SaveAsync(admin, slug, Details("Rheinfeuer", type: "Mixteam", city: null));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var detail = await DetailAsync(admin, slug);
        Assert.Equal("Mixteam", detail.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("location").ValueKind);
        Assert.Null(await CityIdAsync(slug));
    }

    [Fact]
    public async Task A_mixteam_needs_a_city_to_become_a_city_team_and_cannot_carry_one()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        await SaveAsync(admin, slug, Details("Rheinfeuer", type: "Mixteam", city: null));

        await AssertRefusedAsync(await SaveAsync(admin, slug, Details("Rheinfeuer", type: "CityTeam", city: null)), "cityRequired");
        await AssertRefusedAsync(await SaveAsync(admin, slug, Details("Rheinfeuer", type: "Mixteam", city: "TEST:berlin")), "mixteamHasCity");

        var detail = await DetailAsync(admin, slug);
        Assert.Equal("Mixteam", detail.GetProperty("type").GetString());
        Assert.Null(await CityIdAsync(slug));

        // With a city, it becomes a City team again.
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(admin, slug, Details("Rheinfeuer", type: "CityTeam", city: "TEST:köln"))).StatusCode);
        Assert.Equal("CityTeam", (await DetailAsync(admin, slug)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task An_unknown_city_is_refused()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        await AssertRefusedAsync(await SaveAsync(admin, slug, Details("Rheinfeuer", city: "TEST:atlantis")), "cityNotFound");
    }

    [Fact]
    public async Task Only_an_admin_may_change_the_details_and_outsiders_learn_nothing()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        var member = await NewUserAsync();
        await JoinAsync(admin, slug, member);
        var outsider = await NewUserAsync();

        var asMember = await SaveAsync(member, slug, Details("Taken over"));
        var asOutsider = await SaveAsync(outsider, slug, Details("Taken over"));
        var noSuchTeam = await SaveAsync(outsider, "doesnotexist" + Unique(), Details("Taken over"));
        var anonymous = await _factory.CreateClient().PutAsJsonAsync($"/api/v1/teams/{slug}/details", Details("Taken over"));

        Assert.Equal(HttpStatusCode.Forbidden, asMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asOutsider.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noSuchTeam.StatusCode);
        // No membership oracle: a real team and a made-up one answer an outsider identically.
        Assert.Equal(await ProblemAsync(noSuchTeam), await ProblemAsync(asOutsider));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("Rheinfeuer", (await DetailAsync(admin, slug)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_details_and_the_recruitment_flag_and_logo_do_not_touch_each_other()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        (await admin.Client.PatchAsJsonAsync($"/api/v1/teams/{slug}", new { beginnersWelcome = true })).EnsureSuccessStatusCode();
        (await UploadLogoAsync(admin, slug)).EnsureSuccessStatusCode();

        (await SaveAsync(admin, slug, Details("Rheinfeuer Neu", description: "Wir.",
            links: [new { label = "Website", url = "https://rheinfeuer.de" }]))).EnsureSuccessStatusCode();

        var afterDetails = await DetailAsync(admin, slug);
        Assert.True(afterDetails.GetProperty("beginnersWelcome").GetBoolean());
        Assert.True(afterDetails.GetProperty("hasLogo").GetBoolean());

        (await admin.Client.PatchAsJsonAsync($"/api/v1/teams/{slug}", new { beginnersWelcome = false })).EnsureSuccessStatusCode();

        var afterFlag = await DetailAsync(admin, slug);
        Assert.Equal("Rheinfeuer Neu", afterFlag.GetProperty("name").GetString());
        Assert.Equal("Wir.", afterFlag.GetProperty("description").GetString());
        Assert.Single(afterFlag.GetProperty("links").EnumerateArray());
    }

    [Fact]
    public async Task A_refused_save_changes_nothing_at_all()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);

        var resp = await SaveAsync(admin, slug, Details("A perfectly good new name", description: "Also fine.",
            links: [new { label = "Blog", url = "http://insecure.example" }]));

        await AssertRefusedAsync(resp, "linkUrlInvalid", link: 0);
        var detail = await DetailAsync(admin, slug);
        Assert.Equal("Rheinfeuer", detail.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task A_save_moves_the_teams_modified_date()
    {
        var admin = await NewUserAsync();
        var slug = await CreateTeamAsync(admin);
        var before = await TeamModifiedDateAsync(slug);

        (await SaveAsync(admin, slug, Details("Rheinfeuer Neu"))).EnsureSuccessStatusCode();

        // The team row is written with ExecuteUpdate, which skips the audit interceptor (Gate 2).
        Assert.True(await TeamModifiedDateAsync(slug) > before);
    }

    // --- helpers -------------------------------------------------------------------------------------

    private sealed record Player(HttpClient Client, Guid Id, string Handle, string Email);

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private async Task<Player> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (userId, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return new Player(client, userId, handle, email);
    }

    private static async Task<string> CreateTeamAsync(Player admin)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..12];
        (await admin.Client.PostAsJsonAsync("/api/v1/teams", new
        {
            name = "Rheinfeuer",
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

    /// <summary>A whole details body. <paramref name="city"/> null sends <c>location: null</c>.</summary>
    internal static object Details(
        string name,
        string type = "CityTeam",
        string? city = "TEST:berlin",
        string? description = null,
        object[]? links = null) => new
        {
            name,
            type,
            location = city is null ? null : new { cityExternalId = city, name = (string?)null },
            description,
            links = links ?? [],
        };

    private static Task<HttpResponseMessage> SaveAsync(Player actor, string slug, object body) =>
        actor.Client.PutAsJsonAsync($"/api/v1/teams/{slug}/details", body);

    private static Task<JsonElement> DetailAsync(Player reader, string slug) =>
        reader.Client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}");

    private static Task<JsonElement> PublicAsync(Player reader, string slug) =>
        reader.Client.GetFromJsonAsync<JsonElement>($"/api/v1/teams/{slug}/public");

    private static async Task<List<string?>> BrowseSlugsAsync(Player reader, string query)
    {
        var page = await reader.Client.GetFromJsonAsync<JsonElement>($"/api/v1/teams?{query}&take=50");
        return page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("slug").GetString()).ToList();
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage resp, string code, int? link = null)
    {
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        if (link is { } index)
        {
            Assert.Equal(index, problem.GetProperty("link").GetInt32());
        }
        else
        {
            Assert.False(problem.TryGetProperty("link", out _));
        }
    }

    private static async Task<(string? Title, string? Detail)> ProblemAsync(HttpResponseMessage resp)
    {
        var problem = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (problem.GetProperty("title").GetString(), problem.GetProperty("detail").GetString());
    }

    private static Task<HttpResponseMessage> UploadLogoAsync(Player admin, string slug)
    {
        using var img = new Image<Rgba32>(8, 8, new Rgba32(10, 120, 200));
        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        var content = new MultipartFormDataContent { { new ByteArrayContent(ms.ToArray()), "file", "logo.png" } };
        return admin.Client.PutAsync($"/api/v1/teams/{slug}/logo", content);
    }

    private Task<Guid?> CityIdAsync(string slug) =>
        HomeTestSupport.WithDbAsync(_factory, db =>
            db.Teams.AsNoTracking().Where(t => t.Slug == slug).Select(t => t.CityId).SingleAsync());

    private Task<DateTime> TeamModifiedDateAsync(string slug) =>
        HomeTestSupport.WithDbAsync(_factory, db =>
            db.Teams.AsNoTracking().Where(t => t.Slug == slug).Select(t => t.ModifiedDate).SingleAsync());
}
