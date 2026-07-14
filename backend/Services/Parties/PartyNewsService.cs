using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Parties;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Parties;

/// <summary>
/// EF-Core-direct implementation of <see cref="IPartyNewsService"/> (feature 016). The feed is
/// private to the crew (In members); posting is party-admin-only and notifies the crew (in-app +
/// email), mirroring team news. Posts are deleted with the party on disband (cascade).
/// </summary>
public sealed class PartyNewsService : IPartyNewsService
{
    private readonly AppDbContext _db;
    private readonly PartyGuard _guard;
    private readonly INotificationService _notifications;
    private readonly PartyEmailService _email;

    public PartyNewsService(AppDbContext db, PartyGuard guard, INotificationService notifications, PartyEmailService email)
    {
        _db = db;
        _guard = guard;
        _notifications = notifications;
        _email = email;
    }

    public async Task<PagedResult<PartyNewsDto>?> ListAsync(Guid partyId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null || !access.Value.IsCrew)
        {
            return null; // 404 — private to the crew.
        }

        var query = _db.PartyNewsPosts.AsNoTracking().Where(n => n.PartyId == partyId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(n => new PartyNewsDto(
                n.Id,
                n.Author.Profile!.DisplayName,
                _db.PartyMembers
                    .Where(m => m.PartyId == partyId && m.UserId == n.AuthorUserId)
                    .Select(m => m.Role)
                    .FirstOrDefault(),
                n.Body,
                n.CreatedDate))
            .ToListAsync(ct);

        return new PagedResult<PartyNewsDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<PartyResult<PartyNewsDto>> CreateAsync(Guid partyId, string body, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is null)
        {
            return PartyResult<PartyNewsDto>.Fail(PartyOutcome.NotFound);
        }

        if (!access.Value.IsPartyAdmin)
        {
            return PartyResult<PartyNewsDto>.Fail(PartyOutcome.Forbidden, "Only a party admin can post news.");
        }

        var trimmed = (body ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > 1000)
        {
            return PartyResult<PartyNewsDto>.Fail(PartyOutcome.Invalid, "Write an update of up to 1000 characters.");
        }

        var post = new PartyNewsPost { PartyId = partyId, AuthorUserId = actorUserId, Body = trimmed };
        _db.PartyNewsPosts.Add(post);
        await _db.SaveChangesAsync(ct);

        await NotifyCrewAsync(partyId, actorUserId, post.Id, ct);

        var authorName = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == actorUserId).Select(p => p.DisplayName).FirstAsync(ct);
        var dto = new PartyNewsDto(post.Id, authorName, PartyMemberRole.Admin, post.Body, post.CreatedDate);
        return PartyResult<PartyNewsDto>.Ok(dto);
    }

    private async Task NotifyCrewAsync(Guid partyId, Guid actorUserId, Guid postId, CancellationToken ct)
    {
        var info = await _db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId)
            .Select(p => new { p.EventId, EventName = p.Event.Name, TeamName = p.Team.Name, TeamSlug = p.Team.Slug })
            .FirstAsync(ct);

        var crew = await _db.PartyMembers.AsNoTracking()
            .Where(m => m.PartyId == partyId && m.Status == PartyMemberStatus.In && m.UserId != actorUserId)
            .Select(m => new { m.UserId, m.User.Email, Name = m.User.Profile!.DisplayName })
            .ToListAsync(ct);
        if (crew.Count == 0)
        {
            return;
        }

        await _notifications.CreateManyAsync(
            crew.Select(c => c.UserId).ToList(),
            NotificationType.PartyNews,
            new { partyId, info.EventId, info.TeamSlug, info.EventName, info.TeamName },
            actorUserId,
            dedupeKeyPrefix: $"party-news:{postId}",
            ct);

        // Body isn't stored on the post at fan-out time — re-read for the email copy.
        var body = await _db.PartyNewsPosts.AsNoTracking().Where(n => n.Id == postId).Select(n => n.Body).FirstAsync(ct);
        foreach (var c in crew.Where(c => !string.IsNullOrEmpty(c.Email)))
        {
            await _email.SendPartyNewsEmailAsync(c.Email!, c.Name, info.TeamName, info.EventName, info.TeamSlug, info.EventId, body, ct);
        }
    }
}
