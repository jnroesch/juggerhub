using JuggerHub.Common;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace JuggerHub.Resilience;

/// <summary>
/// The single, shared resilience policy for outbound calls (feature 028; constitution
/// Principle VII). Mirrors the registration style of
/// <see cref="Security.RateLimiting.RateLimitPolicies"/>: infrastructure composed once here,
/// inherited everywhere, never re-derived per call site.
/// </summary>
/// <remarks>
/// <para>
/// A new integration opts in with ONE chained call plus one configuration section:
/// <code>
/// builder.Services
///     .AddHttpClient&lt;IPaymentGateway, StripeGateway&gt;()
///     .AddJuggerHubResilience(builder.Configuration, "Stripe");
/// </code>
/// </para>
/// <para>
/// Callers MUST NOT write their own retry loop, set <c>HttpClient.Timeout</c>, or add a second
/// resilience handler — stacking handlers is explicitly unsupported, and a client-level timeout
/// cuts across the pipeline, collapsing the deliberate distinction between a per-attempt limit and
/// a total budget. Anything genuinely different about an integration belongs in its configuration
/// section, not in its code.
/// </para>
/// </remarks>
public static class ResilienceExtensions
{
    /// <summary>
    /// Applies the standard resilience pipeline — concurrency limiter, total timeout, retry,
    /// circuit breaker, per-attempt timeout — using limits bound from
    /// <c>Resilience:Outbound:&lt;name&gt;</c>.
    /// </summary>
    /// <param name="builder">The typed-client builder returned by <c>AddHttpClient</c>.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="name">
    /// Integration name. Selects the config section AND names the pipeline, so telemetry
    /// attributes every retry, timeout and breaker transition to the right dependency (FR-027).
    /// </param>
    public static IHttpClientBuilder AddJuggerHubResilience(
        this IHttpClientBuilder builder,
        IConfiguration configuration,
        string name)
    {
        var options = new ResilienceOptions();
        configuration.GetSection($"{ResilienceOptions.SectionPrefix}:{name}").Bind(options);

        // Repair anything unsafe BEFORE it reaches the pipeline, and surface why. A misconfigured
        // limit must degrade to the safe default, never to an unbounded wait (FR-030).
        var problems = options.Normalize();
        if (problems.Count > 0)
        {
            using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
            var logger = loggerFactory.CreateLogger(typeof(ResilienceExtensions));
            foreach (var problem in problems)
            {
                logger.LogWarning(
                    "Resilience configuration for '{Integration}' was invalid and has been corrected: {Problem}",
                    name,
                    problem);
            }
        }

        builder.AddStandardResilienceHandler(handler =>
        {
            handler.AttemptTimeout.Timeout = TimeSpan.FromSeconds(options.AttemptTimeoutSeconds);
            handler.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(options.TotalTimeoutSeconds);

            // "No retries" is a legitimate thing to configure, but the strategy's own floor is 1 —
            // supplying 0 throws OptionsValidationException at resolve time and takes the whole
            // integration down silently. So honour the intent instead of the number: keep the
            // strategy at its minimum and simply never handle anything, which yields exactly one
            // attempt. Clamping 0 up to the default would be worse than useless — it would give
            // MORE retries than were asked for.
            handler.Retry.MaxRetryAttempts = Math.Max(1, options.MaxRetryAttempts);
            if (options.MaxRetryAttempts == 0)
            {
                handler.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
            }

            handler.Retry.Delay = TimeSpan.FromSeconds(options.BaseDelaySeconds);
            handler.Retry.BackoffType = DelayBackoffType.Exponential;
            // Jitter spreads retries so many callers failing at once do not resynchronise into a
            // thundering herd against a service that is already struggling (Principle VII).
            handler.Retry.UseJitter = true;

            // NOTE: retries are deliberately NOT disabled for unsafe methods here. The email send
            // is a POST, and retrying it can duplicate a message if the provider accepted a request
            // whose response we never saw. That trade is intentional and asymmetric with the
            // browser hop: a duplicate email is an annoyance, a lost verification email is an
            // account nobody can activate. See research.md §3 before changing this.

            handler.CircuitBreaker.FailureRatio = options.BreakerFailureRatio;
            handler.CircuitBreaker.MinimumThroughput = options.BreakerMinimumThroughput;
            handler.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(options.BreakerSamplingSeconds);
            handler.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(options.BreakerDurationSeconds);
        });

        return builder;
    }
}
