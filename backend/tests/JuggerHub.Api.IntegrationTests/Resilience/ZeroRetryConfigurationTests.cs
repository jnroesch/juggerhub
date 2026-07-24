using System.Net;

namespace JuggerHub.Api.IntegrationTests.Resilience;

/// <summary>
/// "No retries, please" must be a configurable choice, not a way to break the integration
/// (feature 028, FR-030).
/// </summary>
/// <remarks>
/// Found while writing the circuit-breaker tests: setting <c>MaxRetryAttempts=0</c> — an entirely
/// reasonable thing for an operator to write when they want a call attempted once — left the client
/// unable to send at all, silently. That is precisely the failure mode FR-030 exists to prevent, so
/// it gets its own test rather than a comment.
/// </remarks>
public sealed class ZeroRetryConfigurationTests
{
    private static Dictionary<string, string?> WithMaxRetryAttempts(string value) =>
        new() { ["Resilience:Outbound:Test:MaxRetryAttempts"] = value };

    [Fact]
    public async Task Zero_retries_still_sends_the_request_once()
    {
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.Statuses(HttpStatusCode.OK),
            WithMaxRetryAttempts("0"));

        await harness.SendAsync();

        Assert.Equal(1, harness.Transport.Calls);
    }

    [Fact]
    public async Task Zero_retries_means_a_transient_failure_is_not_retried()
    {
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.AlwaysFails(),
            WithMaxRetryAttempts("0"));

        await Assert.ThrowsAsync<InvalidOperationException>(harness.SendAsync);

        Assert.Equal(1, harness.Transport.Calls);
    }

    [Fact]
    public async Task A_negative_retry_count_falls_back_to_a_working_default()
    {
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.Statuses(HttpStatusCode.OK),
            WithMaxRetryAttempts("-3"));

        await harness.SendAsync();

        Assert.Equal(1, harness.Transport.Calls);
    }
}
