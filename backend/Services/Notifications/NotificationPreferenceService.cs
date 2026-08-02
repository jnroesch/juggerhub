using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Entities;
using JuggerHub.Services.Localization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuggerHub.Services.Notifications;

/// <summary>EF-Core-direct implementation of <see cref="INotificationPreferenceService"/> (feature 011).</summary>
public sealed class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationPreferenceService> _logger;
    private readonly IRecipientCultureResolver _culture;

    public NotificationPreferenceService(
        AppDbContext db,
        ILogger<NotificationPreferenceService> logger,
        IRecipientCultureResolver culture)
    {
        _db = db;
        _logger = logger;
        _culture = culture;
    }

    /// <summary>The togglable categories, in display order. Copy is resolved per request language below.</summary>
    private static readonly IReadOnlyList<NotificationCategory> CategoryOrder =
    [
        NotificationCategory.InvitesAndRoster,
        NotificationCategory.TeamNews,
        NotificationCategory.Trainings,
        NotificationCategory.Events,
    ];

    /// <summary>
    /// User-facing category copy, localized by the caller's request language (feature 031). Labels are
    /// server-owned so both layouts render identical copy; English is the universal fallback. Backed by
    /// in-code per-culture dictionaries — the same low-risk approach as <see cref="Email.EmailLocalizer"/>.
    /// de/es are draft translations pending the native-review pass (#77).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<NotificationCategory, (string Label, string Description)>> CategoryCopy =
        new Dictionary<string, IReadOnlyDictionary<NotificationCategory, (string, string)>>
        {
            ["en"] = new Dictionary<NotificationCategory, (string, string)>
            {
                [NotificationCategory.InvitesAndRoster] = ("Invites & roster changes", "Team invites, people joining or leaving"),
                [NotificationCategory.TeamNews] = ("Team news", "News posted to your teams"),
                [NotificationCategory.Trainings] = ("Trainings", "New training sessions and schedule changes"),
                [NotificationCategory.Events] = ("Events", "Changes to events you signed up for"),
            },
            ["de"] = new Dictionary<NotificationCategory, (string, string)>
            {
                [NotificationCategory.InvitesAndRoster] = ("Einladungen & Kaderänderungen", "Team-Einladungen, Beitritte und Austritte"),
                [NotificationCategory.TeamNews] = ("Team-News", "Neuigkeiten, die in deinen Teams gepostet werden"),
                [NotificationCategory.Trainings] = ("Trainings", "Neue Trainingseinheiten und Terminänderungen"),
                [NotificationCategory.Events] = ("Veranstaltungen", "Änderungen an Events, für die du angemeldet bist"),
            },
            ["es"] = new Dictionary<NotificationCategory, (string, string)>
            {
                [NotificationCategory.InvitesAndRoster] = ("Invitaciones y cambios de plantilla", "Invitaciones de equipo, altas y bajas"),
                [NotificationCategory.TeamNews] = ("Noticias del equipo", "Novedades publicadas en tus equipos"),
                [NotificationCategory.Trainings] = ("Entrenamientos", "Nuevas sesiones de entrenamiento y cambios de horario"),
                [NotificationCategory.Events] = ("Eventos", "Cambios en los eventos a los que te apuntaste"),
            },
        };

    /// <summary>The always-on groups (no toggles), localized by request language. English is the fallback.</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<AlwaysOnGroupDto>> AlwaysOnCopy =
        new Dictionary<string, IReadOnlyList<AlwaysOnGroupDto>>
        {
            ["en"] = [new AlwaysOnGroupDto("Security & sign-in", "Verification, password, and login security")],
            ["de"] = [new AlwaysOnGroupDto("Sicherheit & Anmeldung", "Verifizierung, Passwort und Anmeldesicherheit")],
            ["es"] = [new AlwaysOnGroupDto("Seguridad e inicio de sesión", "Verificación, contraseña y seguridad de acceso")],
        };

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

        // The caller's effective language (frontend-stamped Accept-Language), English-fallback.
        var culture = _culture.ResolveFromRequest();
        var copy = CategoryCopy.GetValueOrDefault(culture, CategoryCopy[SupportedLanguages.Default]);
        var alwaysOn = AlwaysOnCopy.GetValueOrDefault(culture, AlwaysOnCopy[SupportedLanguages.Default]);

        var categories = CategoryOrder
            .Select(category =>
            {
                // Per-category English fallback (feature 039). This used to be a bare indexer, which
                // meant adding a category and forgetting one language's entry threw
                // KeyNotFoundException and took down the whole settings page for that language —
                // rather than degrading the way every other translation gap in this codebase does.
                var (label, description) = copy.TryGetValue(category, out var localized)
                    ? localized
                    : CategoryCopy[SupportedLanguages.Default][category];

                return new PreferenceCategoryDto(
                    category,
                    label,
                    description,
                    new PreferenceChannelsDto(
                        Effective(category, NotificationChannel.InApp),
                        Effective(category, NotificationChannel.Email)));
            })
            .ToList();

        return new NotificationPreferenceMatrixDto(categories, alwaysOn);
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
