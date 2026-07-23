using System.Net;
using System.Net.Http.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Profile;

/// <summary>
/// Feature 026 (US2) — the profile visibility gate: a public profile is anonymously viewable,
/// a private one is indistinguishable from a missing handle (no oracle), an authenticated
/// caller sees any profile, and a banned owner is never anonymously visible regardless of the flag.
/// </summary>
[Collection("Profile")]
public sealed class VisibilityTests
{
    private readonly JuggerHubApiFactory _factory;

    public VisibilityTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_profile_is_viewable_anonymously()
    {
        var (client, handle, _) = await RegisterLoginAsync();
        await SetVisibilityAsync(client, isPublic: true);

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/profiles/{handle}/activity")).StatusCode);
    }

    [Fact]
    public async Task Private_profile_is_404_to_anonymous_callers()
    {
        var (_, handle, _) = await RegisterLoginAsync(); // private by default

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/profiles/{handle}/activity")).StatusCode);
    }

    [Fact]
    public async Task Authenticated_caller_sees_any_profile_regardless_of_visibility()
    {
        var (_, privateHandle, _) = await RegisterLoginAsync(); // stays private
        var viewer = (await RegisterLoginAsync()).Client;

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/v1/profiles/{privateHandle}")).StatusCode);
    }

    [Fact]
    public async Task Private_profile_and_missing_handle_are_indistinguishable_no_oracle()
    {
        var (_, privateHandle, _) = await RegisterLoginAsync(); // private by default
        var anon = _factory.CreateClient();

        var privateResp = await anon.GetAsync($"/api/v1/profiles/{privateHandle}");
        var missingResp = await anon.GetAsync($"/api/v1/profiles/{AuthTestHelpers.NewHandle()}");

        Assert.Equal(missingResp.StatusCode, privateResp.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, privateResp.StatusCode);
        // Same ProblemDetails (type/title/status/detail) — nothing distinguishes "private" from
        // "missing". (traceId is a random per-request correlation id, not an information leak.)
        Assert.Equal(await ProblemShapeAsync(missingResp), await ProblemShapeAsync(privateResp));
    }

    [Fact]
    public async Task Banned_owner_is_never_anonymously_visible_even_when_public()
    {
        var (client, handle, _) = await RegisterLoginAsync();
        await SetVisibilityAsync(client, isPublic: true);
        await BanAsync(handle);

        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);

        // Even an authenticated viewer cannot see a banned owner (global filter wins over the flag).
        var viewer = (await RegisterLoginAsync()).Client;
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);
    }

    // --- Helpers --------------------------------------------------------------

    /// <summary>The identifying fields of a ProblemDetails response, minus the random traceId.</summary>
    private static async Task<string> ProblemShapeAsync(HttpResponseMessage resp)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        static string? Get(System.Text.Json.JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) ? v.ToString() : null;
        return $"{Get(root, "type")}|{Get(root, "title")}|{Get(root, "status")}|{Get(root, "detail")}";
    }

    private async Task<(HttpClient Client, string Handle, string Email)> RegisterLoginAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, handle, email);
    }

    private static async Task SetVisibilityAsync(HttpClient client, bool isPublic)
    {
        var resp = await client.PutAsJsonAsync("/api/v1/profiles/me", new
        {
            displayName = "Visible Player",
            hometown = (string?)null,
            description = (string?)null,
            pompfen = Array.Empty<string>(),
            isPublic,
        });
        resp.EnsureSuccessStatusCode();
    }

    private async Task BanAsync(string handle)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Users
            .Where(u => u.Profile!.Handle == handle)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Status, AccountStatus.Banned)
                .SetProperty(u => u.StatusChangedAt, DateTime.UtcNow));
    }
}
