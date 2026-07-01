using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JuggerHub.Api.IntegrationTests.Auth;
using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Onboarding;

/// <summary>
/// Onboarding feature (004): the complete-onboarding endpoint is owner-only and
/// idempotent, and the <c>onboardingCompleted</c> flag surfaces on <c>/auth/me</c>
/// and login. Reuses the shared Auth container + helpers.
/// </summary>
[Collection("Auth")]
public sealed class OnboardingCompleteTests
{
    private readonly JuggerHubApiFactory _factory;

    public OnboardingCompleteTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Complete_without_auth_is_401()
    {
        var anon = _factory.CreateClient();

        var response = await anon.PostAsync("/api/v1/profiles/me/onboarding/complete", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Fresh_account_reports_onboarding_not_completed()
    {
        var (client, _) = await RegisterVerifyLoginAsync();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");

        Assert.False(me.GetProperty("onboardingCompleted").GetBoolean());
    }

    [Fact]
    public async Task Complete_marks_owner_and_flag_shows_on_me_and_login()
    {
        var (client, email) = await RegisterVerifyLoginAsync();

        var complete = await client.PostAsync("/api/v1/profiles/me/onboarding/complete", content: null);
        Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);

        // /me reflects the flag on the existing session.
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.True(me.GetProperty("onboardingCompleted").GetBoolean());

        // A fresh login also reports it in the response body (drives the redirect).
        var relogin = await AuthTestHelpers.LoginAsync(_factory.CreateClient(), email, AuthTestHelpers.ValidPassword);
        relogin.EnsureSuccessStatusCode();
        var body = await relogin.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("onboardingCompleted").GetBoolean());
    }

    [Fact]
    public async Task Complete_is_idempotent_first_timestamp_stands()
    {
        var (client, email) = await RegisterVerifyLoginAsync();

        var first = await client.PostAsync("/api/v1/profiles/me/onboarding/complete", content: null);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        var firstStamp = await ReadCompletedAtAsync(email);
        Assert.NotNull(firstStamp);

        // A second call is a no-op that must not move the original timestamp.
        var second = await client.PostAsync("/api/v1/profiles/me/onboarding/complete", content: null);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var secondStamp = await ReadCompletedAtAsync(email);

        Assert.Equal(firstStamp, secondStamp);
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<(HttpClient Client, string Email)> RegisterVerifyLoginAsync()
    {
        var client = _factory.CreateClient();
        var (_, email) = await AuthTestHelpers.RegisterAndVerifyAsync(client, _factory);
        var login = await AuthTestHelpers.LoginAsync(client, email, AuthTestHelpers.ValidPassword);
        login.EnsureSuccessStatusCode();
        return (client, email);
    }

    private async Task<DateTime?> ReadCompletedAtAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PlayerProfiles
            .AsNoTracking()
            .Where(p => p.User.Email == email)
            .Select(p => p.OnboardingCompletedAt)
            .FirstOrDefaultAsync();
    }
}
