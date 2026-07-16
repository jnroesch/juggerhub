using System.Threading.RateLimiting;
using JuggerHub.Security.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace JuggerHub.Api.IntegrationTests.Chat;

/// <summary>
/// The distributed rate limiter (feature 019, FR-049a — research §11).
/// </summary>
/// <remarks>
/// <para>
/// The headline test here is <see cref="The_limit_is_shared_across_limiter_instances"/>. Two limiter
/// instances over one Redis stand in for two pods behind the load balancer — which is the case an
/// <em>in-memory</em> limiter would happily pass in a unit test while being wrong in production, since
/// each pod would keep its own counter and the real limit would be <c>configured × replicas</c>.
/// </para>
/// <para>
/// This matters because DM reach is open by product decision (FR-049): the rate limit is the only
/// thing bounding how fast one account can reach the whole community, so a limit that silently
/// multiplies with the replica count is not a limit.
/// </para>
/// </remarks>
public sealed class RedisRateLimiterTests : IAsyncLifetime
{
    // Same image as docker-compose, so the tests exercise what local and Dev/Prod actually run.
    private readonly RedisContainer _redis = new RedisBuilder("redis:8.2-alpine").Build();
    private IConnectionMultiplexer _mux = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _mux = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_mux is not null)
        {
            await _mux.DisposeAsync();
        }

        await _redis.DisposeAsync();
    }

    private RedisFixedWindowRateLimiter Limiter(string key, int limit, int windowSeconds = 60) =>
        new(_mux, key, limit, TimeSpan.FromSeconds(windowSeconds), NullLogger.Instance);

    [Fact]
    public async Task Permits_up_to_the_limit_then_rejects()
    {
        var limiter = Limiter($"t:{Guid.NewGuid():N}", limit: 3);

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await limiter.AcquireAsync(1)).IsAcquired, $"permit {i + 1} should be allowed");
        }

        Assert.False((await limiter.AcquireAsync(1)).IsAcquired);
        Assert.False((await limiter.AcquireAsync(1)).IsAcquired);
    }

    /// <summary>
    /// <b>The multi-replica test.</b> Two limiters, one Redis, one shared budget — as two pods must
    /// behave. An in-memory limiter would give each instance its own 3 permits (6 total) and pass a
    /// naive test while being wrong in production.
    /// </summary>
    [Fact]
    public async Task The_limit_is_shared_across_limiter_instances()
    {
        var key = $"t:{Guid.NewGuid():N}";
        var podA = Limiter(key, limit: 3);
        var podB = Limiter(key, limit: 3);

        // Spend the budget alternating between "pods".
        Assert.True((await podA.AcquireAsync(1)).IsAcquired);
        Assert.True((await podB.AcquireAsync(1)).IsAcquired);
        Assert.True((await podA.AcquireAsync(1)).IsAcquired);

        // The budget is now spent — on BOTH pods. This is the assertion an in-memory limiter fails.
        Assert.False((await podB.AcquireAsync(1)).IsAcquired);
        Assert.False((await podA.AcquireAsync(1)).IsAcquired);
    }

    [Fact]
    public async Task Different_partitions_have_independent_budgets()
    {
        var ada = Limiter($"t:{Guid.NewGuid():N}", limit: 1);
        var ben = Limiter($"t:{Guid.NewGuid():N}", limit: 1);

        Assert.True((await ada.AcquireAsync(1)).IsAcquired);
        Assert.False((await ada.AcquireAsync(1)).IsAcquired);

        // Ada exhausting her budget must not cost Ben anything.
        Assert.True((await ben.AcquireAsync(1)).IsAcquired);
    }

    [Fact]
    public async Task The_window_releases_the_budget()
    {
        var limiter = Limiter($"t:{Guid.NewGuid():N}", limit: 1, windowSeconds: 1);

        Assert.True((await limiter.AcquireAsync(1)).IsAcquired);
        Assert.False((await limiter.AcquireAsync(1)).IsAcquired);

        // Cross into the next window.
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.True((await limiter.AcquireAsync(1)).IsAcquired);
    }

    /// <summary>
    /// <b>Fails closed.</b> With Redis gone the limiter rejects rather than waving traffic through. A
    /// limiter that fails open would turn a cache outage into an open mass-DM window — the exact thing
    /// FR-049a exists to prevent. Degraded chat beats an exposed community.
    /// </summary>
    [Fact]
    public async Task Rejects_when_redis_is_unreachable()
    {
        // Point at a closed port rather than tearing down the shared container.
        var dead = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { { "127.0.0.1", 6390 } },
            AbortOnConnectFail = false,
            ConnectTimeout = 250,
            SyncTimeout = 250,
            ConnectRetry = 0,
        });

        var limiter = new RedisFixedWindowRateLimiter(
            dead, $"t:{Guid.NewGuid():N}", 10, TimeSpan.FromMinutes(1), NullLogger.Instance);

        var lease = await limiter.AcquireAsync(1);

        Assert.False(lease.IsAcquired);

        await dead.DisposeAsync();
    }

    /// <summary>The sync path declines by design, so the middleware falls through to the async check.</summary>
    [Fact]
    public void The_synchronous_path_declines_rather_than_blocking_on_redis()
    {
        var limiter = Limiter($"t:{Guid.NewGuid():N}", limit: 10);

        Assert.False(limiter.AttemptAcquire(1).IsAcquired);
    }
}
