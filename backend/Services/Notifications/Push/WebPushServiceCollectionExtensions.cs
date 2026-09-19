using JuggerHub.Common;
using JuggerHub.Resilience;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Notifications.Push;

/// <summary>
/// Registers the Web Push integration (feature 055) — the one place its wiring lives, so the app
/// and the resilience tests exercise exactly the same registration.
/// </summary>
public static class WebPushServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="WebPushOptions"/> and registers <see cref="PushServiceClient"/> as a typed
    /// client behind the shared resilience pipeline. Returns the client builder so a test can swap
    /// the transport.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Principle VII.</b> One typed client, the shared pipeline, one configuration section
    /// (<c>Resilience:Outbound:WebPush</c>); nothing else retries or times out.
    /// </para>
    /// <para>
    /// <b><see cref="PushServiceClient.AutoRetryAfter"/> is set to false, and that is mandatory
    /// rather than stylistic.</b> It defaults to <c>true</c> with <c>MaxRetriesAfter = 0</c>, and
    /// the library's loop only applies a cap when that value is above zero — so at its defaults it
    /// retries a <c>429</c> <i>without a bound</i>, inside our own retry handler, and the two
    /// multiply. Feature 035 recorded the same defect for the Azure Blob SDK, where leaving both on
    /// stacked 3 x 3 = 9 attempts.
    /// </para>
    /// <para>
    /// <b>A <c>429</c> here is the PUSH SERVICE throttling us</b>, which is the retriable case: the
    /// shared pipeline retries it with jittered backoff and honours <c>Retry-After</c>. The
    /// <c>429</c> our own fail-closed rate limiter returns to a browser is the opposite and is
    /// never retried against. Same status code, opposite correct behaviour — the constitution
    /// requires that distinction to be written where it is implemented, so it is written here and
    /// again in <c>PushDispatcher</c> where the failures are handled.
    /// </para>
    /// <para>
    /// <c>404</c> and <c>410</c> mean the subscription is gone. They sit outside the standard
    /// handler's retry set, so they already fail fast on the first attempt; the dispatcher deletes
    /// the row. They must never be made retriable.
    /// </para>
    /// </remarks>
    public static IHttpClientBuilder AddWebPushClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WebPushOptions>()
            .Bind(configuration.GetSection(WebPushOptions.SectionName));

        // A NAMED client plus an explicit factory registration, rather than the typed-client
        // overload. `AddHttpClient<PushServiceClient>` with a two-parameter lambda is ambiguous
        // between the factory Func<HttpClient, IServiceProvider, TClient> and the configure
        // Action<IServiceProvider, HttpClient>, and the compiler resolves it to the latter — the
        // resulting error points at the lambda body rather than at the overload choice. This form
        // has one meaning and reads the same.
        var builder = services
            .AddHttpClient(WebPushOptions.ResilienceName)
            .AddJuggerHubResilience(configuration, WebPushOptions.ResilienceName);

        // Transient, so every resolution takes a fresh HttpClient from the factory and handler
        // rotation keeps working. The client object itself is cheap; it holds no connection.
        services.AddTransient(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WebPushOptions>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(WebPushOptions.ResilienceName);

            return new PushServiceClient(httpClient)
            {
                DefaultAuthentication = new VapidAuthentication(options.PublicKey, options.PrivateKey)
                {
                    Subject = options.Subject,
                },
                // See the remarks above: the default is an uncapped 429 loop inside our pipeline.
                AutoRetryAfter = false,
            };
        });

        services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();
        services.AddSingleton<IPushLocalizer, PushLocalizer>();
        services.AddSingleton<IPushContentComposer, PushContentComposer>();
        services.AddSingleton<IPushDispatcher, PushDispatcher>();
        services.AddScoped<IPushFanOut, PushFanOut>();

        return builder;
    }

    /// <summary>
    /// Fails startup when the VAPID configuration is missing or malformed, mirroring the chat
    /// encryption and Redis guards. There is deliberately no disable switch: a notification channel
    /// that is silently off is worse than a start that refuses and says why.
    /// </summary>
    /// <remarks>
    /// The message names the configuration keys and <b>never echoes key material</b> (Principle I).
    /// </remarks>
    public static void ValidateWebPushConfiguration(this IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<WebPushOptions>>().Value;
        if (options.IsConfigured)
        {
            return;
        }

        throw new InvalidOperationException(
            "Web Push is not configured. WebPush:Subject, WebPush:PublicKey and WebPush:PrivateKey must all be set, "
            + "and the subject must start with 'mailto:' or 'https://'. "
            + "Generate a key pair with scripts/New-VapidKeyPair.ps1. "
            + "Each environment needs its own pair — a subscription is bound to the public key it was created with.");
    }
}
