using System.Security.Cryptography;
using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Parties;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using InviteState = JuggerHub.Dtos.Teams.InviteState;
using UserRelation = JuggerHub.Dtos.Teams.UserRelation;

namespace JuggerHub.Services.Parties;

/// <summary>
/// EF-Core-direct implementation of <see cref="IPartyInvitationService"/> (feature 016), mirroring
/// the event invitation slice. Co-admins are restricted to members of the party's team; accepting
/// grants <see cref="PartyMemberRole.Admin"/> on the invitee's <see cref="PartyMember"/> row.
/// </summary>
public sealed class PartyInvitationService : IPartyInvitationService
{
    private readonly AppDbContext _db;
    private readonly PartyGuard _guard;
    private readonly PartyCapacity _capacity;
    private readonly PartyEmailService _email;
    private readonly EventOptions _eventOptions;
    private readonly EmailOptions _emailOptions;

    public PartyInvitationService(
        AppDbContext db, PartyGuard guard, PartyCapacity capacity, PartyEmailService email,
        IOptions<EventOptions> eventOptions, IOptions<EmailOptions> emailOptions)
    {
        _db = db;
        _guard = guard;
        _capacity = capacity;
        _email = email;
        _eventOptions = eventOptions.Value;
        _emailOptions = emailOptions.Value;
    }

    // --- Link -----------------------------------------------------------------

    public async Task<PartyResult<PartyInviteLinkDto?>> GetActiveLinkAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(partyId, actorUserId, ct);
        if (gate.Outcome != PartyOutcome.Ok)
        {
            return PartyResult<PartyInviteLinkDto?>.Fail(gate.Outcome, gate.Error);
        }

        var now = DateTime.UtcNow;
        var link = await _db.PartyAdminInvitations.AsNoTracking()
            .Where(i => i.PartyId == partyId && i.Kind == InvitationKind.Link
                && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
            .Select(i => new { i.Token, i.ExpiresDate })
            .FirstOrDefaultAsync(ct);

        if (link is null)
        {
            return PartyResult<PartyInviteLinkDto?>.Ok(null);
        }

        var url = PartyEmailService.BuildInviteLink(_emailOptions.FrontendBaseUrl, link.Token);
        return PartyResult<PartyInviteLinkDto?>.Ok(new PartyInviteLinkDto(url, link.Token, link.ExpiresDate));
    }

    public async Task<PartyResult<PartyInviteLinkDto>> CreateOrRotateLinkAsync(Guid partyId, Guid actorUserId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(partyId, actorUserId, ct);
        if (gate.Outcome != PartyOutcome.Ok)
        {
            return PartyResult<PartyInviteLinkDto>.Fail(gate.Outcome, gate.Error);
        }

        var now = DateTime.UtcNow;
        await _db.PartyAdminInvitations
            .Where(i => i.PartyId == partyId && i.Kind == InvitationKind.Link && i.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, InvitationStatus.Revoked)
                .SetProperty(i => i.ModifiedDate, now), ct);

        var invite = new PartyAdminInvitation
        {
            PartyId = partyId,
            Kind = InvitationKind.Link,
            Token = NewToken(),
            Status = InvitationStatus.Pending,
            ExpiresDate = now.AddDays(_eventOptions.InviteLinkTtlDays),
            CreatedByUserId = actorUserId,
            TargetUserId = null,
        };
        _db.PartyAdminInvitations.Add(invite);
        await _db.SaveChangesAsync(ct);

        var url = PartyEmailService.BuildInviteLink(_emailOptions.FrontendBaseUrl, invite.Token);
        return PartyResult<PartyInviteLinkDto>.Ok(new PartyInviteLinkDto(url, invite.Token, invite.ExpiresDate));
    }

    public async Task<PartyResult> RevokeAsync(Guid partyId, Guid actorUserId, Guid invitationId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(partyId, actorUserId, ct);
        if (gate.Outcome != PartyOutcome.Ok)
        {
            return PartyResult.Fail(gate.Outcome, gate.Error);
        }

        var affected = await _db.PartyAdminInvitations
            .Where(i => i.Id == invitationId && i.PartyId == partyId && i.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, InvitationStatus.Revoked)
                .SetProperty(i => i.ModifiedDate, DateTime.UtcNow), ct);

        return affected > 0 ? PartyResult.Ok() : PartyResult.Fail(PartyOutcome.NotFound, "No pending invitation with that id.");
    }

    // --- Targeted -------------------------------------------------------------

    public async Task<PartyResult<PartyInvitationDto>> CreateTargetedAsync(Guid partyId, Guid actorUserId, Guid targetUserId, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(partyId, actorUserId, ct);
        if (gate.Outcome != PartyOutcome.Ok)
        {
            return PartyResult<PartyInvitationDto>.Fail(gate.Outcome, gate.Error);
        }

        var teamId = gate.TeamId;
        var info = await _db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId)
            .Select(p => new { EventName = p.Event.Name, TeamName = p.Team.Name })
            .FirstAsync(ct);

        var target = await _db.Users.AsNoTracking()
            .Where(u => u.Id == targetUserId)
            .Select(u => new { u.Email, DisplayName = u.Profile!.DisplayName })
            .FirstOrDefaultAsync(ct);
        if (target is null || string.IsNullOrEmpty(target.Email))
        {
            return PartyResult<PartyInvitationDto>.Fail(PartyOutcome.NotFound, "No such user.");
        }

        if (!await _db.TeamMemberships.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == targetUserId, ct))
        {
            return PartyResult<PartyInvitationDto>.Fail(PartyOutcome.Invalid, "Co-admins must be members of the party's team.");
        }

        if (await _db.PartyMembers.AnyAsync(m => m.PartyId == partyId && m.UserId == targetUserId && m.Role == PartyMemberRole.Admin, ct))
        {
            return PartyResult<PartyInvitationDto>.Fail(PartyOutcome.Conflict, "That member already co-runs this party.");
        }

        var now = DateTime.UtcNow;
        var existing = await _db.PartyAdminInvitations.AsNoTracking()
            .Where(i => i.PartyId == partyId && i.Kind == InvitationKind.Targeted
                && i.TargetUserId == targetUserId && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
            .Select(i => new { i.Id, i.CreatedDate, i.ExpiresDate })
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return PartyResult<PartyInvitationDto>.Ok(new PartyInvitationDto(
                existing.Id, InvitationKind.Targeted, target.DisplayName, existing.CreatedDate, existing.ExpiresDate, InvitationStatus.Pending));
        }

        var inviterName = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == actorUserId).Select(p => p.DisplayName).FirstOrDefaultAsync(ct) ?? "A team admin";

        var invite = new PartyAdminInvitation
        {
            PartyId = partyId,
            Kind = InvitationKind.Targeted,
            Token = NewToken(),
            Status = InvitationStatus.Pending,
            ExpiresDate = now.AddDays(_eventOptions.InviteLinkTtlDays),
            CreatedByUserId = actorUserId,
            TargetUserId = targetUserId,
        };
        _db.PartyAdminInvitations.Add(invite);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return PartyResult<PartyInvitationDto>.Fail(PartyOutcome.Conflict, "That member has already been invited.");
        }

        await _email.SendCoAdminInviteEmailAsync(
            target.Email!, target.DisplayName, info.TeamName, info.EventName, inviterName, invite.Token, invite.ExpiresDate, ct);

        return PartyResult<PartyInvitationDto>.Ok(new PartyInvitationDto(
            invite.Id, InvitationKind.Targeted, target.DisplayName, invite.CreatedDate, invite.ExpiresDate, InvitationStatus.Pending));
    }

    public async Task<PartyResult<PagedResult<PartyInvitationDto>>> ListPendingAsync(Guid partyId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(partyId, actorUserId, ct);
        if (gate.Outcome != PartyOutcome.Ok)
        {
            return PartyResult<PagedResult<PartyInvitationDto>>.Fail(gate.Outcome, gate.Error);
        }

        var now = DateTime.UtcNow;
        var query = _db.PartyAdminInvitations.AsNoTracking()
            .Where(i => i.PartyId == partyId && i.Status == InvitationStatus.Pending && i.ExpiresDate > now);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(i => new PartyInvitationDto(
                i.Id,
                i.Kind,
                i.TargetUser != null ? i.TargetUser.Profile!.DisplayName : null,
                i.CreatedDate,
                i.ExpiresDate,
                i.Status))
            .ToListAsync(ct);

        return PartyResult<PagedResult<PartyInvitationDto>>.Ok(
            new PagedResult<PartyInvitationDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    public async Task<PartyResult<PagedResult<PartyInvitableUserDto>>> SearchMembersAsync(Guid partyId, Guid actorUserId, string query, PaginationRequest pagination, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(partyId, actorUserId, ct);
        if (gate.Outcome != PartyOutcome.Ok)
        {
            return PartyResult<PagedResult<PartyInvitableUserDto>>.Fail(gate.Outcome, gate.Error);
        }

        var teamId = gate.TeamId;
        var term = (query ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            return PartyResult<PagedResult<PartyInvitableUserDto>>.Ok(
                new PagedResult<PartyInvitableUserDto>([], 0, pagination.NormalizedSkip, pagination.NormalizedTake));
        }

        var now = DateTime.UtcNow;
        var pattern = $"%{term}%";
        // Scope to the party's team members only.
        var candidates = _db.TeamMemberships.AsNoTracking()
            .Where(tm => tm.TeamId == teamId)
            .Select(tm => tm.User.Profile!)
            .Where(p => EF.Functions.ILike(p.DisplayName, pattern) || EF.Functions.ILike(p.Handle, pattern));

        var total = await candidates.CountAsync(ct);
        var items = await candidates
            .OrderBy(p => p.DisplayName)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new PartyInvitableUserDto(
                p.UserId,
                p.Handle,
                p.DisplayName,
                p.Hometown,
                _db.PartyMembers.Any(m => m.PartyId == partyId && m.UserId == p.UserId && m.Role == PartyMemberRole.Admin)
                    ? UserRelation.Member // already a party admin
                    : _db.PartyAdminInvitations.Any(i => i.PartyId == partyId && i.Kind == InvitationKind.Targeted
                        && i.TargetUserId == p.UserId && i.Status == InvitationStatus.Pending && i.ExpiresDate > now)
                        ? UserRelation.Invited
                        : UserRelation.Invitable))
            .ToListAsync(ct);

        return PartyResult<PagedResult<PartyInvitableUserDto>>.Ok(
            new PagedResult<PartyInvitableUserDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    // --- Invitee token flow ---------------------------------------------------

    public async Task<PartyInvitePreviewDto?> GetPreviewAsync(string token, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var invite = await _db.PartyAdminInvitations.AsNoTracking()
            .Where(i => i.Token == token)
            .Select(i => new
            {
                i.Status,
                i.ExpiresDate,
                PartyId = i.Party.Id,
                TeamName = i.Party.Team.Name,
                EventName = i.Party.Event.Name,
                i.Party.Event.StartsAt,
                InviterName = i.CreatedBy.Profile != null ? i.CreatedBy.Profile.DisplayName : "A team admin",
            })
            .FirstOrDefaultAsync(ct);

        if (invite is null)
        {
            return null;
        }

        var state = invite.Status == InvitationStatus.Pending && invite.ExpiresDate > now
            ? InviteState.Usable
            : invite.Status == InvitationStatus.Pending
                ? InviteState.Expired
                : InviteState.Invalid;

        return new PartyInvitePreviewDto(invite.PartyId, invite.TeamName, invite.EventName, invite.StartsAt, invite.InviterName, state);
    }

    public async Task<PartyResult<Guid>> AcceptAsync(string token, Guid userId, CancellationToken ct = default)
    {
        // Connection resiliency (feature 028): the invitation is re-read INSIDE the retriable unit.
        // It is a tracked entity that gets mutated here, so loading it outside would leave a replay
        // holding state the previous attempt already changed — and clearing the tracker per attempt
        // would silently discard those writes.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();

            var now = DateTime.UtcNow;
            var invite = await _db.PartyAdminInvitations.FirstOrDefaultAsync(i => i.Token == token, ct);
            if (invite is null)
            {
                return PartyResult<Guid>.Fail(PartyOutcome.NotFound);
            }

            var partyId = invite.PartyId;
            if (!(invite.Status == InvitationStatus.Pending && invite.ExpiresDate > now))
            {
                return PartyResult<Guid>.Fail(PartyOutcome.Closed, "This invitation is no longer usable.");
            }

            var teamId = await _db.Parties.AsNoTracking().Where(p => p.Id == partyId).Select(p => new { p.TeamId, p.RosterCap }).FirstAsync(ct);
            if (!await _db.TeamMemberships.AnyAsync(tm => tm.TeamId == teamId.TeamId && tm.UserId == userId, ct))
            {
                return PartyResult<Guid>.Fail(PartyOutcome.Forbidden, "Only a member of the party's team can co-run it.");
            }

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await _capacity.LockPartyRowAsync(partyId, ct);

            var mine = await _db.PartyMembers.FirstOrDefaultAsync(m => m.PartyId == partyId && m.UserId == userId, ct);
            if (mine is { Role: PartyMemberRole.Admin })
            {
                if (invite.Kind == InvitationKind.Targeted)
                {
                    invite.Status = InvitationStatus.Accepted;
                    await _db.SaveChangesAsync(ct);
                }

                await tx.CommitAsync(ct);
                return PartyResult<Guid>.Ok(partyId);
            }

            if (mine is null || mine.Status != PartyMemberStatus.In)
            {
                var inCount = await _capacity.InCountAsync(partyId, ct);
                if (inCount >= teamId.RosterCap)
                {
                    return PartyResult<Guid>.Fail(PartyOutcome.Full, "The party is full — free a spot before seating another admin.");
                }
            }

            if (mine is null)
            {
                _db.PartyMembers.Add(new PartyMember
                {
                    PartyId = partyId,
                    UserId = userId,
                    Status = PartyMemberStatus.In,
                    Role = PartyMemberRole.Admin,
                });
            }
            else
            {
                mine.Status = PartyMemberStatus.In;
                mine.Role = PartyMemberRole.Admin;
            }

            if (invite.Kind == InvitationKind.Targeted)
            {
                invite.Status = InvitationStatus.Accepted;
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return PartyResult<Guid>.Ok(partyId);
        });
    }

    public async Task<PartyResult> DeclineAsync(string token, Guid userId, CancellationToken ct = default)
    {
        var invite = await _db.PartyAdminInvitations.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null)
        {
            return PartyResult.Fail(PartyOutcome.NotFound);
        }

        if (invite.Kind == InvitationKind.Targeted && invite.Status == InvitationStatus.Pending)
        {
            invite.Status = InvitationStatus.Declined;
            await _db.SaveChangesAsync(ct);
        }

        return PartyResult.Ok();
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<(PartyOutcome Outcome, Guid TeamId, string? Error)> GateAdminAsync(Guid partyId, Guid userId, CancellationToken ct)
    {
        var access = await _guard.ResolveAsync(partyId, userId, ct);
        if (access is null)
        {
            return (PartyOutcome.NotFound, Guid.Empty, "No such party.");
        }

        return access.Value.IsPartyAdmin
            ? (PartyOutcome.Ok, access.Value.TeamId, null)
            : (PartyOutcome.Forbidden, access.Value.TeamId, "Only a party admin can manage co-admins.");
    }

    private static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool IsUniqueViolation(Exception ex) =>
        ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
