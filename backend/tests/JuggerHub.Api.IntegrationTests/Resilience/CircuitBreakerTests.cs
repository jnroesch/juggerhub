using System.Net;

namespace JuggerHub.Api.IntegrationTests.Resilience;

/// <summary>
/// The stop-condition on retry (feature 028, US3; constitution Principle VII — "retry without a
/// stop condition is a hazard, not a safeguard").
/// </summary>
/// <remarks>
/// <para>
/// The headline test is <see cref="The_default_minimum_throughput_would_never_trip_at_our_volume"/>,
/// which is really a test about <em>configuration</em>. The .NET standard handler defaults to a
/// minimum throughput of 100 calls per sampling window before it evaluates its failure ratio at
/// all. JuggerHub sends a handful of transactional emails per minute, so that window never fills —
/// meaning a breaker left on defaults is configured, reviewed, merged, and completely decorative.
/// </para>
/// <para>
/// This is the exact trap the research phase found, and it is worth a test rather than a comment
/// because it is invisible: nothing fails, nothing logs, the breaker simply never opens.
/// </para>
/// </remarks>
public sealed class CircuitBreakerTests
{
    /// <summary>Breaker tuned to JuggerHub's real volume, as shipped.</summary>
    private static Dictionary<string, string?> TunedToRealVolume => new()
    {
        ["Resilience:Outbound:Test:BreakerMinimumThroughput"] = "5",
        ["Resilience:Outbound:Test:BreakerFailureRatio"] = "0.5",
        ["Resilience:Outbound:Test:MaxRetryAttempts"] = "1",
    };

    private static async Task<int> SendUntilAsync(OutboundResilienceHarness harness, int sends)
    {
        for (var i = 0; i < sends; i++)
        {
            try
            {
                await harness.SendAsync();
            }
            catch
            {
                // Every send fails here; the interesting number is how many reached the transport.
            }
        }

        return harness.Transport.Calls;
    }

    [Fact]
    public async Task A_failing_provider_stops_being_called_once_the_breaker_opens()
    {
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.AlwaysFails(),
            TunedToRealVolume);

        var reached = await SendUntilAsync(harness, 20);

        // Without a breaker this would be 40 (20 sends × 2 attempts). Once open, calls fail without
        // touching the provider (FR-017), so outbound load stays bounded rather than growing with
        // the outage (FR-026).
        Assert.True(reached < 40, $"breaker never opened — {reached} calls reached the provider");
    }

    [Fact]
    public async Task The_default_minimum_throughput_would_never_trip_at_our_volume()
    {
        // Same failing provider, same number of sends — only the threshold differs. This is what
        // shipping the library default would have bought us: every single call still going out.
        using var withLibraryDefault = new OutboundResilienceHarness(
            ScriptedHandler.AlwaysFails(),
            new Dictionary<string, string?>
            {
                ["Resilience:Outbound:Test:BreakerMinimumThroughput"] = "100",
                ["Resilience:Outbound:Test:BreakerFailureRatio"] = "0.5",
                ["Resilience:Outbound:Test:MaxRetryAttempts"] = "1",
            });

        var reachedWithDefault = await SendUntilAsync(withLibraryDefault, 20);

        using var tuned = new OutboundResilienceHarness(ScriptedHandler.AlwaysFails(), TunedToRealVolume);
        var reachedWhenTuned = await SendUntilAsync(tuned, 20);

        // 20 sends × (1 attempt + 1 retry) — every last one goes out, because the window never
        // reaches 100 calls and the failure ratio is therefore never evaluated.
        Assert.Equal(40, reachedWithDefault);
        Assert.True(
            reachedWhenTuned < reachedWithDefault,
            $"the tuned breaker must shed load the library default lets through "
            + $"(tuned={reachedWhenTuned}, default={reachedWithDefault})");
    }

    [Fact]
    public async Task The_breaker_recovers_on_its_own_once_the_provider_is_healthy()
    {
        var calls = 0;
        var handler = ScriptedHandler.AlwaysFails();

        using var harness = new OutboundResilienceHarness(handler, TunedToRealVolume);

        await SendUntilAsync(harness, 20);
        calls = harness.Transport.Calls;

        // Break duration is 2s in the harness; wait it out so the breaker moves to half-open.
        await Task.Delay(TimeSpan.FromSeconds(3));

        try
        {
            await harness.SendAsync();
        }
        catch
        {
            // Still failing — the point is that a trial call was ALLOWED through.
        }

        Assert.True(
            harness.Transport.Calls > calls,
            "no trial call was permitted after the break duration, so the breaker never recovers");
    }

    [Fact]
    public async Task A_healthy_provider_is_never_broken()
    {
        // Guard against an over-eager breaker: normal operation must not be shed.
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.Statuses(HttpStatusCode.OK),
            TunedToRealVolume);

        for (var i = 0; i < 20; i++)
        {
            await harness.SendAsync();
        }

        Assert.Equal(20, harness.Transport.Calls);
    }
}
