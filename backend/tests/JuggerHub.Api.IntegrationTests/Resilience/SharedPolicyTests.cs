using System.Net;
using Microsoft.Extensions.Logging;

namespace JuggerHub.Api.IntegrationTests.Resilience;

/// <summary>
/// The shared policy is genuinely generic (US5, FR-018/FR-019) and observable (US6,
/// FR-027/FR-028).
/// </summary>
/// <remarks>
/// US5's acceptance test was "a throwaway client inherits everything with one chained call".
/// <see cref="OutboundResilienceHarness"/> already <em>is</em> that client — it registers under the
/// arbitrary integration name "Test" with its own configuration section and writes no resilience
/// code of its own. Asserting against it keeps the proof permanent instead of deleting it after one
/// manual check.
/// </remarks>
public sealed class SharedPolicyTests
{
    [Fact]
    public async Task An_integration_registered_under_any_name_inherits_the_whole_policy()
    {
        // Named "Test", not "Resend": nothing about the shared extension is email-shaped.
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.Statuses(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK));

        await harness.SendAsync();

        Assert.Equal(2, harness.Transport.Calls);
    }

    [Fact]
    public async Task Two_integrations_can_be_tuned_independently_through_configuration_alone()
    {
        // FR-019: divergence lives in configuration, never in code. Same shared extension, same
        // client type, different behaviour — driven only by the bound section.
        using var patient = new OutboundResilienceHarness(
            ScriptedHandler.AlwaysFails(),
            new Dictionary<string, string?> { ["Resilience:Outbound:Test:MaxRetryAttempts"] = "4" });

        using var impatient = new OutboundResilienceHarness(
            ScriptedHandler.AlwaysFails(),
            new Dictionary<string, string?> { ["Resilience:Outbound:Test:MaxRetryAttempts"] = "1" });

        await Assert.ThrowsAsync<InvalidOperationException>(patient.SendAsync);
        await Assert.ThrowsAsync<InvalidOperationException>(impatient.SendAsync);

        Assert.Equal(5, patient.Transport.Calls);
        Assert.Equal(2, impatient.Transport.Calls);
    }

    [Fact]
    public async Task Retry_activity_is_visible_in_telemetry_with_the_integration_named()
    {
        // FR-027: an operator must be able to tell that retries happened, and to WHICH dependency,
        // without reproducing the fault.
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.Statuses(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK));

        await harness.SendAsync();

        Assert.NotEmpty(harness.Logs);
        Assert.Contains(
            harness.Logs,
            l => l.Message.Contains("Test", StringComparison.OrdinalIgnoreCase)
                || l.Message.Contains("retry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Telemetry_never_carries_the_credential_or_the_payload()
    {
        // FR-028 / constitution Principle I, asserted across every log the pipeline emits — retry
        // and breaker telemetry included, not just the give-up entry.
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.Statuses(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK));

        await harness.SendAsync();

        Assert.DoesNotContain(harness.Logs, l => l.Message.Contains("re_test_key_do_not_log"));
        Assert.DoesNotContain(harness.Logs, l => l.Message.Contains("Bearer"));
        Assert.DoesNotContain(harness.Logs, l => l.Message.Contains("<p>hi</p>"));
    }

    [Fact]
    public async Task A_give_up_is_logged_at_error_level_so_it_stands_out()
    {
        using var harness = new OutboundResilienceHarness(ScriptedHandler.AlwaysFails());

        await Assert.ThrowsAsync<InvalidOperationException>(harness.SendAsync);

        Assert.Contains(harness.Logs, l => l.Level == LogLevel.Error);
    }
}
