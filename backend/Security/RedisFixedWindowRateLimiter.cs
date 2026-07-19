using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace JuggerHub.Security.RateLimiting;

/// <summary>
/// A fixed-window rate limiter whose counter lives in Redis, so one limit is shared across every
/// replica (feature 019).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> ASP.NET Core's built-in limiters keep their partitions in the process's own
/// memory. That is correct on one instance and quietly wrong on several: behind a load balancer, a
/// caller gets one bucket <em>per pod</em>, so the effective limit is <c>configured × replica count</c>
/// — and it drifts again every time the cluster autoscales. Nothing looks broken, which is what makes
/// it dangerous.
/// </para>
/// <para>
/// That matters here because chat's direct-message reach is deliberately open (spec FR-049): any player
/// may message any other. Blocking is per-recipient and reactive, so the rate limit is the only thing
/// standing between one account and the whole community (FR-049a). A limit that is really "10/min times
/// however many pods happen to be up" does not honour "enforced server-side".
/// </para>
/// <para>
/// <b>The algorithm</b> is the standard atomic fixed window: <c>INCR</c> the window's key, and set the
/// expiry on the first hit. Redis is single-threaded per key, so the increment is atomic across every
/// replica without a Lua script or a third-party package (constitution: minimal dependencies). The
/// window boundary is computed from wall-clock time, which every pod agrees on closely enough for a
/// one-minute bucket.
/// </para>
/// <para>
/// <b>It fails closed.</b> If Redis is unreachable the limiter <em>rejects</em>. A limiter that fails
/// open would turn a cache outage into an open mass-DM window — precisely the scenario FR-049a exists
/// to prevent. Chat is degraded during a Redis outage; the community is not exposed.
/// </para>
/// </remarks>
public sealed class RedisFixedWindowRateLimiter : RateLimiter
{
    private static readonly RateLimitLease FailedLease = new SimpleLease(false);
    private static readonly RateLimitLease AcquiredLease = new SimpleLease(true);

    private readonly IConnectionMultiplexer _redis;
    private readonly string _partitionKey;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly ILogger _logger;

    public RedisFixedWindowRateLimiter(
        IConnectionMultiplexer redis,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        ILogger logger)
    {
        _redis = redis;
        _partitionKey = partitionKey;
        _permitLimit = permitLimit;
        _window = window;
        _logger = logger;
    }

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    /// <summary>
    /// Always reports "not acquired" so the middleware falls through to <see cref="AcquireAsyncCore"/>.
    /// </summary>
    /// <remarks>
    /// The synchronous path cannot talk to Redis without blocking a request thread, and blocking on I/O
    /// inside a rate limiter would make the limiter itself the bottleneck it is meant to prevent.
    /// Declining here costs nothing: the rate-limiting middleware tries this first and then awaits
    /// <see cref="AcquireAsyncCore"/>, which does the real check.
    /// </remarks>
    protected override RateLimitLease AttemptAcquireCore(int permitCount) => FailedLease;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken)
    {
        // The window's key changes every period, so expired windows simply stop being addressed and
        // Redis reclaims them via the TTL — no sweeping, no cleanup job.
        var windowIndex = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (long)_window.TotalSeconds;
        var key = $"rl:{_partitionKey}:{windowIndex}";

        try
        {
            var db = _redis.GetDatabase();

            var count = await db.StringIncrementAsync(key, permitCount).ConfigureAwait(false);

            if (count == permitCount)
            {
                // First hit in this window — give the key a TTL slightly longer than the window so a
                // clock skew between pods cannot expire a window still in use.
                await db.KeyExpireAsync(key, _window + TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }

            return count > _permitLimit ? FailedLease : AcquiredLease;
        }
        catch (RedisException ex)
        {
            // Fail CLOSED. See the class remarks: an open failure mode here is an open mass-DM window.
            _logger.LogError(ex, "Rate limiter could not reach Redis; rejecting the request (fail-closed).");
            return FailedLease;
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Rate limiter timed out talking to Redis; rejecting the request (fail-closed).");
            return FailedLease;
        }
    }

    /// <summary>Minimal lease — these limiters carry no metadata and nothing to release.</summary>
    private sealed class SimpleLease : RateLimitLease
    {
        public SimpleLease(bool isAcquired) => IsAcquired = isAcquired;

        public override bool IsAcquired { get; }

        public override IEnumerable<string> MetadataNames => Array.Empty<string>();

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}
