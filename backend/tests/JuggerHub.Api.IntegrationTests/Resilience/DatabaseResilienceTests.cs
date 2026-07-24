using JuggerHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace JuggerHub.Api.IntegrationTests.Resilience;

/// <summary>
/// Database connection resiliency (feature 028, US4; constitution Principle VII).
/// </summary>
/// <remarks>
/// <para>
/// The change that makes a database restart survivable also makes every user-initiated transaction
/// throw unless it runs through the execution strategy. Ten call sites were restructured for it,
/// and the failure mode if one was missed is loud and immediate:
/// <c>InvalidOperationException: The configured execution strategy ... does not support
/// user-initiated transactions</c>.
/// </para>
/// <para>
/// These tests assert the configuration is actually on. The *coverage* for the ten restructured
/// sites is the existing Parties / Events / Marketplace suites — they drive every one of those
/// paths, and before this feature they would have started throwing the moment retry was enabled.
/// </para>
/// </remarks>
public sealed class DatabaseResilienceTests(JuggerHubApiFactory factory) : IClassFixture<JuggerHubApiFactory>
{
    private readonly JuggerHubApiFactory _factory = factory;

    [Fact]
    public void The_configured_execution_strategy_retries_transient_faults()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var strategy = db.Database.CreateExecutionStrategy();

        // A non-retrying strategy here would mean a database restart still surfaces as a 500 for
        // every request in flight — the thing US4 exists to fix.
        Assert.True(
            strategy.RetriesOnFailure,
            "EnableRetryOnFailure is not configured, so transient database faults are not retried");
    }

    [Fact]
    public async Task A_transaction_opened_through_the_execution_strategy_commits_normally()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var strategy = db.Database.CreateExecutionStrategy();

        var handleCount = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var count = await db.PlayerProfiles.CountAsync();
            await tx.CommitAsync();
            return count;
        });

        Assert.True(handleCount >= 0);
    }

    [Fact]
    public async Task A_transaction_opened_outside_the_execution_strategy_is_rejected_on_save()
    {
        // Regression guard for the whole of US4, and it pins down exactly WHERE the rejection
        // happens — which is narrower than the EF documentation suggests. Verified empirically on
        // EF 10 + Npgsql 10:
        //
        //   * BeginTransactionAsync outside an execution strategy  → succeeds, no exception
        //   * raw SQL / ExecuteSqlRawAsync inside that transaction → succeeds, no exception
        //   * the first SaveChangesAsync inside it                 → throws (this test)
        //
        // So the failure surfaces one step later than "opening a transaction throws". A future call
        // site that opens a transaction and only ever uses ExecuteUpdate/ExecuteDelete would slip
        // past it silently — and would silently lose retry-as-a-unit semantics. Grep for
        // BeginTransactionAsync, not for this exception.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            db.Teams.Add(new JuggerHub.Entities.Team
            {
                Name = "Execution strategy probe",
                Slug = $"exec-strategy-probe-{Guid.CreateVersion7():N}",
                City = "Berlin",
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        Assert.Contains("execution strategy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
