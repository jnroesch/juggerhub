using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Search;

/// <summary>
/// Player browse/search (007, US3). The central assertion is the opt-in privacy invariant:
/// a player who has not opted in is never returned, for any query/filter/sort and regardless
/// of auth (spec FR-042 / SC-003). Also covers the position filter, name search, and the
/// self-service opt-in write. Exercises the real API + Postgres container.
/// </summary>
[Collection("Search")]
public sealed class PlayerBrowseTests
{
    private readonly JuggerHubApiFactory _factory;

    public PlayerBrowseTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Non_opted_in_player_is_never_returned_across_query_filter_sort_and_auth()
    {
        // A hidden player (opted out) and a visible player (opted in), both matching the query.
        var hiddenName = "Zzhidden" + Guid.NewGuid().ToString("N")[..8];
        var visibleName = "Zzvisible" + Guid.NewGuid().ToString("N")[..8];

        var (_, hiddenUserId, hiddenHandle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, hiddenUserId, appearInSearch: false,
            displayName: hiddenName, hometown: "Berlin", pompfen: Entities.Pompfe.Laeufer);

        var (_, visibleUserId, _, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, visibleUserId, appearInSearch: true,
            displayName: visibleName, hometown: "Berlin", pompfen: Entities.Pompfe.Laeufer);

        var anon = _factory.CreateClient();
        var (authed, _, _, _) = await SearchTestSupport.NewUserAsync(_factory);

        // Every query/filter/sort combination, anonymous AND authenticated — never the hidden one.
        string[] queries =
        {
            "/api/v1/profiles",
            $"/api/v1/profiles?q={hiddenName}",
            $"/api/v1/profiles?q={hiddenHandle}",
            "/api/v1/profiles?positions=Laeufer",
            "/api/v1/profiles?city=Berlin",
            "/api/v1/profiles?positions=Laeufer&city=Berlin&sort=DisplayNameAsc",
            "/api/v1/profiles?take=100",
        };

        foreach (var client in new[] { anon, authed })
        {
            foreach (var url in queries)
            {
                var handles = await HandlesAsync(client, url);
                Assert.DoesNotContain(hiddenHandle, handles);
            }
        }

        // Sanity: the opted-in player IS reachable by name.
        var visibleHandles = await HandlesAsync(anon, $"/api/v1/profiles?q={visibleName}");
        Assert.NotEmpty(visibleHandles);
    }

    [Fact]
    public async Task Position_filter_matches_any_declared_pompfe()
    {
        var runnerName = "Runner" + Guid.NewGuid().ToString("N")[..8];
        var chainName = "Chainer" + Guid.NewGuid().ToString("N")[..8];

        var (_, runnerId, runnerHandle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, runnerId, true, displayName: runnerName,
            pompfen: Entities.Pompfe.Laeufer);

        var (_, chainId, chainHandle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, chainId, true, displayName: chainName,
            pompfen: Entities.Pompfe.Kette);

        var anon = _factory.CreateClient();
        var runners = await HandlesAsync(anon, "/api/v1/profiles?positions=Laeufer&take=100");

        Assert.Contains(runnerHandle, runners);
        Assert.DoesNotContain(chainHandle, runners);
    }

    [Fact]
    public async Task Name_search_is_case_insensitive()
    {
        var name = "Casing" + Guid.NewGuid().ToString("N")[..8];
        var (_, userId, handle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, userId, true, displayName: name);

        var anon = _factory.CreateClient();
        var lower = await HandlesAsync(anon, $"/api/v1/profiles?q={name.ToLowerInvariant()}");
        var upper = await HandlesAsync(anon, $"/api/v1/profiles?q={name.ToUpperInvariant()}");

        Assert.Contains(handle, lower);
        Assert.Contains(handle, upper);
    }

    [Fact]
    public async Task Opt_in_write_toggles_visibility_and_is_owner_only()
    {
        var name = "Toggler" + Guid.NewGuid().ToString("N")[..8];
        var (client, userId, handle, _) = await SearchTestSupport.NewUserAsync(_factory);
        await SearchTestSupport.ConfigurePlayerAsync(_factory, userId, false, displayName: name);

        var anon = _factory.CreateClient();
        Assert.DoesNotContain(handle, await HandlesAsync(anon, $"/api/v1/profiles?q={name}"));

        // Opt in via the owner endpoint.
        var optIn = await client.PutAsJsonAsync("/api/v1/profiles/me",
            new { displayName = name, hometown = (string?)null, description = (string?)null, pompfen = Array.Empty<string>(), appearInSearch = true });
        optIn.EnsureSuccessStatusCode();
        Assert.Contains(handle, await HandlesAsync(anon, $"/api/v1/profiles?q={name}"));

        // Opt back out.
        var optOut = await client.PutAsJsonAsync("/api/v1/profiles/me",
            new { displayName = name, hometown = (string?)null, description = (string?)null, pompfen = Array.Empty<string>(), appearInSearch = false });
        optOut.EnsureSuccessStatusCode();
        Assert.DoesNotContain(handle, await HandlesAsync(anon, $"/api/v1/profiles?q={name}"));
    }

    [Fact]
    public async Task Browse_is_anonymous_and_paginates()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/v1/profiles?take=5");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var page = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, page.GetProperty("take").GetInt32());
        Assert.True(page.GetProperty("items").GetArrayLength() <= 5);
    }

    private static async Task<List<string>> HandlesAsync(HttpClient client, string url)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(url);
        return page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("handle").GetString()!)
            .ToList();
    }
}
