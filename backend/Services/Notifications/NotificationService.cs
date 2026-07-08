using System.Text.Json;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Entities;
using JuggerHub.Services.Notifications.Realtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuggerHub.Services.Notifications;

/// <summary>EF-Core-direct implementation of <see cref="INotificationService"/> (feature 010).</summary>
public sealed class NotificationService : INotificationService
{
    // Payloads are stored camelCase so the client reads them without remapping.
    private static readonly JsonSerializerOptions PayloadJson =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never };

    private readonly AppDbContext _db;
    private readonly INotificationRealtime _realtime;
    private readonly INotificationPreferenceService _preferences;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext db,
        INotificationRealtime realtime,
        INotificationPreferenceService preferences,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _realtime = realtime;
        _preferences = preferences;
        _logger = logger;
    }

    // --- Create ---------------------------------------------------------------

    public async Task CreateAsync(
        Guid recipientUserId,
        NotificationType type,
        object payload,
        Guid? actorUserId = null,
        string? dedupeKey = null,
        CancellationToken ct = default)
    {
        // Honor the recipient's in-app preference for this category (feature 011): off ⇒ no row,
        // no unread bump. Fail-safe defaults to on inside the preference service.
        if (!await _preferences.IsEnabledAsync(recipientUserId, NotificationCategories.For(type), NotificationChannel.InApp, ct))
        {
            return;
        }

        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            Type = type,
            Payload = JsonSerializer.Serialize(payload, PayloadJson),
            ActorUserId = actorUserId,
            DedupeKey = dedupeKey,
        };

        _db.Notifications.Add(notification);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Duplicate for the same (recipient, dedupeKey) — the logical event is already
            // notified. Detach and no-op (idempotency, FR-017).
            _db.Entry(notification).State = EntityState.Detached;
            return;
        }

        await PushAsync(recipientUserId, notification, actorUserId, ct);
    }

    public async Task CreateManyAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        NotificationType type,
        object payload,
        Guid? actorUserId = null,
        string? dedupeKeyPrefix = null,
        CancellationToken ct = default)
    {
        var distinct = recipientUserIds.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        // Drop recipients who turned this category's in-app channel off (feature 011).
        var recipients = (await _preferences.GetEnabledRecipientsAsync(
            distinct, NotificationCategories.For(type), NotificationChannel.InApp, ct)).ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        var json = JsonSerializer.Serialize(payload, PayloadJson);
        var rows = recipients.Select(r => new Notification
        {
            RecipientUserId = r,
            Type = type,
            Payload = json,
            ActorUserId = actorUserId,
            DedupeKey = dedupeKeyPrefix is null ? null : $"{dedupeKeyPrefix}:{r}",
        }).ToList();

        _db.Notifications.AddRange(rows);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A rare partial-duplicate race on fan-out. The originating action already succeeded;
            // log and move on rather than fail it (FR-016).
            _logger.LogWarning(ex, "Duplicate notification(s) during fan-out for type {Type}; skipped.", type);
            return;
        }

        foreach (var row in rows)
        {
            await PushAsync(row.RecipientUserId, row, actorUserId, ct);
        }
    }

    // --- Read -----------------------------------------------------------------

    public async Task<PagedResult<NotificationDto>> ListAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var query = _db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == userId);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(n => n.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(n => new Row(
                n.Id,
                n.Type,
                n.CreatedDate,
                n.IsRead,
                n.ActorUserId != null && n.Actor!.Profile != null ? n.Actor.Profile.DisplayName : null,
                n.Payload))
            .ToListAsync(ct);

        // Resolve invite usability for the TeamInvite rows on this page in a single query, so an
        // invite accepted/declined/expired out-of-band renders as resolved (actions hidden).
        var inviteIds = rows
            .Where(r => r.Type == NotificationType.TeamInvite)
            .Select(r => TryGetInvitationId(r.Payload))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var usable = new HashSet<Guid>();
        if (inviteIds.Count > 0)
        {
            var now = DateTime.UtcNow;
            var found = await _db.TeamInvitations.AsNoTracking()
                .Where(i => inviteIds.Contains(i.Id) && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
                .Select(i => i.Id)
                .ToListAsync(ct);
            usable = found.ToHashSet();
        }

        var items = rows.Select(r => new NotificationDto(
            r.Id,
            r.Type,
            r.CreatedDate,
            r.IsRead,
            r.ActorDisplayName,
            Resolved: r.Type == NotificationType.TeamInvite
                && !(TryGetInvitationId(r.Payload) is Guid id && usable.Contains(id)),
            Payload: JsonSerializer.Deserialize<JsonElement>(r.Payload))).ToList();

        return new PagedResult<NotificationDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        _db.Notifications.AsNoTracking().CountAsync(n => n.RecipientUserId == userId && !n.IsRead, ct);

    // --- Mutate ---------------------------------------------------------------

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        // Ownership check first so a foreign id is a clean "not found" (no existence leak) and an
        // already-read own notification is still a success (idempotent).
        var owned = await _db.Notifications.AsNoTracking()
            .AnyAsync(n => n.Id == notificationId && n.RecipientUserId == userId, ct);
        if (!owned)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        await _db.Notifications
            .Where(n => n.Id == notificationId && n.RecipientUserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadDate, now)
                .SetProperty(n => n.ModifiedDate, now), ct);

        await PushUnreadCountAsync(userId, ct);
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var affected = await _db.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadDate, now)
                .SetProperty(n => n.ModifiedDate, now), ct);

        if (affected > 0)
        {
            await PushUnreadCountAsync(userId, ct);
        }

        return affected;
    }

    // --- Helpers --------------------------------------------------------------

    /// <summary>Best-effort realtime push; a socket failure never fails the create (FR-016).</summary>
    private async Task PushAsync(Guid recipientUserId, Notification notification, Guid? actorUserId, CancellationToken ct)
    {
        try
        {
            string? actorName = null;
            if (actorUserId is Guid actorId)
            {
                actorName = await _db.PlayerProfiles.AsNoTracking()
                    .Where(p => p.UserId == actorId)
                    .Select(p => p.DisplayName)
                    .FirstOrDefaultAsync(ct);
            }

            // A freshly created invite is always usable, so Resolved is false for every new row.
            var dto = new NotificationDto(
                notification.Id,
                notification.Type,
                notification.CreatedDate,
                notification.IsRead,
                actorName,
                Resolved: false,
                JsonSerializer.Deserialize<JsonElement>(notification.Payload));

            await _realtime.PushCreatedAsync(recipientUserId, dto, ct);
            await PushUnreadCountAsync(recipientUserId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Realtime push failed for notification {Id}; stored durably.", notification.Id);
        }
    }

    private async Task PushUnreadCountAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var count = await CountUnreadAsync(userId, ct);
            await _realtime.PushUnreadCountAsync(userId, count, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Realtime unread-count push failed for user {UserId}.", userId);
        }
    }

    private static Guid? TryGetInvitationId(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("invitationId", out var prop) && prop.TryGetGuid(out var id))
            {
                return id;
            }
        }
        catch (JsonException)
        {
            // Malformed payload — treat as no invite id.
        }

        return null;
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>Flat read projection; keeps the payload as a string until we shape the DTO.</summary>
    private sealed record Row(
        Guid Id,
        NotificationType Type,
        DateTime CreatedDate,
        bool IsRead,
        string? ActorDisplayName,
        string Payload);
}
