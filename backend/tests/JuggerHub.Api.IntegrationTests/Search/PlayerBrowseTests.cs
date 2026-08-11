using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// Player browse/search (007). After feature 020 the directory is unconditionally inclusive:
/// every non-banned player is returned, with no per-player opt-in. Covers that inclusivity across
/// query/filter/sort and auth, the position filter, case-insensitive name search, and pagination.
/// </summary>
[Collection("Search")]
public sealed class PlayerBrowseTests
{
    private readonly JuggerHubApiFactory _factory;

    public PlayerBrowseTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task All_players_are_returned_across_query_filter_sort_and_auth()
    {
        // Two players matching the same query — after removing the opt-in gate, BOTH appear.
        var nameA = "Zzalpha" + Guid.NewGuid().ToString("N")[..8];
        var nameB = "Zzbeta" + Guid.NewGuid().ToString("N")[..8];

        var (_, userAId, handleA, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, userAId,
            displayName: nameA, hometown: "Berlin", pompfen: Entities.Pompfe.Laeufer);

        var (_, userBId, handleB, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, userBId,
            displayName: nameB, hometown: "Berlin", pompfen: Entities.Pompfe.Laeufer);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var (authed, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        // Both players are reachable by name for anonymous and authenticated callers.
        foreach (var client in new[] { viewer, authed })
        {
            Assert.Contains(handleA, await HandlesAsync(client, $"/api/v1/profiles?q={nameA}"));
            Assert.Contains(handleB, await HandlesAsync(client, $"/api/v1/profiles?q={nameB}"));
        }

        // And both appear together under a shared filter/sort.
        var filtered = await HandlesAsync(viewer, "/api/v1/profiles?positions=Laeufer&city=Berlin&sort=DisplayNameAsc&take=100");
        Assert.Contains(handleA, filtered);
        Assert.Contains(handleB, filtered);
    }

    [Fact]
    public async Task Player_appears_without_any_opt_in()
    {
        // The new default: a freshly registered player is directory-visible with no action.
        var name = "Fresh" + Guid.NewGuid().ToString("N")[..8];
        var (_, userId, handle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, userId, displayName: name);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        Assert.Contains(handle, await HandlesAsync(viewer, $"/api/v1/profiles?q={name}"));
    }

    [Fact]
    public async Task Position_filter_matches_any_declared_pompfe()
    {
        var runnerName = "Runner" + Guid.NewGuid().ToString("N")[..8];
        var chainName = "Chainer" + Guid.NewGuid().ToString("N")[..8];

        var (_, runnerId, runnerHandle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, runnerId, displayName: runnerName,
            pompfen: Entities.Pompfe.Laeufer);

        var (_, chainId, chainHandle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, chainId, displayName: chainName,
            pompfen: Entities.Pompfe.Kette);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var runners = await HandlesAsync(viewer, "/api/v1/profiles?positions=Laeufer&take=100");

        Assert.Contains(runnerHandle, runners);
        Assert.DoesNotContain(chainHandle, runners);
    }

    [Fact]
    public async Task Name_search_is_case_insensitive()
    {
        var name = "Casing" + Guid.NewGuid().ToString("N")[..8];
        var (_, userId, handle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, userId, displayName: name);

        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var lower = await HandlesAsync(viewer, $"/api/v1/profiles?q={name.ToLowerInvariant()}");
        var upper = await HandlesAsync(viewer, $"/api/v1/profiles?q={name.ToUpperInvariant()}");

        Assert.Contains(handle, lower);
        Assert.Contains(handle, upper);
    }

    [Fact]
    public async Task Browse_is_anonymous_and_paginates()
    {
        var viewer = await SearchTestSupport.AuthedClientAsync(_factory);
        var resp = await viewer.GetAsync("/api/v1/profiles?take=5");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var page = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, page.GetProperty("take").GetInt32());
        Assert.True(page.GetProperty("items").GetArrayLength() <= 5);
    }

    // ---- Proximity sort (feature 030 pattern) -------------------------------------

    [Fact]
    public async Task Proximity_sort_orders_players_nearest_to_the_viewers_home_city_first()
    {
        // A shared display-name token isolates just these two players in the shared DB; proximity
        // then orders them. Berlin ↔ München is ~500 km, so the ordering is unambiguous.
        var token = "Prox" + Rnd();
        var (_, nearId, nearHandle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, nearId, displayName: $"{token} Berlin", hometown: "Berlin");

        var (_, farId, farHandle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, farId, displayName: $"{token} Munich", hometown: "München");

        var (viewer, viewerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.SetHomeCityAsync(_factory, viewerId, "Berlin");

        var handles = await HandlesAsync(viewer, $"/api/v1/profiles?q={Uri.EscapeDataString(token)}&sort=Proximity&take=100");

        var iNear = handles.IndexOf(nearHandle);
        var iFar = handles.IndexOf(farHandle);
        Assert.True(iNear >= 0 && iFar >= 0, "both players are present in the proximity view");
        Assert.True(iNear < iFar, "the home-city (Berlin) player ranks ahead of the Munich player");
    }

    [Fact]
    public async Task Proximity_sort_excludes_players_without_a_home_city_and_reports_the_matching_total()
    {
        // The distance join has no row for a city-less player, so they drop out of the view. The
        // total must be recomputed with the join's own predicate — reporting the pre-join count
        // would promise a page that cannot be paged to.
        var token = "Prox" + Rnd();
        var (_, locatedId, locatedHandle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, locatedId, displayName: $"{token} Located", hometown: "Berlin");

        var (_, homelessId, homelessHandle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, homelessId, displayName: $"{token} Homeless");

        var (viewer, viewerId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.SetHomeCityAsync(_factory, viewerId, "Berlin");

        var url = $"/api/v1/profiles?q={Uri.EscapeDataString(token)}&sort=Proximity&take=100";
        var page = await viewer.GetFromJsonAsync<JsonElement>(url);
        var handles = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("handle").GetString()!).ToList();

        Assert.Contains(locatedHandle, handles);
        Assert.DoesNotContain(homelessHandle, handles);
        Assert.Equal(handles.Count, page.GetProperty("totalCount").GetInt32());

        // Default sort still returns both — proximity narrows the view, it does not hide anyone.
        var byName = await HandlesAsync(viewer, $"/api/v1/profiles?q={Uri.EscapeDataString(token)}&take=100");
        Assert.Contains(homelessHandle, byName);
    }

    [Fact]
    public async Task Proximity_sort_without_a_home_city_is_rejected()
    {
        var (viewer, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        var resp = await viewer.GetAsync("/api/v1/profiles?sort=Proximity&take=10");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    private static string Rnd() => Guid.NewGuid().ToString("N")[..6];

    private static async Task<List<string>> HandlesAsync(HttpClient client, string url)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(url);
        return page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("handle").GetString()!)
            .ToList();
    }
}
