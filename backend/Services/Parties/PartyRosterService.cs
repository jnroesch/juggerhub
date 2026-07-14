using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Parties;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Parties;

/// <summary>
/// EF-Core-direct implementation of <see cref="IPartyRosterService"/> (feature 016). Joining runs
/// under a pessimistic party-row lock so the roster cap can never be exceeded; the "no response"
/// group is derived from current team memberships. Removing/leaving never touches team membership.
/// </summary>
public sealed class PartyRosterService : IPartyRosterService
{
    private readonly AppDbContext _db;
    private readonly PartyGuard _guard;
    private readonly PartyCapacity _capacity;
    private readonly INotificationService _notifications;
    private readonly PartyEmailService _email;

    public PartyRosterService(
        AppDbContext db, PartyGuard guard, PartyCapacity capacity,
        INotificationService notifications, PartyEmailService email)
    {
        _db = db;
        _guard = guard;
        _capacity = capacity;
        _notifications = notifications;
        _email = email;
    }

    public async Task<PagedResult<PartyMemberDto>?> ListGroupAsync(
        Guid partyId, PartyRosterGroup group, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null || !access.Value.IsTeamMember)
        {
            return null;
        }

        var teamId = access.Value.TeamId;
        var teamMemberIds = _db.TeamMemberships.Where(tm => tm.TeamId == teamId).Select(tm => tm.UserId);

        if (group == PartyRosterGroup.NoResponse)
        {
            // Current team members with no party row.
            var q = _db.TeamMemberships.AsNoTracking()
                .Where(tm => tm.TeamId == teamId && !_db.PartyMembers.Any(m => m.PartyId == partyId && m.UserId == tm.UserId));
            var total = await q.CountAsync(ct);
            var items = await q
                .OrderBy(tm => tm.User.Profile!.DisplayName)
                .Skip(pagination.NormalizedSkip)
                .Take(pagination.NormalizedTake)
                .Select(tm => new PartyMemberDto(
                    tm.UserId, tm.User.Profile!.Handle, tm.User.Profile!.DisplayName, null, tm.UserId == actorUserId,
                    tm.User.Profile!.Pompfen.Select(p => p.Pompfe).ToList()))
                .ToListAsync(ct);
            return new PagedResult<PartyMemberDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
        }

        var status = group == PartyRosterGroup.In ? PartyMemberStatus.In : PartyMemberStatus.Declined;
        var query = _db.PartyMembers.AsNoTracking()
            .Where(m => m.PartyId == partyId && m.Status == status && teamMemberIds.Contains(m.UserId));
        var totalRows = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(m => m.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(m => new PartyMemberDto(
                m.UserId, m.User.Profile!.Handle, m.User.Profile!.DisplayName, m.Role, m.UserId == actorUserId,
                m.User.Profile!.Pompfen.Select(p => p.Pompfe).ToList()))
            .ToListAsync(ct);
        return new PagedResult<PartyMemberDto>(rows, totalRows, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<PartyResult<PartyMemberDto>> JoinAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<PartyMemberDto>.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsTeamMember)
        {
            return PartyResult<PartyMemberDto>.Fail(PartyOutcome.Forbidden, "Only a team member can join this party.");
        }

        if (!access.Value.IsEventOpen)
        {
            return PartyResult<PartyMemberDto>.Fail(PartyOutcome.Closed, "This event isn't accepting entries.");
        }

        var cap = await _db.Parties.Where(p => p.Id == partyId).Select(p => p.RosterCap).FirstAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _capacity.LockPartyRowAsync(partyId, ct);

        var mine = await _db.PartyMembers.FirstOrDefaultAsync(m => m.PartyId == partyId && m.UserId == actorUserId, ct);
        if (mine is { Status: PartyMemberStatus.In })
        {
            await tx.CommitAsync(ct);
            return PartyResult<PartyMemberDto>.Ok(await LoadMineAsync(partyId, actorUserId, ct));
        }

        var inCount = await _capacity.InCountAsync(partyId, ct);
        if (inCount >= cap)
        {
            return PartyResult<PartyMemberDto>.Fail(PartyOutcome.Full, "The party is full right now.");
        }

        if (mine is null)
        {
            _db.PartyMembers.Add(new PartyMember
            {
                PartyId = partyId,
                UserId = actorUserId,
                Status = PartyMemberStatus.In,
                Role = PartyMemberRole.Member,
            });
        }
        else
        {
            mine.Status = PartyMemberStatus.In;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return PartyResult<PartyMemberDto>.Ok(await LoadMineAsync(partyId, actorUserId, ct));
    }

    public async Task<PartyResult<PartyMemberDto>> DeclineAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<PartyMemberDto>.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsTeamMember)
        {
            return PartyResult<PartyMemberDto>.Fail(PartyOutcome.Forbidden, "Only a team member can answer this request.");
        }

        var mine = await _db.PartyMembers.FirstOrDefaultAsync(m => m.PartyId == partyId && m.UserId == actorUserId, ct);
        if (mine is { Role: PartyMemberRole.Admin } && await IsLastAdminAsync(partyId, ct))
        {
            return PartyResult<PartyMemberDto>.Fail(PartyOutcome.Conflict, "Assign another party admin before you step down.");
        }

        if (mine is null)
        {
            _db.PartyMembers.Add(new PartyMember
            {
                PartyId = partyId,
                UserId = actorUserId,
                Status = PartyMemberStatus.Declined,
                Role = PartyMemberRole.Member,
            });
        }
        else
        {
            mine.Status = PartyMemberStatus.Declined;
            mine.Role = PartyMemberRole.Member;
        }

        await _db.SaveChangesAsync(ct);
        return PartyResult<PartyMemberDto>.Ok(await LoadMineAsync(partyId, actorUserId, ct));
    }

    public async Task<PartyResult> LeaveAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsTeamMember)
        {
            return PartyResult.Fail(PartyOutcome.Forbidden, "Only a team member can leave this party.");
        }

        var mine = await _db.PartyMembers.FirstOrDefaultAsync(m => m.PartyId == partyId && m.UserId == actorUserId, ct);
        if (mine is null || mine.Status != PartyMemberStatus.In)
        {
            return PartyResult.Fail(PartyOutcome.Conflict, "You're not in this party.");
        }

        if (mine.Role == PartyMemberRole.Admin && await IsLastAdminAsync(partyId, ct))
        {
            return PartyResult.Fail(PartyOutcome.Conflict, "Assign another party admin before you leave.");
        }

        _db.PartyMembers.Remove(mine);
        await _db.SaveChangesAsync(ct);
        return PartyResult.Ok();
    }

    public async Task<PartyResult> RemoveAsync(Guid partyId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult.Fail(PartyOutcome.Forbidden, "Only a party admin can remove members.");
        }

        var target = await _db.PartyMembers.FirstOrDefaultAsync(m => m.PartyId == partyId && m.UserId == targetUserId, ct);
        if (target is null)
        {
            return PartyResult.Fail(PartyOutcome.NotFound, "That member isn't in the party.");
        }

        if (target.Role == PartyMemberRole.Admin && await IsLastAdminAsync(partyId, ct))
        {
            return PartyResult.Fail(PartyOutcome.Conflict, "You can't remove the last party admin.");
        }

        _db.PartyMembers.Remove(target);
        await _db.SaveChangesAsync(ct);
        return PartyResult.Ok();
    }

    public async Task<PartyResult> NudgeAsync(Guid partyId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult.Fail(PartyOutcome.Forbidden, "Only a party admin can nudge members.");
        }

        var isTeamMember = await _db.TeamMemberships.AnyAsync(tm => tm.TeamId == access.Value.TeamId && tm.UserId == targetUserId, ct);
        if (!isTeamMember)
        {
            return PartyResult.Fail(PartyOutcome.NotFound, "That user isn't on the team.");
        }

        if (await _db.PartyMembers.AnyAsync(m => m.PartyId == partyId && m.UserId == targetUserId, ct))
        {
            return PartyResult.Fail(PartyOutcome.Invalid, "That member has already answered.");
        }

        var info = await _db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId)
            .Select(p => new { p.EventId, EventName = p.Event.Name, TeamName = p.Team.Name, TeamSlug = p.Team.Slug })
            .FirstAsync(ct);
        var target = await _db.Users.AsNoTracking()
            .Where(u => u.Id == targetUserId)
            .Select(u => new { u.Email, Name = u.Profile!.DisplayName })
            .FirstAsync(ct);

        // Fresh dedupe (null) so the nudge always re-alerts.
        await _notifications.CreateAsync(
            targetUserId,
            NotificationType.PartyRequest,
            new { partyId, info.EventId, info.TeamSlug, info.EventName, info.TeamName },
            actorUserId,
            dedupeKey: null,
            ct);

        if (!string.IsNullOrEmpty(target.Email))
        {
            await _email.SendPartyRequestEmailAsync(target.Email, target.Name, info.TeamName, info.EventName, info.TeamSlug, info.EventId, ct);
        }

        return PartyResult.Ok();
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<bool> IsLastAdminAsync(Guid partyId, CancellationToken ct) =>
        await _db.PartyMembers.CountAsync(m => m.PartyId == partyId && m.Role == PartyMemberRole.Admin, ct) <= 1;

    private Task<PartyMemberDto> LoadMineAsync(Guid partyId, Guid userId, CancellationToken ct) =>
        _db.PartyMembers.AsNoTracking()
            .Where(m => m.PartyId == partyId && m.UserId == userId)
            .Select(m => new PartyMemberDto(m.UserId, m.User.Profile!.Handle, m.User.Profile!.DisplayName, m.Role, true,
                m.User.Profile!.Pompfen.Select(p => p.Pompfe).ToList()))
            .FirstAsync(ct);
}
