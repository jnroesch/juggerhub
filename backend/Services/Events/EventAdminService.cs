using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Events;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Events;

/// <summary>
/// EF-Core-direct implementation of <see cref="IEventAdminService"/>. Admin removal/step-down runs
/// inside a transaction that locks the event row so the admin-count check + delete is atomic — the
/// event can never be driven below one admin (mirrors the team last-admin guard).
/// </summary>
public sealed class EventAdminService : IEventAdminService
{
    private readonly AppDbContext _db;
    private readonly EventAdminGuard _guard;

    public EventAdminService(AppDbContext db, EventAdminGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    public async Task<AdminListResult> ListAsync(Guid eventId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return new AdminListResult(EventAdminGate.NotFound, null);
        }

        if (!access.Value.IsAdmin)
        {
            return new AdminListResult(EventAdminGate.Forbidden, null);
        }

        var query = _db.EventAdmins.AsNoTracking().Where(a => a.EventId == eventId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(a => a.AddedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(a => new EventAdminDto(a.UserId, a.User.Profile!.Handle, a.User.Profile!.DisplayName))
            .ToListAsync(ct);

        return new AdminListResult(EventAdminGate.Ok,
            new PagedResult<EventAdminDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    public async Task<AdminOpStatus> RemoveAsync(Guid eventId, Guid actorUserId, Guid targetUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return AdminOpStatus.NotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return AdminOpStatus.Forbidden;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Serialize admin mutations for this event on the event row.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Events\" WHERE \"Id\" = {eventId} FOR UPDATE", ct);

        var target = await _db.EventAdmins
            .FirstOrDefaultAsync(a => a.EventId == eventId && a.UserId == targetUserId, ct);
        if (target is null)
        {
            return AdminOpStatus.AdminNotFound;
        }

        var adminCount = await _db.EventAdmins.CountAsync(a => a.EventId == eventId, ct);
        if (adminCount <= 1)
        {
            return AdminOpStatus.LastAdmin;
        }

        _db.EventAdmins.Remove(target);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return AdminOpStatus.Ok;
    }
}
