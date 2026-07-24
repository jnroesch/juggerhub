namespace JuggerHub.Common;

/// <summary>
/// Limits governing calls to ONE external service (feature 028; constitution Principle VII).
/// Bound from <c>Resilience:Outbound:&lt;Name&gt;</c>, so every integration is tunable per
/// environment without a rebuild while the shape stays identical everywhere (Principle V).
/// No secrets.
/// </summary>
/// <remarks>
/// Every value is optional. Missing or invalid configuration falls back to the built-in default
/// below — <b>never</b> to "unlimited" and never to a disabled limit. That rule is load-bearing:
/// a typo in a config key must not silently remove the bound that stops a hung call from holding
/// a request open, so <see cref="Normalize"/> repairs rather than rejects.
/// </remarks>
public sealed class ResilienceOptions
{
    public const string SectionPrefix = "Resilience:Outbound";

    /// <summary>Time limit for a single attempt.</summary>
    public int AttemptTimeoutSeconds { get; set; } = 10;

    /// <summary>Time limit across every attempt together, including the waits between them.</summary>
    public int TotalTimeoutSeconds { get; set; } = 30;

    /// <summary>Retries AFTER the first try, so 3 means up to 4 calls.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>First backoff step; delays grow exponentially from here and are jittered.</summary>
    public double BaseDelaySeconds { get; set; } = 2;

    /// <summary>Share of calls that must fail before the breaker opens.</summary>
    public double BreakerFailureRatio { get; set; } = 0.1;

    /// <summary>
    /// Calls that must be observed inside the sampling window before the failure ratio is
    /// evaluated at all.
    /// </summary>
    /// <remarks>
    /// Deliberately far below the library default of <b>100</b>. The standard handler expects
    /// service-to-service traffic; JuggerHub sends a handful of transactional emails per minute,
    /// so a threshold of 100 per window is never reached and the breaker would never open —
    /// configured, reviewed, and completely decorative. Derive this from the integration's real
    /// call rate, never by copying a default. See specs/028-network-resilience/research.md §2.
    /// </remarks>
    public int BreakerMinimumThroughput { get; set; } = 5;

    /// <summary>Window over which the failure ratio is measured.</summary>
    public int BreakerSamplingSeconds { get; set; } = 60;

    /// <summary>How long calls are skipped once the breaker opens, before a trial call.</summary>
    public int BreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Repairs any value that would weaken or invalidate a limit, returning the reasons so the
    /// caller can log them at startup. Returns an empty list when the configuration is sound.
    /// </summary>
    public IReadOnlyList<string> Normalize()
    {
        var defaults = new ResilienceOptions();
        var problems = new List<string>();

        if (AttemptTimeoutSeconds <= 0)
        {
            problems.Add($"{nameof(AttemptTimeoutSeconds)} must be positive; using {defaults.AttemptTimeoutSeconds}.");
            AttemptTimeoutSeconds = defaults.AttemptTimeoutSeconds;
        }

        if (TotalTimeoutSeconds <= 0)
        {
            problems.Add($"{nameof(TotalTimeoutSeconds)} must be positive; using {defaults.TotalTimeoutSeconds}.");
            TotalTimeoutSeconds = defaults.TotalTimeoutSeconds;
        }

        if (MaxRetryAttempts < 0)
        {
            problems.Add($"{nameof(MaxRetryAttempts)} cannot be negative; using {defaults.MaxRetryAttempts}.");
            MaxRetryAttempts = defaults.MaxRetryAttempts;
        }

        if (BaseDelaySeconds <= 0)
        {
            problems.Add($"{nameof(BaseDelaySeconds)} must be positive; using {defaults.BaseDelaySeconds}.");
            BaseDelaySeconds = defaults.BaseDelaySeconds;
        }

        if (BreakerFailureRatio is <= 0 or > 1)
        {
            problems.Add($"{nameof(BreakerFailureRatio)} must be within (0,1]; using {defaults.BreakerFailureRatio}.");
            BreakerFailureRatio = defaults.BreakerFailureRatio;
        }

        if (BreakerMinimumThroughput < 2)
        {
            // Polly's own floor is 2; anything lower is rejected by the strategy at build time.
            problems.Add($"{nameof(BreakerMinimumThroughput)} must be at least 2; using {defaults.BreakerMinimumThroughput}.");
            BreakerMinimumThroughput = defaults.BreakerMinimumThroughput;
        }

        if (BreakerSamplingSeconds <= 0)
        {
            problems.Add($"{nameof(BreakerSamplingSeconds)} must be positive; using {defaults.BreakerSamplingSeconds}.");
            BreakerSamplingSeconds = defaults.BreakerSamplingSeconds;
        }

        if (BreakerDurationSeconds <= 0)
        {
            problems.Add($"{nameof(BreakerDurationSeconds)} must be positive; using {defaults.BreakerDurationSeconds}.");
            BreakerDurationSeconds = defaults.BreakerDurationSeconds;
        }

        // The two cross-field rules are checked LAST, against the already-repaired values.

        // A single attempt that may outlive the total budget makes the total meaningless. Widen the
        // total rather than shrink the attempt — shrinking would silently cut off a legitimately
        // slow call, which is a worse failure than a generous ceiling.
        if (TotalTimeoutSeconds <= AttemptTimeoutSeconds)
        {
            var widened = AttemptTimeoutSeconds * 3;
            problems.Add(
                $"{nameof(TotalTimeoutSeconds)} ({TotalTimeoutSeconds}s) must exceed "
                + $"{nameof(AttemptTimeoutSeconds)} ({AttemptTimeoutSeconds}s); using {widened}s.");
            TotalTimeoutSeconds = widened;
        }

        // The standard resilience pipeline refuses to build unless the breaker's sampling window is
        // at least double the attempt timeout — otherwise a window could not contain even one
        // completed attempt. Repair it here so a bad config value degrades to a working default
        // instead of taking the whole application down at startup (FR-030).
        var minimumSampling = AttemptTimeoutSeconds * 2;
        if (BreakerSamplingSeconds < minimumSampling)
        {
            problems.Add(
                $"{nameof(BreakerSamplingSeconds)} ({BreakerSamplingSeconds}s) must be at least double "
                + $"{nameof(AttemptTimeoutSeconds)} ({AttemptTimeoutSeconds}s); using {minimumSampling}s.");
            BreakerSamplingSeconds = minimumSampling;
        }

        return problems;
    }
}
