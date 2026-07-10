using JuggerHub.Data;
using JuggerHub.Dtos.Admin;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Admin;

/// <summary>
/// Computes the admin landing aggregate (feature 013 US2). Everything is derived —
/// nothing is stored. Counts deliberately keep the global ban filter ON for the
/// players count (banned accounts are "removed" and don't count), while the
/// suspended count reads account state directly off the Identity users table
/// (which no filter touches).
/// </summary>
public sealed class AdminOverviewService : IAdminOverviewService
{
    private const int ListCap = 5;
    private const string BadgeKind = "Badge";
    private const string AchievementKind = "Achievement";

    private readonly AppDbContext _db;

    public AdminOverviewService(AppDbContext db) => _db = db;

    public async Task<AdminOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddDays(-30);

        var players = await _db.PlayerProfiles.AsNoTracking().CountAsync(ct);
        var teams = await _db.Teams.AsNoTracking().CountAsync(ct);
        var eventsLast30Days = await _db.Events.AsNoTracking()
            .CountAsync(e => e.Status != EventStatus.Cancelled && e.StartsAt >= monthAgo && e.StartsAt <= now, ct);
        var suspended = await _db.Users.AsNoTracking()
            .CountAsync(u => u.Status == AccountStatus.Suspended, ct);

        var newPlayers = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.CreatedDate >= weekAgo)
            .OrderByDescending(p => p.CreatedDate)
            .Take(ListCap)
            .Select(p => new AdminNewPlayerDto(p.Handle, p.DisplayName, p.Hometown, p.CreatedDate))
            .ToListAsync(ct);

        // Newest grants across both families; merged in memory (two capped queries).
        var badgeGrants = await _db.BadgeAwards.AsNoTracking()
            .Where(a => a.Status == AwardStatus.Active)
            .OrderByDescending(a => a.CreatedDate)
            .Take(ListCap)
            .Select(a => new AdminRecentGrantDto(
                BadgeKind,
                a.Definition.Name,
                a.PlayerProfile != null ? a.PlayerProfile.Handle : null,
                a.PlayerProfile != null ? a.PlayerProfile.DisplayName : (a.Team != null ? a.Team.Name : "—"),
                _db.PlayerProfiles.Where(p => p.UserId == a.GrantedByUserId).Select(p => p.DisplayName).FirstOrDefault() ?? "—",
                a.CreatedDate))
            .ToListAsync(ct);

        var achievementGrants = await _db.AchievementAwards.AsNoTracking()
            .Where(a => a.Status == AwardStatus.Active)
            .OrderByDescending(a => a.CreatedDate)
            .Take(ListCap)
            .Select(a => new AdminRecentGrantDto(
                AchievementKind,
                a.Definition.Name,
                a.PlayerProfile != null ? a.PlayerProfile.Handle : null,
                a.PlayerProfile != null ? a.PlayerProfile.DisplayName : (a.Team != null ? a.Team.Name : "—"),
                _db.PlayerProfiles.Where(p => p.UserId == a.GrantedByUserId).Select(p => p.DisplayName).FirstOrDefault() ?? "—",
                a.CreatedDate))
            .ToListAsync(ct);

        var recentGrants = badgeGrants.Concat(achievementGrants)
            .OrderByDescending(g => g.GrantedAt)
            .Take(ListCap)
            .ToList();

        return new AdminOverviewDto(players, teams, eventsLast30Days, suspended, newPlayers, recentGrants);
    }
}
