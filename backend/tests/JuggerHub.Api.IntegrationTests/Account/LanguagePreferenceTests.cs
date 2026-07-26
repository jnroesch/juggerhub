using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;

namespace JuggerHub.Api.IntegrationTests.Account;

/// <summary>
/// Language preference (feature 031): the signed-in user can persist a supported language via
/// <c>PUT /account/language</c>, it round-trips on <c>/auth/me</c>, unsupported values are rejected
/// (never trust the client), and anonymous callers can't set it. Reuses the "Teams" fixture.
/// </summary>
[Collection("Teams")]
public sealed class LanguagePreferenceTests
{
    private readonly JuggerHubApiFactory _factory;

    public LanguagePreferenceTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Setting_a_supported_language_persists_and_round_trips_on_me()
    {
        var user = await NewUserAsync();

        // Default: no preference stored yet.
        var before = await user.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.True(before.GetProperty("preferredLanguage").ValueKind is JsonValueKind.Null);

        var put = await user.PutAsJsonAsync("/api/v1/account/language", new { language = "de" });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var after = await user.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal("de", after.GetProperty("preferredLanguage").GetString());
    }

    [Fact]
    public async Task Unsupported_language_is_rejected()
    {
        var user = await NewUserAsync();
        var resp = await user.PutAsJsonAsync("/api/v1/account/language", new { language = "fr" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Anonymous_cannot_set_language()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.PutAsJsonAsync("/api/v1/account/language", new { language = "de" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private async Task<HttpClient> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var handle = AuthTestHelpers.NewHandle();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory, handle: handle);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return client;
    }
}
