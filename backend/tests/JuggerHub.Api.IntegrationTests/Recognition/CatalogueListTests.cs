using System.Net.Http.Json;
using System.Text.Json;

namespace JuggerHub.Api.IntegrationTests.Recognition;

/// <summary>
/// Feature 014 catalogue list: the admin list DTO carries a live <c>grantedCount</c> (active awards)
/// and a <c>createdAt</c>, and retired types drop from the default list but remain (with holders'
/// grants intact) when <c>includeRetired=true</c>. Runs for both catalogues.
/// </summary>
[Collection("Recognition")]
public sealed class CatalogueListTests
{
    private readonly JuggerHubApiFactory _factory;

    public CatalogueListTests(JuggerHubApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("badges")]
    [InlineData("achievements")]
    public async Task List_reports_grant_count_created_date_and_retired_filter(string resource)
    {
        var admin = await RecognitionTestSupport.AdminClientAsync(_factory);
        var (_, _, handle, _) = await RecognitionTestSupport.UserClientAsync(_factory);
        var id = await RecognitionTestSupport.CreateDefinitionAsync(admin, resource);

        // Freshly created → active, zero grants, with a recent created timestamp.
        var created = await FindAsync(admin, resource, id, includeRetired: false);
        Assert.NotNull(created);
        Assert.Equal(0, created!.Value.GetProperty("grantedCount").GetInt32());
        Assert.False(created.Value.GetProperty("isRetired").GetBoolean());
        Assert.True(created.Value.GetProperty("createdAt").GetDateTime() > DateTime.UtcNow.AddMinutes(-10));

        // Grant one → grantedCount reflects active awards.
        (await admin.PostAsJsonAsync($"/api/v1/admin/{resource}/{id}/awards", new { playerHandle = handle }))
            .EnsureSuccessStatusCode();
        var granted = await FindAsync(admin, resource, id, includeRetired: false);
        Assert.Equal(1, granted!.Value.GetProperty("grantedCount").GetInt32());

        // Retire → absent from the default (active) list; present (holder's grant kept) with includeRetired.
        (await admin.DeleteAsync($"/api/v1/admin/{resource}/{id}")).EnsureSuccessStatusCode();
        Assert.Null(await FindAsync(admin, resource, id, includeRetired: false));

        var retired = await FindAsync(admin, resource, id, includeRetired: true);
        Assert.NotNull(retired);
        Assert.True(retired!.Value.GetProperty("isRetired").GetBoolean());
        Assert.Equal(1, retired.Value.GetProperty("grantedCount").GetInt32());
    }

    /// <summary>Pages the admin catalogue to find a definition by id (the list has no by-id read).</summary>
    private static async Task<JsonElement?> FindAsync(HttpClient admin, string resource, Guid id, bool includeRetired)
    {
        var flag = includeRetired ? "true" : "false";
        for (var skip = 0; skip < 5000; skip += 100)
        {
            var resp = await admin.GetAsync($"/api/v1/admin/{resource}?skip={skip}&take=100&includeRetired={flag}");
            resp.EnsureSuccessStatusCode();
            var page = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var items = page.GetProperty("items");
            foreach (var item in items.EnumerateArray())
            {
                if (item.GetProperty("id").GetGuid() == id)
                {
                    return item.Clone();
                }
            }

            if (items.GetArrayLength() < 100)
            {
                break;
            }
        }

        return null;
    }
}
