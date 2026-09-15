using JuggerHub.Common;
using JuggerHub.Resilience;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Results;

/// <summary>
/// Registers the Tugeny integration (feature 050) — the one place its wiring lives, so the app and
/// the resilience tests exercise exactly the same registration.
/// </summary>
public static class TugenyServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="TugenyOptions"/> and registers the named <c>"Tugeny"</c> client and
    /// <see cref="ITugenyClient"/>. Returns the client builder so a test can swap the transport.
    /// </summary>
    /// <remarks>
    /// Principle VII: one named client, the shared pipeline, one configuration section
    /// (<c>Resilience:Outbound:Tugeny</c>); nothing else retries or times out. Every call is a GET, so
    /// retrying is idempotent. A <c>429</c> FROM Tugeny is the provider throttling us and is retried
    /// with backoff; the <c>429</c> our own <c>tugeny</c> rate-limit policy returns to the browser is
    /// never retried. The size guard is added AFTER the pipeline so it is the inner handler: it runs
    /// per attempt, inside the attempt's time limit, and an oversized body fails once instead of being
    /// fetched again on every retry.
    /// </remarks>
    public static IHttpClientBuilder AddTugenyClient(this IServiceCollection services, IConfiguration configuration)
    {
        // Invalid settings are repaired to safe defaults — never to "unlimited" — and logged at startup.
        services.AddOptions<TugenyOptions>()
            .Bind(configuration.GetSection(TugenyOptions.SectionName))
            .PostConfigure(options => options.Normalize());

        services.AddScoped<ITugenyClient, TugenyClient>();

        return services
            .AddHttpClient(TugenyOptions.ResilienceName, (sp, client) =>
                client.BaseAddress = sp.GetRequiredService<IOptions<TugenyOptions>>().Value.BaseUri)
            .AddJuggerHubResilience(configuration, TugenyOptions.ResilienceName)
            .AddHttpMessageHandler(sp =>
                new ResponseSizeLimitHandler(sp.GetRequiredService<IOptions<TugenyOptions>>().Value.MaxResponseBytes));
    }
}
