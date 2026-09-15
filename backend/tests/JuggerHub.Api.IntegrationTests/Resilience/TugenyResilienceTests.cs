using System.Net;
using System.Text;
using JuggerHub.Common;
using JuggerHub.Resilience;
using JuggerHub.Services.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JuggerHub.Api.IntegrationTests.Resilience;

/// <summary>
/// Principle VII / Gate 8 for the Tugeny integration (feature 050): the shipped registration
/// (<see cref="TugenyServiceCollectionExtensions.AddTugenyClient"/>) with the SHIPPED limits from
/// <c>appsettings.json</c>, over a scripted transport. Only the backoff delay is shortened.
/// </summary>
public sealed class TugenyResilienceTests
{
    /// <summary>A provider wired exactly as the app wires it, reading the shipped Tugeny settings.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public Harness(HttpMessageHandler transport, IDictionary<string, string?>? overrides = null)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Tugeny:BaseUrl"] = "http://tugeny.test/",
                // The one deviation from production: don't spend real seconds on backoff.
                ["Resilience:Outbound:Tugeny:BaseDelaySeconds"] = "0.01",
            };
            foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
            {
                settings[key] = value;
            }

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
                .AddInMemoryCollection(settings)
                .Build();

            var services = new ServiceCollection();
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddProvider(new CapturingLoggerProvider(Logs));
            });
            services.AddTugenyClient(configuration).ConfigurePrimaryHttpMessageHandler(() => transport);
            _provider = services.BuildServiceProvider();
        }

        public List<CapturedLog> Logs { get; } = [];

        public HttpClient Client => _provider.GetRequiredService<IHttpClientFactory>().CreateClient(TugenyOptions.ResilienceName);

        public ITugenyClient Tugeny => _provider.CreateScope().ServiceProvider.GetRequiredService<ITugenyClient>();

        public void Dispose() => _provider.Dispose();
    }

    [Fact]
    public void The_shipped_breaker_is_tuned_to_tugenys_volume()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();
        var options = configuration.GetSection("Resilience:Outbound:Tugeny").Get<ResilienceOptions>()!;

        // About five calls per import session: a breaker needing more than a handful never opens.
        Assert.Equal(4, options.BreakerMinimumThroughput);
        Assert.Equal(2, options.MaxRetryAttempts);
        Assert.Equal(10, options.AttemptTimeoutSeconds);
        Assert.Equal(30, options.TotalTimeoutSeconds);
    }

    [Fact]
    public void The_pipeline_not_the_client_owns_the_time_budget()
    {
        using var harness = new Harness(ScriptedHandler.Statuses(HttpStatusCode.OK));

        Assert.Equal(Timeout.InfiniteTimeSpan, harness.Client.Timeout);
        Assert.Equal(new Uri("http://tugeny.test/"), harness.Client.BaseAddress);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)] // Tugeny throttling us — retried, unlike our own 429
    public async Task Transient_answers_are_retried_twice(HttpStatusCode status)
    {
        var transport = ScriptedHandler.AlwaysFails(status);
        using var harness = new Harness(transport);

        using var response = await harness.Client.GetAsync("api/persistent/teams");

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(3, transport.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Rejections_are_not_retried(HttpStatusCode status)
    {
        var transport = ScriptedHandler.AlwaysFails(status);
        using var harness = new Harness(transport);

        using var response = await harness.Client.GetAsync("api/persistent/teams");

        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task The_breaker_opens_after_four_failed_attempts()
    {
        var transport = ScriptedHandler.AlwaysFails();
        using var harness = new Harness(transport);

        for (var i = 0; i < 5; i++)
        {
            try
            {
                using var _ = await harness.Client.GetAsync("api/persistent/teams");
            }
            catch (Exception)
            {
                // An open breaker refuses without reaching the transport.
            }
        }

        // First call: 3 attempts. Second call: its 1st attempt is the 4th failure and opens the
        // breaker; nothing after that reaches Tugeny.
        Assert.Equal(4, transport.Calls);
    }

    [Fact]
    public async Task An_oversized_body_fails_once_and_is_not_retried()
    {
        var transport = new BodyHandler(new string(' ', 2048));
        using var harness = new Harness(transport, new Dictionary<string, string?> { ["Tugeny:MaxResponseBytes"] = "1024" });

        await Assert.ThrowsAsync<ResponseTooLargeException>(() => harness.Client.GetAsync("api/persistent/teams"));

        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task A_body_is_never_written_to_a_log()
    {
        const string marker = "PRIVATE-BODY-MARKER";
        var transport = new BodyHandler($"{{\"rankings\": \"{marker}\"}}", HttpStatusCode.OK);
        using var harness = new Harness(transport);

        var result = await harness.Tugeny.GetRankingAsync(200);

        Assert.Equal(TugenyOutcome.Unusable, result.Outcome);
        Assert.NotEmpty(harness.Logs);
        Assert.DoesNotContain(harness.Logs, log => log.Message.Contains(marker, StringComparison.Ordinal));
    }

    /// <summary>Answers every request with the same body, without a declared length.</summary>
    private sealed class BodyHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(body), writable: false)),
            });
        }
    }
}
