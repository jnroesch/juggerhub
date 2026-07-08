using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Teams;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JuggerHub.Services.Teams;

/// <summary>EF-Core-direct implementation of <see cref="ITeamJoinRequestService"/> (feature 009).</summary>
public sealed class TeamJoinRequestService : ITeamJoinRequestService
{
    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;

    public TeamJoinRequestService(AppDbContext db, TeamMembershipGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    public async Task<JoinRequestOutcome> RequestAsync(string slug, Guid userId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { } a)
        {
            return JoinRequestOutcome.TeamNotFound;
        }

        if (a.IsMember)
        {
            return JoinRequestOutcome.AlreadyMember;
        }

        var pending = await _db.TeamJoinRequests
            .AnyAsync(r => r.TeamId == a.TeamId && r.UserId == userId && r.Status == JoinRequestStatus.Pending, ct);
        if (pending)
        {
            return JoinRequestOutcome.AlreadyPending;
        }

        _db.TeamJoinRequests.Add(new TeamJoinRequest
        {
            TeamId = a.TeamId,
            UserId = userId,
            Status = JoinRequestStatus.Pending,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost the partial-unique race to a concurrent request by the same player.
            return JoinRequestOutcome.AlreadyPending;
        }

        return JoinRequestOutcome.Created;
    }

    public async Task<JoinQueueResult> ListPendingAsync(
        string slug, Guid adminUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, adminUserId, ct);
        if (access is not { } a)
        {
            return new JoinQueueResult(JoinQueueGate.NotFound, null);
        }

        if (!a.IsAdmin)
        {
            return new JoinQueueResult(JoinQueueGate.Forbidden, null);
        }

        var query = _db.TeamJoinRequests.AsNoTracking()
            .Where(r => r.TeamId == a.TeamId && r.Status == JoinRequestStatus.Pending);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.CreatedDate).ThenBy(r => r.Id) // arrival order, stable
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(r => new JoinRequestDto(
                r.Id,
                r.User.Profile!.Handle,
                r.User.Profile!.DisplayName,
                r.User.Profile!.Avatar != null,
                r.CreatedDate))
            .ToListAsync(ct);

        return new JoinQueueResult(JoinQueueGate.Ok,
            new PagedResult<JoinRequestDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    public async Task<JoinDecisionOutcome> ApproveAsync(string slug, Guid requestId, Guid adminUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, adminUserId, ct);
        if (access is not { } a)
        {
            return JoinDecisionOutcome.TeamNotFound;
        }

        if (!a.IsAdmin)
        {
            return JoinDecisionOutcome.Forbidden;
        }

        var request = await _db.TeamJoinRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TeamId == a.TeamId && r.Status == JoinRequestStatus.Pending, ct);
        if (request is null)
        {
            return JoinDecisionOutcome.RequestNotFound;
        }

        // Create the membership unless the player already joined (idempotent), then resolve the request.
        var alreadyMember = await _db.TeamMemberships
            .AnyAsync(m => m.TeamId == a.TeamId && m.UserId == request.UserId, ct);
        if (!alreadyMember)
        {
            _db.TeamMemberships.Add(new TeamMembership
            {
                TeamId = a.TeamId,
                UserId = request.UserId,
                Role = TeamRole.Member,
                JoinedDate = DateTime.UtcNow,
            });
        }

        request.Status = JoinRequestStatus.Approved;
        request.DecidedByUserId = adminUserId;
        request.DecidedDate = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent join created the membership first — the request is still approved.
            request.Status = JoinRequestStatus.Approved;
            await _db.SaveChangesAsync(ct);
        }

        return JoinDecisionOutcome.Done;
    }

    public async Task<JoinDecisionOutcome> DeclineAsync(string slug, Guid requestId, Guid adminUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, adminUserId, ct);
        if (access is not { } a)
        {
            return JoinDecisionOutcome.TeamNotFound;
        }

        if (!a.IsAdmin)
        {
            return JoinDecisionOutcome.Forbidden;
        }

        var request = await _db.TeamJoinRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TeamId == a.TeamId && r.Status == JoinRequestStatus.Pending, ct);
        if (request is null)
        {
            return JoinDecisionOutcome.RequestNotFound;
        }

        request.Status = JoinRequestStatus.Declined;
        request.DecidedByUserId = adminUserId;
        request.DecidedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return JoinDecisionOutcome.Done;
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
