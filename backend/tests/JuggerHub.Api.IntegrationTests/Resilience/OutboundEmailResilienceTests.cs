using System.Net;
using JuggerHub.Services.Email;
using Microsoft.Extensions.Logging;

namespace JuggerHub.Api.IntegrationTests.Resilience;

/// <summary>
/// Outbound email resilience (feature 028, US2; constitution Principle VII).
/// </summary>
/// <remarks>
/// The failure these exist to prevent is the worst one in the product: a single provider blip used
/// to lose a verification email outright, leaving someone with an account they cannot activate and
/// no way to recover it themselves.
/// </remarks>
public sealed class OutboundEmailResilienceTests
{
    [Fact]
    public async Task A_transient_failure_is_retried_and_the_email_still_goes_out()
    {
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.Statuses(HttpStatusCode.InternalServerError, HttpStatusCode.OK));

        await harness.SendAsync();

        Assert.Equal(2, harness.Transport.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Transient_and_throttling_responses_are_retried(HttpStatusCode status)
    {
        // 429 IS retried here, deliberately: outbound it means the PROVIDER is throttling us, and
        // backing off is the correct response. On the browser hop the same code means our own
        // fail-closed limiter and is never retried — same status, opposite right answer.
        using var harness = new OutboundResilienceHarness(
            ScriptedHandler.Statuses(status, HttpStatusCode.OK));

        await harness.SendAsync();

        Assert.Equal(2, harness.Transport.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task A_permanent_rejection_is_never_retried(HttpStatusCode status)
    {
        // Rejected credentials or an invalid address will be rejected identically every time;
        // retrying only multiplies the rejection (FR-016).
        using var harness = new OutboundResilienceHarness(ScriptedHandler.AlwaysFails(status));

        await Assert.ThrowsAsync<InvalidOperationException>(harness.SendAsync);

        Assert.Equal(1, harness.Transport.Calls);
    }

    [Fact]
    public async Task Attempts_are_bounded_when_the_provider_stays_broken()
    {
        using var harness = new OutboundResilienceHarness(ScriptedHandler.AlwaysFails());

        await Assert.ThrowsAsync<InvalidOperationException>(harness.SendAsync);

        // 1 initial attempt + MaxRetryAttempts (2). Retry must never become unbounded, or it
        // becomes the outage it was meant to survive (FR-026).
        Assert.Equal(3, harness.Transport.Calls);
    }

    [Fact]
    public async Task A_provider_that_never_answers_is_abandoned_rather_than_held_open()
    {
        using var harness = new OutboundResilienceHarness(ScriptedHandler.Hangs());

        // The transport would sleep for five minutes; the attempt timeout is one second, so this
        // completing at all is the assertion (FR-014).
        var start = DateTimeOffset.UtcNow;
        await Assert.ThrowsAnyAsync<Exception>(harness.SendAsync);
        var elapsed = DateTimeOffset.UtcNow - start;

        Assert.True(elapsed < TimeSpan.FromMinutes(1), $"took {elapsed}, so it was not time-limited");
    }

    [Fact]
    public async Task Giving_up_is_logged_loudly_with_enough_detail_to_act_on()
    {
        using var harness = new OutboundResilienceHarness(ScriptedHandler.AlwaysFails());

        await Assert.ThrowsAsync<InvalidOperationException>(harness.SendAsync);

        var giveUp = Assert.Single(
            harness.Logs,
            l => l.Level == LogLevel.Error && l.Message.Contains("Email give-up"));

        // An operator must be able to tell WHO lost WHAT — without these the entry is unactionable,
        // and since durable delivery is out of scope this log is the only recovery path (FR-021).
        // The recipient is MASKED: enough to act on, without a full address in log storage.
        Assert.Contains("p***@example.com", giveUp.Message);
        Assert.Contains("Verify your email", giveUp.Message);
        Assert.DoesNotContain("player@example.com", giveUp.Message);
    }

    [Theory]
    [InlineData("player@example.com", "p***@example.com")]
    [InlineData("a@b.de", "a***@b.de")]
    [InlineData("  spaced@example.com  ", "s***@example.com")]
    [InlineData("@nolocal.com", "***")]
    [InlineData("not-an-address", "***")]
    [InlineData("", "(none)")]
    public void A_recipient_is_masked_before_it_reaches_a_log(string address, string expected)
    {
        Assert.Equal(expected, ResendEmailSender.MaskForLog(address));
    }

    [Fact]
    public void Unicode_line_separators_are_stripped_too()
    {
        // char.IsControl covers only C0/C1 (U+0000-U+001F, U+007F-U+009F). U+2028 LINE SEPARATOR
        // and U+2029 PARAGRAPH SEPARATOR are categories Zl/Zp, so a control-character filter alone
        // lets them through - and plenty of log viewers, JSON tooling and JS contexts treat them as
        // line breaks. That is still log forging, just through a narrower door.
        // Payload in the DOMAIN, not the local part: masking discards everything between the first
        // character and the "@", so a local-part payload proves nothing. The domain is kept verbatim.
        var hostile = "a@ex\u2028ample\u2029.com";

        var masked = ResendEmailSender.MaskForLog(hostile);

        Assert.DoesNotContain('\u2028', masked);
        Assert.DoesNotContain('\u2029', masked);
    }

    [Fact]
    public void A_recipient_cannot_forge_log_lines()
    {
        // cs/log-forging: the address is user-supplied, so a CR/LF payload could otherwise inject
        // fabricated entries and make the audit trail lie.
        var hostile = "evil\r\nfatal: system compromised\n@example.com";

        var masked = ResendEmailSender.MaskForLog(hostile);

        Assert.DoesNotContain('\r', masked);
        Assert.DoesNotContain('\n', masked);
        Assert.DoesNotContain("fatal:", masked);
    }

    [Fact]
    public async Task No_log_written_anywhere_leaks_the_api_key_or_the_response_body()
    {
        // FR-028 / constitution Principle I. Checked across EVERY log the pipeline produced, not
        // just the give-up entry, because retry and breaker telemetry also write as they go.
        using var harness = new OutboundResilienceHarness(ScriptedHandler.AlwaysFails());

        await Assert.ThrowsAsync<InvalidOperationException>(harness.SendAsync);

        Assert.DoesNotContain(harness.Logs, l => l.Message.Contains("re_test_key_do_not_log"));
        Assert.DoesNotContain(harness.Logs, l => l.Message.Contains("Bearer"));
    }
}
