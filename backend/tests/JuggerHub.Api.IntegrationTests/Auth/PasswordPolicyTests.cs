using System.Net.Http.Json;
using JuggerHub.Dtos.Auth;

namespace JuggerHub.Api.IntegrationTests.Auth;

/// <summary>US5 — the published password policy reflects the configured Identity options.</summary>
[Collection("Auth")]
public sealed class PasswordPolicyTests
{
    private readonly JuggerHubApiFactory _factory;

    public PasswordPolicyTests(JuggerHubApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Password_policy_returns_identity_options()
    {
        var client = _factory.CreateClient();

        var policy = await client.GetFromJsonAsync<PasswordPolicyDto>("/api/v1/auth/password-policy");

        Assert.NotNull(policy);
        Assert.Equal(8, policy!.MinLength);
        Assert.True(policy.RequireDigit);
        Assert.True(policy.RequireUppercase);
        Assert.True(policy.RequireLowercase);
        Assert.True(policy.RequireNonAlphanumeric);
        Assert.Equal(3, policy.RequiredUniqueChars);
    }
}
