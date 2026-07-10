using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Admin;
using JuggerHub.Entities;
using JuggerHub.Security.PlatformAdmin;
using JuggerHub.Services.Auth;
using JuggerHub.Services.Email;
using JuggerHub.Services.Search;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Admin;

/// <summary>
/// Admin user management (feature 013 US3/US4). Every read here opts OUT of the
/// global ban filter (<c>IgnoreQueryFilters</c>) on purpose: the admin area is the
/// one place banned (soft-deleted) players remain visible, otherwise a mistaken ban
/// could never be found and undone. Account actions validate the state machine,
/// shield platform admins (FR-019), write an <see cref="AdminActionRecord"/> in the
/// same save as the state change, and kill sessions by revoking refresh tokens
/// (access tokens then die within their configured lifetime — the spec's
/// "session-refresh window").
/// </summary>
public sealed class AdminUserService : IAdminUserService
{
    private const int ActivityCap = 5;

    private readonly AppDbContext _db;
    private readonly UserManager<User> _users;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly AuthEmailService _authEmail;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        AppDbContext db,
        UserManager<User> users,
        IRefreshTokenService refreshTokens,
        AuthEmailService authEmail,
        ILogger<AdminUserService> logger)
    {
        _db = db;
        _users = users;
        _refreshTokens = refreshTokens;
        _authEmail = authEmail;
        _logger = logger;
    }

    public async Task<PagedResult<AdminUserListItemDto>> SearchAsync(
        string? q, AccountStatus? status, PaginationRequest pagination, CancellationToken ct = default)
    {
        var query = _db.PlayerProfiles.IgnoreQueryFilters().AsNoTracking();

        if (status is not null)
        {
            query = query.Where(p => p.User.Status == status);
        }

        var term = SearchQuery.Normalize(q, minLength: 1);
        if (term is not null)
        {
            // "@ada" should hit the handle "ada"; names/teams match the raw term.
            var pattern = SearchQuery.ContainsPattern(term);
            var handlePattern = SearchQuery.ContainsPattern(term.TrimStart('@'));
            query = query.Where(p =>
                EF.Functions.ILike(AppDbContext.Unaccent(p.DisplayName), AppDbContext.Unaccent(pattern))
                || EF.Functions.ILike(p.Handle, handlePattern)
                || _db.TeamMemberships.Any(m => m.UserId == p.UserId
                    && EF.Functions.ILike(AppDbContext.Unaccent(m.Team.Name), AppDbContext.Unaccent(pattern))));
        }

        var adminRoleId = await AdminRoleIdAsync(ct);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.DisplayName)
            .ThenBy(p => p.Handle)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new AdminUserListItemDto(
                p.Handle,
                p.DisplayName,
                p.User.Status,
                adminRoleId != null && _db.UserRoles.Any(ur => ur.UserId == p.UserId && ur.RoleId == adminRoleId),
                _db.TeamMemberships.Where(m => m.UserId == p.UserId)
                    .OrderBy(m => m.Team.Name).Select(m => m.Team.Name).ToList(),
                _db.BadgeAwards.Count(a => a.PlayerProfileId == p.Id && a.Status == AwardStatus.Active)
                    + _db.AchievementAwards.Count(a => a.PlayerProfileId == p.Id && a.Status == AwardStatus.Active),
                p.CreatedDate))
            .ToListAsync(ct);

        return new PagedResult<AdminUserListItemDto>(
            items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<AdminUserDetailDto?> GetDetailAsync(string handle, CancellationToken ct = default)
    {
        var normalized = NormalizeHandle(handle);
        var adminRoleId = await AdminRoleIdAsync(ct);

        return await _db.PlayerProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Handle == normalized)
            .Select(p => new AdminUserDetailDto(
                p.UserId,
                p.Handle,
                p.DisplayName,
                p.Hometown,
                p.CreatedDate,
                p.User.Status,
                p.User.StatusChangedAt,
                adminRoleId != null && _db.UserRoles.Any(ur => ur.UserId == p.UserId && ur.RoleId == adminRoleId),
                _db.TeamMemberships.Where(m => m.UserId == p.UserId)
                    .OrderBy(m => m.Team.Name)
                    .Select(m => new AdminUserTeamDto(m.Team.Name, m.Team.Slug)).ToList(),
                p.Pompfen.OrderBy(pp => pp.Pompfe).Select(pp => pp.Pompfe).ToList(),
                p.Participations.Max(ep => (DateTime?)ep.CreatedDate),
                p.Participations.OrderByDescending(ep => ep.Event.StartsAt)
                    .Take(ActivityCap)
                    .Select(ep => new AdminActivityItemDto(ep.Event.Name, ep.Event.StartsAt)).ToList()))
            .FirstOrDefaultAsync(ct);
    }

    public Task<AdminUserActionOutcome> SuspendAsync(Guid actorId, string handle, CancellationToken ct = default) =>
        TransitionAsync(actorId, handle, AdminAccountAction.Suspend, shielded: true,
            from: [AccountStatus.Active], to: AccountStatus.Suspended, revokeSessions: true, ct);

    public Task<AdminUserActionOutcome> ReinstateAsync(Guid actorId, string handle, CancellationToken ct = default) =>
        TransitionAsync(actorId, handle, AdminAccountAction.Reinstate, shielded: false,
            from: [AccountStatus.Suspended], to: AccountStatus.Active, revokeSessions: false, ct);

    public Task<AdminUserActionOutcome> BanAsync(Guid actorId, string handle, CancellationToken ct = default) =>
        TransitionAsync(actorId, handle, AdminAccountAction.Ban, shielded: true,
            from: [AccountStatus.Active, AccountStatus.Suspended], to: AccountStatus.Banned, revokeSessions: true, ct);

    public Task<AdminUserActionOutcome> UnbanAsync(Guid actorId, string handle, CancellationToken ct = default) =>
        TransitionAsync(actorId, handle, AdminAccountAction.Unban, shielded: false,
            from: [AccountStatus.Banned], to: AccountStatus.Active, revokeSessions: false, ct);

    public async Task<AdminUserActionOutcome> SendPasswordResetAsync(
        Guid actorId, string handle, CancellationToken ct = default)
    {
        var user = await FindUserAsync(handle, ct);
        if (user is null)
        {
            return AdminUserActionOutcome.NotFound;
        }

        try
        {
            // The platform's standard reset flow, triggered for the target. The admin
            // never sees the token, the link, or any credential.
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            await _authEmail.SendPasswordResetEmailAsync(user, token, ct);
        }
        catch (Exception ex)
        {
            // Same neutrality as the self-service forgot flow: delivery problems are
            // logged, never surfaced as an oracle.
            _logger.LogError(ex, "Failed to send admin-triggered password reset email");
        }

        _db.AdminActionRecords.Add(new AdminActionRecord
        {
            ActorUserId = actorId,
            TargetUserId = user.Id,
            Action = AdminAccountAction.PasswordResetSent,
        });
        await _db.SaveChangesAsync(ct);

        return AdminUserActionOutcome.Done;
    }

    private async Task<AdminUserActionOutcome> TransitionAsync(
        Guid actorId,
        string handle,
        AdminAccountAction action,
        bool shielded,
        AccountStatus[] from,
        AccountStatus to,
        bool revokeSessions,
        CancellationToken ct)
    {
        var user = await FindUserAsync(handle, ct);
        if (user is null)
        {
            return AdminUserActionOutcome.NotFound;
        }

        // FR-019: suspend/ban never applies to a designated admin — and the caller is
        // always one, so self-action is inherently covered (checked explicitly anyway).
        if (shielded && (user.Id == actorId
            || await _users.IsInRoleAsync(user, PlatformAdminRoleSync.RoleName)))
        {
            return AdminUserActionOutcome.ProtectedAdmin;
        }

        if (!from.Contains(user.Status))
        {
            return AdminUserActionOutcome.InvalidTransition;
        }

        user.Status = to;
        user.StatusChangedAt = DateTime.UtcNow;
        _db.AdminActionRecords.Add(new AdminActionRecord
        {
            ActorUserId = actorId,
            TargetUserId = user.Id,
            Action = action,
        });
        // State change + record are one atomic save (FR-017).
        await _db.SaveChangesAsync(ct);

        if (revokeSessions)
        {
            // New sign-ins/refreshes are already refused by status; this ends live
            // sessions within the access token's lifetime.
            await _refreshTokens.RevokeAllForUserAsync(user.Id, action == AdminAccountAction.Ban ? "banned" : "suspended", ct);
        }

        return AdminUserActionOutcome.Done;
    }

    /// <summary>Tracked lookup by handle across ALL account states (filters off).</summary>
    private Task<User?> FindUserAsync(string handle, CancellationToken ct)
    {
        var normalized = NormalizeHandle(handle);
        return _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Profile!.Handle == normalized, ct);
    }

    private Task<Guid?> AdminRoleIdAsync(CancellationToken ct) =>
        _db.Roles.AsNoTracking()
            .Where(r => r.Name == PlatformAdminRoleSync.RoleName)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

    private static string NormalizeHandle(string handle) =>
        handle.Trim().TrimStart('@').ToLowerInvariant();
}
