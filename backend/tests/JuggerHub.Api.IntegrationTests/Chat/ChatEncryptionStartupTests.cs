using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// SC-005 — the application refuses to start without a usable chat encryption key (feature 047).
/// </summary>
/// <remarks>
/// This is the assertion behind "there is no switch that turns encryption off". Everything else in
/// this feature protects message text; this protects the protection, by making a configuration
/// mistake stop the process rather than quietly write plaintext.
/// <para>
/// No database is needed: the check runs immediately after the host is built and before the startup
/// migrations, so a misconfigured deployment is refused before it alters anyone's schema.
/// </para>
/// </remarks>
public sealed class ChatEncryptionStartupTests
{
    private sealed class Host(string? keys) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Never reached — startup fails first — but present so a failure can only be
                    // about the encryption key.
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
                    ["Jwt:Issuer"] = "juggerhub-tests",
                    ["Jwt:Audience"] = "juggerhub-tests",
                    ["Jwt:SigningKey"] = "integration-tests-signing-key-at-least-32-bytes-long!!",
                    ["Seeding:CityReferences"] = "false",
                    ["Chat:Encryption:Keys"] = keys,
                }));
        }
    }

    [Theory]
    [InlineData(null)]                                  // not configured at all
    [InlineData("")]                                    // configured empty
    [InlineData("1:tooshort")]                          // not 32 bytes
    [InlineData("0:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=")] // invalid version
    [InlineData("nonsense")]                            // malformed
    public void The_host_refuses_to_start_without_a_usable_key(string? keys)
    {
        using var host = new Host(keys);

        var ex = Assert.Throws<InvalidOperationException>(() => _ = host.Services.GetService<object>());

        Assert.Contains("Chat:Encryption:Keys", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_refusal_never_echoes_key_material()
    {
        const string badKey = "AAECAwQFBgcICQoLDA0ODw==";
        using var host = new Host($"1:{badKey}");

        var ex = Assert.Throws<InvalidOperationException>(() => _ = host.Services.GetService<object>());

        Assert.DoesNotContain(badKey, ex.Message, StringComparison.Ordinal);
    }
}
