using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Chat;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Chat search (feature 019, User Story 6). Matching uses <c>ILike</c> + <c>Unaccent</c>, the
/// convention feature 007 established and every other search surface in this codebase follows.
/// </summary>
/// <remarks>
/// <b>The scope predicate is the security property here.</b> Message results are restricted to the
/// caller's own conversations inside the database query (spec FR-035) — never fetched broadly and
/// filtered afterwards, which would leak through counts, timing, or the next careless refactor. A term
/// that exists only in someone else's conversation returns nothing, and nothing hints that it exists.
/// </remarks>
public sealed class ChatSearchService : IChatSearchService
{
    private readonly AppDbContext _db;

    public ChatSearchService(AppDbContext db) => _db = db;

    public async Task<ChatSearchResultDto> SearchAsync(
        Guid callerId,
        string term,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        var trimmed = term?.Trim() ?? string.Empty;

        if (trimmed.Length < ChatConstants.MinSearchTermLength)
        {
            return Empty(pagination);
        }

        var pattern = $"%{trimmed}%";

        var messages = await SearchMessagesAsync(callerId, pattern, pagination, ct);
        var people = await SearchPeopleAsync(callerId, pattern, pagination, ct);

        return new ChatSearchResultDto(messages, people);
    }

    private async Task<PagedResult<MessageSearchHitDto>> SearchMessagesAsync(
        Guid callerId,
        string pattern,
        PaginationRequest pagination,
        CancellationToken ct)
    {
        // The membership predicate runs first and is indexed, so the ILIKE only ever scans this
        // player's own messages — which is both the security boundary and why the scan is cheap.
        var query = _db.ChatMessages.AsNoTracking()
            .Where(m => !m.IsDeleted && m.Kind == ChatMessageKind.Member)
            // Membership AND the join cutoff, in one predicate: the roster/participant row must both
            // exist and pre-date the message (its JoinedDate/CreatedDate <= the message), so search
            // never surfaces a message from before the caller joined (spec FR-035, FR-051). Archived
            // chats are exempt — their history stays fully searchable (FR-027) — and are checked first
            // because archival stamps snapshot rows at archive time.
            .Where(m =>
                (m.Conversation.State == ConversationState.Archived
                    && m.Conversation.Participants.Any(p => p.UserId == callerId && p.LeftDate == null))
                || ((m.Conversation.Kind == ConversationKind.Direct || m.Conversation.Kind == ConversationKind.Group)
                    && m.Conversation.Participants.Any(p => p.UserId == callerId && p.LeftDate == null && p.JoinedDate <= m.CreatedDate))
                || (m.Conversation.Kind == ConversationKind.Team
                    && _db.TeamMemberships.Any(tm => tm.TeamId == m.Conversation.TeamId && tm.UserId == callerId && tm.JoinedDate <= m.CreatedDate))
                || (m.Conversation.Kind == ConversationKind.Party
                    && _db.PartyMembers.Any(pm => pm.PartyId == m.Conversation.PartyId
                        && pm.UserId == callerId
                        && pm.Status == PartyMemberStatus.In
                        && pm.CreatedDate <= m.CreatedDate)))
            .Where(m => EF.Functions.ILike(AppDbContext.Unaccent(m.Body), AppDbContext.Unaccent(pattern)));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.Id)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(m => new MessageSearchHitDto(
                m.Id,
                m.ConversationId,
                m.Conversation.Name ?? m.Conversation.Team!.Name ?? "Chat",
                m.Conversation.Kind,
                m.Body,
                m.CreatedDate,
                m.Sender!.Profile!.DisplayName))
            .ToListAsync(ct);

        return new PagedResult<MessageSearchHitDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    private async Task<PagedResult<PersonHitDto>> SearchPeopleAsync(
        Guid callerId,
        string pattern,
        PaginationRequest pagination,
        CancellationToken ct)
    {
        // Reach is open (FR-049): people search is not restricted to teammates. Two exclusions only —
        // yourself, and anyone either of you has blocked (FR-033), since offering a chat that the send
        // would refuse is a dead end.
        var query = _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId != callerId)
            .Where(p => !_db.UserBlocks.Any(b =>
                (b.BlockerUserId == callerId && b.BlockedUserId == p.UserId)
                || (b.BlockerUserId == p.UserId && b.BlockedUserId == callerId)))
            .Where(p =>
                EF.Functions.ILike(AppDbContext.Unaccent(p.DisplayName), AppDbContext.Unaccent(pattern))
                || EF.Functions.ILike(p.Handle, pattern));

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(p => p.DisplayName)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(p => new { p.UserId, p.DisplayName, p.Handle })
            .ToListAsync(ct);

        var items = new List<PersonHitDto>(rows.Count);
        foreach (var r in rows)
        {
            // Surface an existing DM so the client opens it rather than starting a duplicate (FR-008).
            var pairKey = Conversation.BuildDirectPairKey(callerId, r.UserId);
            var existing = await _db.Conversations.AsNoTracking()
                .Where(c => c.DirectPairKey == pairKey)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);

            items.Add(new PersonHitDto(r.UserId, r.DisplayName, r.Handle, null, existing));
        }

        return new PagedResult<PersonHitDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    private static ChatSearchResultDto Empty(PaginationRequest pagination) =>
        new(
            new PagedResult<MessageSearchHitDto>(Array.Empty<MessageSearchHitDto>(), 0, pagination.NormalizedSkip, pagination.NormalizedTake),
            new PagedResult<PersonHitDto>(Array.Empty<PersonHitDto>(), 0, pagination.NormalizedSkip, pagination.NormalizedTake));
}
