using System.Net;
using JuggerHub.Common;
using JuggerHub.Resilience;
using JuggerHub.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JuggerHub.Api.IntegrationTests.Resilience;

/// <summary>
/// Builds a real <see cref="IEmailSender"/> wired through the real
/// <see cref="ResilienceExtensions.AddJuggerHubResilience"/> pipeline, with a scripted transport
/// underneath.
/// </summary>
/// <remarks>
/// Deliberately exercises the shipped registration rather than a hand-built Polly pipeline: the
/// thing most likely to break is the wiring — a mis-bound config section, or a limit that never
/// reaches the handler — and a test that rebuilds the pipeline itself would pass right through
/// that class of bug.
/// </remarks>
internal sealed class OutboundResilienceHarness : IDisposable
{
    private readonly ServiceProvider _provider;

    public OutboundResilienceHarness(
        ScriptedHandler transport,
        IDictionary<string, string?>? overrides = null)
    {
        Transport = transport;

        // Fast, bounded limits so the suite doesn't spend real seconds on backoff. Shape is
        // identical to production; only the values differ, which is the point of FR-019.
        var settings = new Dictionary<string, string?>
        {
            ["Resilience:Outbound:Test:AttemptTimeoutSeconds"] = "1",
            ["Resilience:Outbound:Test:TotalTimeoutSeconds"] = "30",
            ["Resilience:Outbound:Test:MaxRetryAttempts"] = "2",
            ["Resilience:Outbound:Test:BaseDelaySeconds"] = "0.01",
            ["Resilience:Outbound:Test:BreakerFailureRatio"] = "0.5",
            ["Resilience:Outbound:Test:BreakerMinimumThroughput"] = "100",
            ["Resilience:Outbound:Test:BreakerSamplingSeconds"] = "30",
            ["Resilience:Outbound:Test:BreakerDurationSeconds"] = "2",
        };

        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddProvider(new CapturingLoggerProvider(Logs));
        });
        services.Configure<EmailOptions>(options =>
        {
            options.FromAddress = "no-reply@juggerhub.test";
            options.Resend = new EmailOptions.ResendOptions { ApiKey = "re_test_key_do_not_log" };
        });

        services
            .AddHttpClient<IEmailSender, ResendEmailSender>()
            .AddJuggerHubResilience(configuration, "Test")
            .ConfigurePrimaryHttpMessageHandler(() => transport);

        _provider = services.BuildServiceProvider();
    }

    public ScriptedHandler Transport { get; }

    public List<CapturedLog> Logs { get; } = [];

    public IEmailSender Sender => _provider.GetRequiredService<IEmailSender>();

    public Task SendAsync() => Sender.SendAsync("player@example.com", "Verify your email", "<p>hi</p>");

    public void Dispose() => _provider.Dispose();
}

/// <summary>A transport that replays a scripted sequence and counts what it was asked to do.</summary>
internal sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly Func<int, CancellationToken, Task<HttpResponseMessage>> _script;
    private int _calls;

    private ScriptedHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> script) =>
        _script = script;

    /// <summary>Number of times the transport was actually reached (retries included).</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Returns each status in turn; the final status repeats once the list runs out.</summary>
    public static ScriptedHandler Statuses(params HttpStatusCode[] statuses) =>
        new(
            (call, _) =>
            {
                var status = statuses[Math.Min(call, statuses.Length) - 1];
                return Task.FromResult(new HttpResponseMessage(status));
            });

    /// <summary>Always fails with the same status.</summary>
    public static ScriptedHandler AlwaysFails(HttpStatusCode status = HttpStatusCode.InternalServerError) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)));

    /// <summary>
    /// Never answers — the hung-provider case. Honours cancellation, as a real socket read does,
    /// so the attempt timeout can actually cut it short rather than waiting out the full delay.
    /// </summary>
    public static ScriptedHandler Hangs() =>
        new(
            async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref _calls);
        return _script(call, cancellationToken);
    }
}

internal sealed record CapturedLog(LogLevel Level, string Message, Exception? Exception);

internal sealed class CapturingLoggerProvider(List<CapturedLog> sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(sink);

    public void Dispose() { }

    private sealed class CapturingLogger(List<CapturedLog> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (sink)
            {
                sink.Add(new CapturedLog(logLevel, formatter(state, exception), exception));
            }
        }
    }
}
