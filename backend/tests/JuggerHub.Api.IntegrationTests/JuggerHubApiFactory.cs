using System.Collections.Concurrent;
using JuggerHub.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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

    /// <summary>Captures outbound auth emails so tests can read verification/reset links.</summary>
    public TestEmailSender EmailSender { get; } = new();

    /// <summary>Captured server-side error logs (incl. exceptions) for diagnostics.</summary>
    public ConcurrentQueue<string> ErrorLogs { get; } = new();

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
            });
        });

        // Replace the real email sender with the in-memory capture so no SMTP is needed.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
        });

        builder.ConfigureLogging(logging => logging.AddProvider(new CaptureLoggerProvider(ErrorLogs)));
    }

    public Task InitializeAsync() => _database.StartAsync();

    public new Task DisposeAsync() => _database.DisposeAsync().AsTask();
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
