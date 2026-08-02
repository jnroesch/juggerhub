using JuggerHub.Common;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Retention;

/// <summary>
/// Runs every registered <see cref="IRetentionSweep"/> on a fixed interval (GH #106).
/// </summary>
/// <remarks>
/// <para>
/// In-process rather than a Kubernetes CronJob, unlike feature 038's session-recording retention.
/// That one runs <c>psql</c> because it deletes from Umami's database under Umami's own scoped
/// role. This deletes from the <em>application</em> database, whose schema EF owns: doing it in
/// raw SQL from a Job would hand a cron pod the Postgres superuser credential, restate the schema
/// somewhere it cannot be type-checked, and leave the statement untested. Here it runs in every
/// environment from local to Prod, out of the same code path, covered by the integration suite.
/// </para>
/// <para>
/// Every replica runs this, so sweeps overlap. That is safe by construction and not by locking:
/// each sweep deletes by age, so whichever replica gets there first removes the rows and the
/// others match nothing. The cost of a redundant run is one indexed <c>DELETE</c> that affects
/// zero rows.
/// </para>
/// <para>
/// <b>Failure is loud.</b> Retention is the one background activity on this platform where silence
/// is worse than noise: the privacy policy states a period, so a sweep that quietly stops turns a
/// published legal statement false with nothing to notice. Hence the error log on every failed
/// sweep, and a bounded timeout so a stuck run surfaces instead of hanging (Principle VII).
/// A failure never stops the loop or the host — the next interval tries again, and deleting by age
/// means a missed run is corrected rather than compounded.
/// </para>
/// </remarks>
public sealed class RetentionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly RetentionOptions _options;
    private readonly ILogger<RetentionBackgroundService> _logger;

    public RetentionBackgroundService(
        IServiceProvider services,
        IOptions<RetentionOptions> options,
        ILogger<RetentionBackgroundService> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Retention sweeps are disabled by configuration.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(_options.StartupDelayMinutes), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return; // shutting down before the first sweep
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.SweepIntervalHours));

        do
        {
            await RunAllAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task RunAllAsync(CancellationToken stoppingToken)
    {
        // A scope per pass, not per process: the sweeps resolve a scoped AppDbContext, and holding
        // one open between daily runs would keep a pooled connection idle for 24 hours.
        using var scope = _services.CreateScope();
        var sweeps = scope.ServiceProvider.GetServices<IRetentionSweep>();

        foreach (var sweep in sweeps)
        {
            // One sweep's failure must not skip the rest — they are unrelated categories.
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(_options.SweepTimeoutMinutes));

                var deleted = await sweep.SweepAsync(timeout.Token);

                _logger.LogInformation(
                    "Retention sweep {Sweep} deleted {Count} rows.", sweep.Name, deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return; // ordinary shutdown, not a failure
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Retention sweep {Sweep} failed. Data past its retention period is still stored, "
                        + "which the privacy policy says it is not.",
                    sweep.Name);
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
