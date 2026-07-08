using JuggerHub.Data;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuggerHub.Services.Notifications;

/// <summary>EF-Core-direct implementation of <see cref="INotificationPreferenceService"/> (feature 011).</summary>
public sealed class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationPreferenceService> _logger;

    public NotificationPreferenceService(AppDbContext db, ILogger<NotificationPreferenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>User-facing metadata for each togglable category — the single source shared by both layouts.</summary>
    private static readonly IReadOnlyList<(NotificationCategory Category, string Label, string Description)> CategoryMeta =
    [
        (NotificationCategory.InvitesAndRoster, "Invites & roster changes", "Team invites, people joining or leaving"),
        (NotificationCategory.TeamNews, "Team news", "News posted to your teams"),
    ];

    private static readonly IReadOnlyList<AlwaysOnGroupDto> AlwaysOnGroups =
    [
        new AlwaysOnGroupDto("Security & sign-in", "Verification, password, and login security"),
    ];

    public async Task<NotificationPreferenceMatrixDto> GetMatrixAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await _db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Category, p.Channel, p.Enabled })
            .ToListAsync(ct);

        // (category, channel) → set value; anything absent stays at the opt-out default (true).
        var set = rows.ToDictionary(r => (r.Category, r.Channel), r => r.Enabled);

        bool Effective(NotificationCategory c, NotificationChannel ch) =>
            !set.TryGetValue((c, ch), out var value) || value;

        var categories = CategoryMeta
            .Select(m => new PreferenceCategoryDto(
                m.Category,
                m.Label,
                m.Description,
                new PreferenceChannelsDto(
                    Effective(m.Category, NotificationChannel.InApp),
                    Effective(m.Category, NotificationChannel.Email))))
            .ToList();

        return new NotificationPreferenceMatrixDto(categories, AlwaysOnGroups);
    }

    public async Task SetCellAsync(
        Guid userId, NotificationCategory category, NotificationChannel channel, bool enabled, CancellationToken ct = default)
    {
        var existing = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Category == category && p.Channel == channel, ct);

        if (existing is not null)
        {
            existing.Enabled = enabled;
            await _db.SaveChangesAsync(ct);
            return;
        }

        _db.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = userId,
            Category = category,
            Channel = channel,
            Enabled = enabled,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent first-set for the same cell won the insert race — apply as an update.
            await _db.NotificationPreferences
                .Where(p => p.UserId == userId && p.Category == category && p.Channel == channel)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Enabled, enabled)
                    .SetProperty(p => p.ModifiedDate, DateTime.UtcNow), ct);
        }
    }

    public async Task<bool> IsEnabledAsync(
        Guid userId, NotificationCategory category, NotificationChannel channel, CancellationToken ct = default)
    {
        try
        {
            var row = await _db.NotificationPreferences.AsNoTracking()
                .Where(p => p.UserId == userId && p.Category == category && p.Channel == channel)
                .Select(p => (bool?)p.Enabled)
                .FirstOrDefaultAsync(ct);

            // Missing row ⇒ default on.
            return row ?? true;
        }
        catch (Exception ex)
        {
            // Fail-safe: deliver rather than silently drop (spec FR-009 / SC-005).
            _logger.LogWarning(ex, "Preference lookup failed for user {UserId} ({Category}/{Channel}); defaulting to enabled.",
                userId, category, channel);
            return true;
        }
    }

    public async Task<IReadOnlyCollection<Guid>> GetEnabledRecipientsAsync(
        IReadOnlyCollection<Guid> userIds, NotificationCategory category, NotificationChannel channel, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return userIds;
        }

        try
        {
            // Only explicit *disabled* rows exclude a recipient; defaults (no row) stay included.
            var disabled = await _db.NotificationPreferences.AsNoTracking()
                .Where(p => userIds.Contains(p.UserId) && p.Category == category && p.Channel == channel && !p.Enabled)
                .Select(p => p.UserId)
                .ToListAsync(ct);

            if (disabled.Count == 0)
            {
                return userIds;
            }

            var excluded = disabled.ToHashSet();
            return userIds.Where(id => !excluded.Contains(id)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Batch preference lookup failed ({Category}/{Channel}); defaulting to all recipients.",
                category, channel);
            return userIds;
        }
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
