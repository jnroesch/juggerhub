using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Localization;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Notifications.Push;

/// <summary>
/// Composes one notification per recipient language and hands it to the dispatcher (feature 055).
/// </summary>
public sealed class PushFanOut : IPushFanOut
{
    private readonly AppDbContext _db;
    private readonly IPushContentComposer _composer;
    private readonly IPushDispatcher _dispatcher;
    private readonly IRecipientCultureResolver _culture;
    private readonly ILogger<PushFanOut> _logger;

    public PushFanOut(
        AppDbContext db,
        IPushContentComposer composer,
        IPushDispatcher dispatcher,
        IRecipientCultureResolver culture,
        ILogger<PushFanOut> logger)
    {
        _db = db;
        _composer = composer;
        _dispatcher = dispatcher;
        _culture = culture;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task FanOutAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        NotificationType type,
        string payloadJson,
        string? dedupeKey,
        CancellationToken ct = default)
    {
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        try
        {
            // The tag is what makes a repeat replace rather than stack. When a producer supplies no
            // dedupe key there is still a stable identity to use: the type plus this payload.
            var tag = dedupeKey ?? $"{type}:{payloadJson.GetHashCode():x8}";

            // Group by language, not by recipient: the sentence is identical for everyone who
            // reads the same one, so it is composed once per language rather than once per person.
            // The language is the RECIPIENT's stored preference — the actor's is irrelevant to them.
            var recipients = await _db.Users.AsNoTracking()
                .Where(u => recipientUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.PreferredLanguage })
                .ToListAsync(ct);

            foreach (var group in recipients.GroupBy(r => SupportedLanguages.ResolveOrDefault(r.PreferredLanguage)))
            {
                var content = _composer.Compose(type, payloadJson, group.Key, tag);
                await _dispatcher.DispatchAsync(group.Select(r => r.Id).ToList(), content, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // FR-013: the action that produced this notification must succeed regardless. The same
            // shape TeamNewsService already uses around its email fan-out.
            _logger.LogWarning(ex, "Failed to fan out push notifications for type {Type}.", type);
        }
    }
}
