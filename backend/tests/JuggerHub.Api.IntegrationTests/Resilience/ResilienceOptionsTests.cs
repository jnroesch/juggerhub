using JuggerHub.Common;
using Microsoft.Extensions.Configuration;

namespace JuggerHub.Api.IntegrationTests.Resilience;

/// <summary>
/// Safe-default behaviour for outbound resilience limits (feature 028, FR-030; constitution
/// Principle VII).
/// </summary>
/// <remarks>
/// The rule under test is deliberately unusual: invalid configuration is <b>repaired</b>, not
/// rejected. The failure this prevents is a quiet one — a typo in a config key must never remove
/// the bound that stops a hung outbound call from holding a request open, and it must never take
/// the application down at startup either. So every test here asserts the same shape of outcome:
/// the limit still exists, and it is still safe.
/// </remarks>
public sealed class ResilienceOptionsTests
{
    private static ResilienceOptions Bind(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var options = new ResilienceOptions();
        configuration.GetSection($"{ResilienceOptions.SectionPrefix}:Test").Bind(options);
        options.Normalize();
        return options;
    }

    private static Dictionary<string, string?> Setting(string key, string value) =>
        new() { [$"{ResilienceOptions.SectionPrefix}:Test:{key}"] = value };

    [Fact]
    public void An_absent_section_yields_the_built_in_defaults()
    {
        var options = Bind([]);

        Assert.Equal(10, options.AttemptTimeoutSeconds);
        Assert.Equal(30, options.TotalTimeoutSeconds);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(0.1, options.BreakerFailureRatio);
        Assert.Equal(5, options.BreakerMinimumThroughput);
    }

    [Fact]
    public void Configured_values_are_honoured_when_they_are_sound()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            [$"{ResilienceOptions.SectionPrefix}:Test:AttemptTimeoutSeconds"] = "4",
            [$"{ResilienceOptions.SectionPrefix}:Test:TotalTimeoutSeconds"] = "20",
            [$"{ResilienceOptions.SectionPrefix}:Test:MaxRetryAttempts"] = "1",
        });

        Assert.Equal(4, options.AttemptTimeoutSeconds);
        Assert.Equal(20, options.TotalTimeoutSeconds);
        Assert.Equal(1, options.MaxRetryAttempts);
    }

    [Theory]
    [InlineData("AttemptTimeoutSeconds", "0")]
    [InlineData("AttemptTimeoutSeconds", "-5")]
    [InlineData("TotalTimeoutSeconds", "0")]
    [InlineData("TotalTimeoutSeconds", "-1")]
    [InlineData("BaseDelaySeconds", "0")]
    [InlineData("MaxRetryAttempts", "-2")]
    [InlineData("BreakerFailureRatio", "0")]
    [InlineData("BreakerFailureRatio", "1.5")]
    [InlineData("BreakerMinimumThroughput", "1")]
    [InlineData("BreakerSamplingSeconds", "0")]
    [InlineData("BreakerDurationSeconds", "-30")]
    public void An_invalid_value_falls_back_to_a_bounded_default(string key, string value)
    {
        var options = Bind(Setting(key, value));

        AssertEveryLimitIsBounded(options);
    }

    [Fact]
    public void A_zero_attempt_timeout_never_becomes_an_unbounded_wait()
    {
        // The specific disaster FR-030 exists to prevent: "0" reads as "no limit" in many
        // libraries. Here it must mean "use the safe default", never "wait forever".
        var options = Bind(Setting("AttemptTimeoutSeconds", "0"));

        Assert.True(options.AttemptTimeoutSeconds > 0);
        Assert.Equal(new ResilienceOptions().AttemptTimeoutSeconds, options.AttemptTimeoutSeconds);
    }

    [Fact]
    public void A_total_budget_that_cannot_contain_one_attempt_is_widened()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            [$"{ResilienceOptions.SectionPrefix}:Test:AttemptTimeoutSeconds"] = "30",
            [$"{ResilienceOptions.SectionPrefix}:Test:TotalTimeoutSeconds"] = "5",
        });

        // Widened rather than clamped: shrinking the attempt would silently cut off a legitimately
        // slow call, which is the worse of the two failures.
        Assert.True(options.TotalTimeoutSeconds > options.AttemptTimeoutSeconds);
        Assert.Equal(30, options.AttemptTimeoutSeconds);
    }

    [Fact]
    public void The_breaker_window_is_widened_to_at_least_double_the_attempt_timeout()
    {
        // The resilience pipeline refuses to build otherwise, so an unrepaired value here would be
        // a startup crash rather than a degraded limit.
        var options = Bind(new Dictionary<string, string?>
        {
            [$"{ResilienceOptions.SectionPrefix}:Test:AttemptTimeoutSeconds"] = "40",
            [$"{ResilienceOptions.SectionPrefix}:Test:BreakerSamplingSeconds"] = "10",
        });

        Assert.True(options.BreakerSamplingSeconds >= options.AttemptTimeoutSeconds * 2);
    }

    [Fact]
    public void Normalize_reports_what_it_repaired_and_stays_silent_otherwise()
    {
        var sound = new ResilienceOptions();
        Assert.Empty(sound.Normalize());

        var broken = new ResilienceOptions { AttemptTimeoutSeconds = -1, BreakerFailureRatio = 9 };
        var problems = broken.Normalize();

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains(nameof(ResilienceOptions.AttemptTimeoutSeconds)));
        Assert.Contains(problems, p => p.Contains(nameof(ResilienceOptions.BreakerFailureRatio)));
    }

    private static void AssertEveryLimitIsBounded(ResilienceOptions options)
    {
        Assert.True(options.AttemptTimeoutSeconds > 0, "attempt timeout must stay bounded");
        Assert.True(options.TotalTimeoutSeconds > options.AttemptTimeoutSeconds, "total budget must exceed one attempt");
        Assert.True(options.MaxRetryAttempts >= 0, "retry count must not be negative");
        Assert.True(options.BaseDelaySeconds > 0, "backoff must be positive");
        Assert.InRange(options.BreakerFailureRatio, 0.0001, 1);
        Assert.True(options.BreakerMinimumThroughput >= 2, "breaker throughput must meet the strategy floor");
        Assert.True(options.BreakerSamplingSeconds >= options.AttemptTimeoutSeconds * 2, "breaker window must fit an attempt");
        Assert.True(options.BreakerDurationSeconds > 0, "break duration must be bounded");
    }
}
