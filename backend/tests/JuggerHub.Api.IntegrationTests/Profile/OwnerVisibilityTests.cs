using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Profile;

/// <summary>
/// Feature 026 (US2) — the owner controls their own visibility: <c>PUT /me</c> persists the flag,
/// <c>GET /me</c> reflects it, and toggling it flips anonymous access to the profile (FR-008/FR-015/SC-005).
/// </summary>
[Collection("Profile")]
public sealed class OwnerVisibilityTests
{
    private readonly JuggerHubApiFactory _factory;

    public OwnerVisibilityTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Owner_toggles_visibility_and_anonymous_access_follows()
    {
        var (client, handle, _) = await RegisterLoginAsync();
        var anon = _factory.CreateClient();

        // Default private → not visible to anonymous, isPublic=false on /me.
        Assert.False(await IsPublicAsync(client));
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);

        // Turn public → /me reflects it and the anonymous link resolves.
        await SetVisibilityAsync(client, isPublic: true);
        Assert.True(await IsPublicAsync(client));
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);

        // Turn private again → anonymous access stops on the next request.
        await SetVisibilityAsync(client, isPublic: false);
        Assert.False(await IsPublicAsync(client));
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/profiles/{handle}")).StatusCode);
    }

    private async Task<(HttpClient Client, string Handle, string Email)> RegisterLoginAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        (await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword)).EnsureSuccessStatusCode();
        return (client, handle, email);
    }

    private static async Task<bool> IsPublicAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/v1/profiles/me")).GetProperty("isPublic").GetBoolean();

    private static async Task SetVisibilityAsync(HttpClient client, bool isPublic)
    {
        var resp = await client.PutAsJsonAsync("/api/v1/profiles/me", new
        {
            displayName = "Toggle Player",
            hometown = (string?)null,
            description = (string?)null,
            pompfen = Array.Empty<string>(),
            isPublic,
        });
        resp.EnsureSuccessStatusCode();
    }
}
