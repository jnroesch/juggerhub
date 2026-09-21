using JuggerHub.Common;
using JuggerHub.Services.Chat.Push;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Hosted;

/// <summary>
/// Runs the chat push pass on a short interval (feature 056 / GH #309).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is why chat push is out of band.</b> <c>ChatMessageService.SendAsync</c> is the hottest
/// write path in the product, and a fan-out to N devices awaited inside it would be a latency
/// regression on every message to buy a convenience on some of them. Nothing on the send path
/// knows this exists.
/// </para>
/// <para>
/// <b>Every replica runs this, and that is safe without locking.</b> Passes overlap, claim the same
/// rows, and occasionally both dispatch — but every notification for a conversation carries the same
/// collapse tag, so a duplicate replaces its twin on the device and the member sees one. Same
/// argument <c>RetentionBackgroundService</c> makes for itself, with "deleting by age is
/// idempotent" replaced by "dispatching under a collapse tag is idempotent on the device".
/// </para>
/// <para>
/// <b>Failure is quiet here, unlike retention.</b> Retention logs loudly because a sweep that
/// silently stops makes a published legal statement false. Nothing comparable is at stake in a
/// missed notification: the message is in the conversation, the badge counts it, and the member
/// finds it the moment they next open the app. So a failed pass is a warning and the loop
/// continues.
/// </para>
/// <para>
/// <b>There is no retry, deliberately.</b> A message is marked considered whether or not delivery
/// succeeded, and nothing re-queues it. Retrying would amplify an incident on the busiest table in
/// the product to deliver something that has a durable equivalent already waiting (FR-003,
/// constitution VII). For the same reason nothing here is wrapped in <c>AddJuggerHubResilience</c>:
/// the outbound call belongs to feature 055's typed client, which already carries the timeout,
/// the jittered retry, the <c>Retry-After</c> handling and the breaker. Adding a second pipeline
/// around it would stack handlers.
/// </para>
/// </remarks>
public sealed class ChatPushBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ChatPushOptions _options;
    private readonly ILogger<ChatPushBackgroundService> _logger;

    public ChatPushBackgroundService(
        IServiceProvider services,
        IOptions<ChatPushOptions> options,
        ILogger<ChatPushBackgroundService> logger)
    {
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Chat push notifications are disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)));

        do
        {
            await RunPassAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task RunPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            // A scope per pass, not one held between passes: the scanner resolves a scoped
            // AppDbContext, and keeping one open across a poll interval would pin a pooled
            // connection for the life of the process.
            using var scope = _services.CreateScope();
            var scanner = scope.ServiceProvider.GetRequiredService<IChatPushScanner>();

            // Nothing waits forever, background loops included (constitution VII). A pass that
            // cannot finish surfaces instead of holding a connection until the process dies.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(_options.PassTimeoutMinutes));

            var considered = await scanner.RunOnceAsync(timeout.Token);

            if (considered > 0)
            {
                // A count, and nothing else. Never a message, a sender or a conversation name —
                // this is the feature that carries member-written content, so its logs are held to
                // a stricter line than the rest of the platform (FR-021c). Note this says
                // "considered", not "sent": most considered messages send nothing.
                _logger.LogInformation("Chat push pass considered {Count} message(s).", considered);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Ordinary shutdown, not a failure.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat push pass failed. The next one will pick up where it left off.");
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
