using System.Net;
using System.Text.Json;
using JuggerHub.Data;
using Lib.Net.Http.WebPush;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Notifications.Push;

/// <summary>
/// Delivers push notifications over the Web Push protocol (feature 055).
/// </summary>
public sealed class PushDispatcher : IPushDispatcher
{
    /// <summary>
    /// How many deliveries run at once. Bounded on purpose: a team news post fans out to members
    /// times devices, and an unbounded sequential loop would hold the producing request open for as
    /// long as that takes. This is concurrency, not a resilience policy — the timeout, retry and
    /// breaker all still come from the shared pipeline.
    /// </summary>
    private const int MaxConcurrency = 8;

    /// <summary>
    /// How long a push service should hold an undelivered message. Short, because everything here
    /// is time-sensitive: a training that changed tomorrow is not worth announcing next week.
    /// </summary>
    private const int TimeToLiveSeconds = 6 * 60 * 60;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PushDispatcher> _logger;

    public PushDispatcher(IServiceScopeFactory scopes, ILogger<PushDispatcher> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DispatchAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        PushContent content,
        CancellationToken ct = default)
    {
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var devices = await db.PushSubscriptions.AsNoTracking()
            .Where(s => recipientUserIds.Contains(s.UserId))
            .Select(s => new Device(s.Id, s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(ct);

        // Nobody has a device enabled: no outbound call is made at all.
        if (devices.Count == 0)
        {
            return;
        }

        var body = JsonSerializer.Serialize(
            new { content.Title, content.Body, content.Url, content.Tag },
            JsonOptions);

        var gone = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        var delivered = new System.Collections.Concurrent.ConcurrentBag<Guid>();

        await Parallel.ForEachAsync(
            devices,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = ct },
            async (device, token) =>
            {
                using var perCall = _scopes.CreateScope();
                var client = perCall.ServiceProvider.GetRequiredService<PushServiceClient>();

                var subscription = new Lib.Net.Http.WebPush.PushSubscription { Endpoint = device.Endpoint };
                subscription.SetKey(PushEncryptionKeyName.P256DH, device.P256dh);
                subscription.SetKey(PushEncryptionKeyName.Auth, device.Auth);

                var message = new PushMessage(body)
                {
                    // Same value as the notification's tag. The tag makes a second arrival replace
                    // the first ON THE DEVICE; the topic makes a push service replace a message it
                    // still holds UNDELIVERED. Together they give once-only without a dedupe store.
                    Topic = Topic(content.Tag),
                    TimeToLive = TimeToLiveSeconds,
                    Urgency = PushMessageUrgency.Normal,
                };

                try
                {
                    await client.RequestPushMessageDeliveryAsync(subscription, message, token);
                    delivered.Add(device.Id);
                }
                catch (PushServiceClientException ex) when (IsGone(ex.StatusCode))
                {
                    // The subscription no longer exists: permission revoked, site data cleared, or
                    // the device wiped. This is a REJECTION, not a transient fault — it is outside
                    // the shared pipeline's retry set and must stay that way. Drop the row.
                    gone.Add(device.Id);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A 429 here is the PUSH SERVICE throttling us, which the shared pipeline has
                    // already retried with backoff honouring Retry-After before giving up. That is
                    // the opposite of a 429 from our own rate limiter, which is never retried
                    // against. Anything reaching this point has exhausted the policy.
                    //
                    // Status code and subscription id only. Never the response body, never the
                    // endpoint: the body can echo request content and the endpoint is effectively a
                    // capability for that device (constitution VII and I).
                    _logger.LogWarning(
                        "Push delivery failed for subscription {SubscriptionId} with status {Status}.",
                        device.Id,
                        (ex as PushServiceClientException)?.StatusCode is { } status ? (int)status : 0);
                }
            });

        await PruneAndTouchAsync(gone, delivered, ct);
    }

    /// <summary>
    /// Deletes what the push service said is gone and records a success timestamp for the rest. Run
    /// after the fan-out rather than inside it, so the writes are two statements instead of one per
    /// device, and on a fresh scope because the fan-out's parallel work must not share a context.
    /// </summary>
    private async Task PruneAndTouchAsync(
        IReadOnlyCollection<Guid> gone,
        IReadOnlyCollection<Guid> delivered,
        CancellationToken ct)
    {
        if (gone.Count == 0 && delivered.Count == 0)
        {
            return;
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (gone.Count > 0)
        {
            var removed = await db.PushSubscriptions
                .Where(s => gone.Contains(s.Id))
                .ExecuteDeleteAsync(ct);
            _logger.LogInformation("Removed {Count} push subscription(s) the push service reported gone.", removed);
        }

        if (delivered.Count > 0)
        {
            var now = DateTime.UtcNow;
            // ExecuteUpdateAsync bypasses the change tracker, so AuditFieldsInterceptor does not
            // run and ModifiedDate must be set explicitly (constitution III).
            await db.PushSubscriptions
                .Where(s => delivered.Contains(s.Id))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.LastSuccessAt, now)
                          .SetProperty(x => x.ModifiedDate, now),
                    ct);
        }
    }

    private static bool IsGone(HttpStatusCode status) =>
        status is HttpStatusCode.NotFound or HttpStatusCode.Gone;

    /// <summary>
    /// A push topic must be short and URL-safe, which a dedupe key is not guaranteed to be. Hashed
    /// to a fixed-length token so equal tags still collapse.
    /// </summary>
    private static string Topic(string tag)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tag));
        return Convert.ToBase64String(hash)[..22].Replace('+', '-').Replace('/', '_');
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private sealed record Device(Guid Id, string Endpoint, string P256dh, string Auth);
}
