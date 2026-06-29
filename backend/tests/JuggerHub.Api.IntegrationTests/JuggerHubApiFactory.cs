using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace JuggerHub.Api.IntegrationTests;

/// <summary>
/// Boots the real API against a disposable PostgreSQL 18 container
/// (Testcontainers), so tests exercise the genuine wiring — EF Core, Npgsql,
/// the startup auto-migration, and the auth pipeline — rather than an in-memory
/// substitute. The app applies migrations on startup against the container.
/// </summary>
public sealed class JuggerHubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _database.GetConnectionString(),
                // Minimal JWT config so the auth pipeline can configure itself.
                ["Jwt:Issuer"] = "juggerhub-tests",
                ["Jwt:Audience"] = "juggerhub-tests",
                ["Jwt:SigningKey"] = "integration-tests-signing-key-at-least-32-bytes-long!!",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15",
            });
        });
    }

    public Task InitializeAsync() => _database.StartAsync();

    public new Task DisposeAsync() => _database.DisposeAsync().AsTask();
}
