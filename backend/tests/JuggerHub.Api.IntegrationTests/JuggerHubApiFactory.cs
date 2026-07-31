using System.Collections.Concurrent;
using JuggerHub.Api.IntegrationTests.Chat;
using JuggerHub.Data;
using JuggerHub.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;
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

    /// <summary>
    /// Real blob storage for media (feature 035 / #97). Azurite speaks the genuine Azure Blob REST
    /// API, so tests exercise the same client, the same transport, and the same resilience pipeline
    /// that Dev and Prod use — an in-memory IMediaStore fake would assert our own stub instead.
    /// </summary>
    private readonly AzuriteContainer _mediaStore =
        new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0").Build();

    /// <summary>Container name the tests' media objects are written to.</summary>
    public const string MediaContainerName = "media";

    /// <summary>
    /// The Azurite blob endpoint, so a test can bypass the API and request an object key straight
    /// from storage — the check that proves the store is not publicly readable (SC-010).
    /// </summary>
    public string MediaBlobEndpoint =>
        $"http://{_mediaStore.Hostname}:{_mediaStore.GetMappedPublicPort(10000)}/devstoreaccount1";

    /// <summary>Captures outbound auth emails so tests can read verification/reset links.</summary>
    public TestEmailSender EmailSender { get; } = new();

    /// <summary>Captured server-side error logs (incl. exceptions) for diagnostics.</summary>
    public ConcurrentQueue<string> ErrorLogs { get; } = new();

    /// <summary>
    /// Records chat's realtime pushes so tests can assert the fan-out contract — and, crucially, who
    /// is <em>not</em> in the audience — without a live socket (feature 019).
    /// </summary>
    public FakeChatRealtime ChatRealtime { get; } = new();

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
                // Email: provider is irrelevant (sender is replaced below), but the
                // template links need a base URL the tests can parse.
                ["Email:Provider"] = "Smtp",
                ["Email:FromAddress"] = "test@juggerhub.local",
                ["Email:FrontendBaseUrl"] = "http://localhost:3000",
                // Feature 013 — the platform-admin sync source. Tests register an account
                // with this email and re-run the role sync (see RecognitionTestSupport)
                // to exercise admin-only routes.
                ["Admin:Emails"] = "admin@test.de",
                // Feature 030 (R8) — skip loading the full ~235k cities500 dataset; tests seed a
                // small CityReference fixture (TestReferenceCities) in InitializeAsync instead.
                ["Seeding:CityReferences"] = "false",
                // Feature 035 — media object store, backed by the Azurite container above.
                ["MediaStorage:ConnectionString"] = _mediaStore.GetConnectionString(),
                ["MediaStorage:ContainerName"] = MediaContainerName,
                // Keep the store's resilience pipeline fast: a test that exercises an outage should
                // fail in seconds, not sit through production-sized backoff.
                ["Resilience:Outbound:MediaStore:AttemptTimeoutSeconds"] = "2",
                ["Resilience:Outbound:MediaStore:TotalTimeoutSeconds"] = "6",
                ["Resilience:Outbound:MediaStore:MaxRetryAttempts"] = "1",
                ["Resilience:Outbound:MediaStore:BaseDelaySeconds"] = "1",
            });
        });

        // Replace the real email sender with the in-memory capture so no SMTP is needed.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            // Same trick for chat's realtime seam: no socket, and the pushes become assertable.
            services.RemoveAll<JuggerHub.Services.Chat.Realtime.IChatRealtime>();
            services.AddSingleton<JuggerHub.Services.Chat.Realtime.IChatRealtime>(ChatRealtime);
        });

        builder.ConfigureLogging(logging => logging.AddProvider(new CaptureLoggerProvider(ErrorLogs)));
    }

    public async Task InitializeAsync()
    {
        // Both containers must be up before the host is built: the configuration above reads their
        // connection strings, and the app resolves the blob client during startup.
        await Task.WhenAll(_database.StartAsync(), _mediaStore.StartAsync());

        // Accessing Services builds the host, which runs Program's startup migrations against the
        // now-started container. Then seed the small CityReference fixture the tests select from.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await TestReferenceCities.SeedAsync(db);
    }

    public new async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        await _mediaStore.DisposeAsync();
    }
}

internal sealed class CaptureLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _sink;

    public CaptureLoggerProvider(ConcurrentQueue<string> sink) => _sink = sink;

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, _sink);

    public void Dispose() { }

    private sealed class CaptureLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<string> _sink;

        public CaptureLogger(string category, ConcurrentQueue<string> sink)
        {
            _category = category;
            _sink = sink;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                _sink.Enqueue($"[{logLevel}] {_category}: {formatter(state, exception)}{(exception is null ? "" : "\n" + exception)}");
            }
        }
    }
}
