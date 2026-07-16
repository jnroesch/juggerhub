using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Chat;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Conversations and the inbox (feature 019). Authorization is delegated wholly to
/// <see cref="ChatGuard"/> so the membership rule lives in one place; this service never re-implements
/// it (constitution Principle I).
/// </summary>
public sealed class ChatConversationService : IChatConversationService
{
    private readonly AppDbContext _db;
    private readonly ChatGuard _guard;

    public ChatConversationService(AppDbContext db, ChatGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    // --- Starting ---------------------------------------------------------------

    public async Task<ChatResult<ConversationSummaryDto>> StartAsync(
        Guid callerId,
        IReadOnlyList<Guid> participantUserIds,
        string? name,
        CancellationToken ct = default)
    {
        // Distinct, and never counting the caller — the client may send either shape.
        var others = participantUserIds.Where(id => id != callerId).Distinct().ToList();

        if (others.Count == 0)
        {
            return ChatResult<ConversationSummaryDto>.Fail(ChatOutcome.Invalid, "Pick someone to chat with.");
        }

        // Never trust the client's user ids: they must be real, non-banned accounts. Without this a
        // caller could seed a conversation with arbitrary GUIDs.
        var known = await _db.Users.AsNoTracking()
            .Where(u => others.Contains(u.Id) && u.Status != AccountStatus.Banned)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (known.Count != others.Count)
        {
            return ChatResult<ConversationSummaryDto>.Fail(ChatOutcome.Invalid, "Some of those players are unavailable.");
        }

        return others.Count == 1
            ? await StartDirectAsync(callerId, others[0], ct)
            : await StartGroupAsync(callerId, others, name, ct);
    }

    private async Task<ChatResult<ConversationSummaryDto>> StartDirectAsync(
        Guid callerId,
        Guid otherId,
        CancellationToken ct)
    {
        // Reach is open (spec FR-049) — no shared-context check. Block is the one gate, and it holds
        // in both directions so a blocked player cannot open a fresh thread to get around it (FR-049b).
        if (await _guard.IsBlockedBetweenAsync(callerId, otherId, ct))
        {
            return ChatResult<ConversationSummaryDto>.Fail(ChatOutcome.Forbidden, "You can't message this player.");
        }

        var pairKey = Conversation.BuildDirectPairKey(callerId, otherId);

        var existing = await _db.Conversations.AsNoTracking()
            .Where(c => c.DirectPairKey == pairKey)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != Guid.Empty)
        {
            return await SummariseAsync(callerId, existing, ct);
        }

        var conversation = new Conversation
        {
            Kind = ConversationKind.Direct,
            DirectPairKey = pairKey,
            State = ConversationState.Active,
        };
        _db.Conversations.Add(conversation);
        AddParticipants(conversation.Id, new[] { callerId, otherId });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two clients started the same DM at the same moment; the unique index on DirectPairKey
            // caught the loser. That is the point of having the constraint — resolve to the winner
            // rather than returning an error the user would not understand (spec FR-008).
            _db.ChangeTracker.Clear();
            var winner = await _db.Conversations.AsNoTracking()
                .Where(c => c.DirectPairKey == pairKey)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);

            if (winner == Guid.Empty)
            {
                throw;
            }

            return await SummariseAsync(callerId, winner, ct);
        }

        return await SummariseAsync(callerId, conversation.Id, ct);
    }

    private async Task<ChatResult<ConversationSummaryDto>> StartGroupAsync(
        Guid callerId,
        List<Guid> others,
        string? name,
        CancellationToken ct)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return ChatResult<ConversationSummaryDto>.Fail(ChatOutcome.Invalid, "Give your group a name.");
        }

        // +1 for the creator.
        if (others.Count + 1 > ChatConstants.MaxGroupMembers)
        {
            return ChatResult<ConversationSummaryDto>.Fail(
                ChatOutcome.Invalid,
                $"A group can have up to {ChatConstants.MaxGroupMembers} people.");
        }

        var conversation = new Conversation
        {
            Kind = ConversationKind.Group,
            Name = trimmed,
            State = ConversationState.Active,
        };
        _db.Conversations.Add(conversation);
        AddParticipants(conversation.Id, others.Append(callerId));

        await _db.SaveChangesAsync(ct);

        return await SummariseAsync(callerId, conversation.Id, ct);
    }

    /// <remarks>
    /// Added through <see cref="DbSet{TEntity}.Add"/> rather than the navigation collection: a
    /// client-generated UUIDv7 key on a nav-property insert can be misclassified by the change
    /// tracker (a known EF gotcha in this codebase).
    /// </remarks>
    private void AddParticipants(Guid conversationId, IEnumerable<Guid> userIds)
    {
        var now = DateTime.UtcNow;
        foreach (var userId in userIds)
        {
            _db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = userId,
                JoinedDate = now,
            });
        }
    }

    // --- Inbox ------------------------------------------------------------------

    public async Task<PagedResult<ConversationSummaryDto>> GetInboxAsync(
        Guid callerId,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        var query = VisibleConversations(callerId);

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(c => c.LastMessageDate ?? c.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(c => new
            {
                c.Id,
                c.Kind,
                c.Name,
                c.State,
                c.TeamId,
                c.PartyId,
                TeamName = c.Team!.Name,
                State_ = c.State,
                Me = c.Participants.FirstOrDefault(p => p.UserId == callerId),
                Other = c.Participants
                    .Where(p => p.UserId != callerId)
                    .Select(p => new { p.UserId, p.User.Profile!.DisplayName })
                    .FirstOrDefault(),
                Last = c.Messages
                    .OrderByDescending(m => m.Id)
                    .Select(m => new
                    {
                        m.Id,
                        m.Body,
                        m.CreatedDate,
                        m.SenderId,
                        m.IsDeleted,
                        m.Kind,
                        SenderName = m.Sender!.Profile!.DisplayName,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = new List<ConversationSummaryDto>(rows.Count);
        foreach (var r in rows)
        {
            var unread = await CountUnreadAsync(r.Id, callerId, r.Me?.LastReadMessageId, ct);

            items.Add(new ConversationSummaryDto(
                r.Id,
                r.Kind,
                DisplayName(r.Kind, r.Name, r.TeamName, r.Other?.DisplayName),
                BuildAvatar(r.Kind, r.TeamId, r.Other?.UserId),
                r.Last is null
                    ? null
                    : new LastMessageDto(
                        // A deleted message surrenders its preview — the inbox must not keep showing
                        // content the sender withdrew (spec FR-050c).
                        r.Last.IsDeleted ? string.Empty : r.Last.Body,
                        r.Last.CreatedDate,
                        r.Last.SenderId == callerId ? null : r.Last.SenderName ?? PlaceholderName,
                        r.Last.SenderId == callerId,
                        r.Last.Kind == ChatMessageKind.System),
                unread,
                r.Me?.IsMuted ?? false,
                r.State,
                r.TeamId,
                r.PartyId));
        }

        return new PagedResult<ConversationSummaryDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<int> GetUnreadTotalAsync(Guid callerId, CancellationToken ct = default)
    {
        // Muted and hidden conversations contribute nothing to the badge (spec FR-018).
        var rows = await VisibleConversations(callerId)
            .Where(c => !c.Participants.Any(p => p.UserId == callerId && p.IsMuted))
            .Select(c => new
            {
                c.Id,
                LastRead = c.Participants
                    .Where(p => p.UserId == callerId)
                    .Select(p => p.LastReadMessageId)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var total = 0;
        foreach (var r in rows)
        {
            total += await CountUnreadAsync(r.Id, callerId, r.LastRead, ct);
        }

        return total;
    }

    /// <summary>
    /// The conversations a caller can see in their inbox: member of (by the same rule as
    /// <see cref="ChatGuard"/>), not hidden, and — for DMs — not with someone they have blocked.
    /// </summary>
    /// <remarks>
    /// This mirrors ChatGuard's predicate deliberately: the guard answers "may I open <em>this</em>
    /// one?" in one round trip, while the inbox needs the same rule as a composable query. If either
    /// changes, both must — which is why both are expressed as the same three branches in the same
    /// order.
    /// </remarks>
    private IQueryable<Conversation> VisibleConversations(Guid callerId) =>
        _db.Conversations.AsNoTracking()
            .Where(c =>
                ((c.Kind == ConversationKind.Direct || c.Kind == ConversationKind.Group)
                    && c.Participants.Any(p => p.UserId == callerId && p.LeftDate == null))
                || (c.Kind == ConversationKind.Team
                    && _db.TeamMemberships.Any(m => m.TeamId == c.TeamId && m.UserId == callerId))
                || (c.Kind == ConversationKind.Party
                    && _db.PartyMembers.Any(pm => pm.PartyId == c.PartyId
                        && pm.UserId == callerId
                        && pm.Status == PartyMemberStatus.In))
                // An archived auto chat has no roster left to derive from, so its snapshotted
                // participant rows carry membership (data-model R3a).
                || (c.State == ConversationState.Archived
                    && c.Participants.Any(p => p.UserId == callerId && p.LeftDate == null)))
            .Where(c => !c.Participants.Any(p => p.UserId == callerId && p.IsHidden))
            .Where(c => c.Kind != ConversationKind.Direct
                || !_db.UserBlocks.Any(b =>
                    (b.BlockerUserId == callerId
                        && c.Participants.Any(p => p.UserId == b.BlockedUserId))
                    || (b.BlockedUserId == callerId
                        && c.Participants.Any(p => p.UserId == b.BlockerUserId))));

    /// <summary>
    /// Unread = messages after the caller's read marker, not their own, not deleted.
    /// </summary>
    /// <remarks>
    /// <c>m.Id.CompareTo(lastRead) &gt; 0</c> works because message ids are UUIDv7 — monotonic and
    /// timestamp-prefixed — so "greater id" means "sent later". A range scan on the composite
    /// (ConversationId, Id) index rather than a join against a receipt table (research §3).
    /// </remarks>
    private Task<int> CountUnreadAsync(Guid conversationId, Guid callerId, Guid? lastReadMessageId, CancellationToken ct)
    {
        var q = _db.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId
                && m.SenderId != callerId
                && !m.IsDeleted);

        if (lastReadMessageId is { } lastRead)
        {
            q = q.Where(m => m.Id.CompareTo(lastRead) > 0);
        }

        return q.CountAsync(ct);
    }

    // --- Detail & members -------------------------------------------------------

    public async Task<ChatResult<ConversationDetailDto>> GetDetailAsync(
        Guid callerId,
        Guid conversationId,
        CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(conversationId, callerId, ct);
        if (access is not { } a)
        {
            return ChatResult<ConversationDetailDto>.Fail(ChatOutcome.NotFound);
        }

        var row = await _db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new
            {
                c.Kind,
                c.Name,
                c.State,
                c.TeamId,
                c.PartyId,
                TeamName = c.Team!.Name,
                Other = c.Participants
                    .Where(p => p.UserId != callerId)
                    .Select(p => new { p.UserId, p.User.Profile!.DisplayName })
                    .FirstOrDefault(),
                Me = c.Participants.FirstOrDefault(p => p.UserId == callerId),
            })
            .FirstAsync(ct);

        var memberCount = (await _guard.ResolveParticipantUserIdsAsync(conversationId, ct)).Count;

        return ChatResult<ConversationDetailDto>.Ok(new ConversationDetailDto(
            conversationId,
            row.Kind,
            DisplayName(row.Kind, row.Name, row.TeamName, row.Other?.DisplayName),
            BuildAvatar(row.Kind, row.TeamId, row.Other?.UserId),
            row.State,
            row.Me?.IsMuted ?? false,
            row.Me?.IsHidden ?? false,
            memberCount,
            // Only a manual group can be left; a team/party chat offers mute instead (spec FR-026).
            CanLeave: a.IsManualGroup && !a.IsArchived,
            CanAddMembers: a.IsManualGroup && !a.IsArchived,
            row.TeamId,
            row.PartyId));
    }

    public async Task<ChatResult<PagedResult<MemberDto>>> GetMembersAsync(
        Guid callerId,
        Guid conversationId,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(conversationId, callerId, ct);
        if (access is not { } a)
        {
            return ChatResult<PagedResult<MemberDto>>.Fail(ChatOutcome.NotFound);
        }

        // Team/party member lists come from the roster, not from participant rows — the rows are
        // state only and may not even exist for a player who has never opened the chat.
        var userIds = await _guard.ResolveParticipantUserIdsAsync(conversationId, ct);
        var total = userIds.Count;

        var page = userIds
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .ToList();

        var profiles = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => page.Contains(p.UserId))
            .Select(p => new { p.UserId, p.DisplayName, p.Handle })
            .ToListAsync(ct);

        var viaMarket = a.Kind == ConversationKind.Party
            ? (await _db.PartyMembers.AsNoTracking()
                .Where(pm => pm.PartyId == a.PartyId && pm.ViaMarket && pm.Status == PartyMemberStatus.In)
                .Select(pm => pm.UserId)
                .ToListAsync(ct))
                .ToHashSet()
            : new HashSet<Guid>();

        var items = page.Select(id =>
        {
            var profile = profiles.FirstOrDefault(p => p.UserId == id);
            return new MemberDto(
                id,
                // A banned account's profile is filtered out globally (feature 013), so this is null
                // rather than missing — render the placeholder instead of dropping the member.
                profile?.DisplayName ?? PlaceholderName,
                profile?.Handle,
                null,
                IsYou: id == callerId,
                ViaMarket: viaMarket.Contains(id));
        }).ToList();

        return ChatResult<PagedResult<MemberDto>>.Ok(
            new PagedResult<MemberDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake));
    }

    // --- Read state -------------------------------------------------------------

    public async Task<ChatResult> MarkReadAsync(
        Guid callerId,
        Guid conversationId,
        Guid lastReadMessageId,
        CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(conversationId, callerId, ct);
        if (access is null)
        {
            return ChatResult.Fail(ChatOutcome.NotFound);
        }

        // The marker must name a real message in THIS conversation — otherwise a client could send an
        // arbitrary (or maximal) GUID and mark everything read, or worse, park the marker beyond every
        // future message and never see an unread again.
        var exists = await _db.ChatMessages.AsNoTracking()
            .AnyAsync(m => m.Id == lastReadMessageId && m.ConversationId == conversationId, ct);

        if (!exists)
        {
            return ChatResult.Fail(ChatOutcome.Invalid, "Unknown message.");
        }

        var state = await _guard.EnsureParticipantStateAsync(conversationId, callerId, ct);

        // Never move backwards: a slow request from a stale tab must not resurrect messages the player
        // has already read on another device.
        if (state.LastReadMessageId is { } current && current.CompareTo(lastReadMessageId) >= 0)
        {
            return ChatResult.Ok();
        }

        state.LastReadMessageId = lastReadMessageId;
        await _db.SaveChangesAsync(ct);

        return ChatResult.Ok();
    }

    // --- Projection helpers -----------------------------------------------------

    /// <summary>Stands in for a player whose profile is gone or hidden (deleted/banned — feature 013).</summary>
    internal const string PlaceholderName = "A former player";

    private async Task<ChatResult<ConversationSummaryDto>> SummariseAsync(Guid callerId, Guid conversationId, CancellationToken ct)
    {
        var page = await GetInboxAsync(callerId, new PaginationRequest { Skip = 0, Take = 100 }, ct);
        var found = page.Items.FirstOrDefault(c => c.Id == conversationId);

        return found is null
            ? ChatResult<ConversationSummaryDto>.Fail(ChatOutcome.NotFound)
            : ChatResult<ConversationSummaryDto>.Ok(found);
    }

    /// <summary>
    /// A conversation's display name. Only a group stores one; the rest derive it — except an archived
    /// auto chat, which froze its name at archival because the link it derived from is gone (R3a).
    /// </summary>
    private static string DisplayName(ConversationKind kind, string? stored, string? teamName, string? otherName) =>
        kind switch
        {
            ConversationKind.Group => stored ?? "Group",
            ConversationKind.Direct => otherName ?? PlaceholderName,
            ConversationKind.Team => stored ?? teamName ?? "Team chat",
            ConversationKind.Party => stored ?? "Party chat",
            _ => stored ?? "Chat",
        };

    private static ConversationAvatarDto BuildAvatar(ConversationKind kind, Guid? teamId, Guid? otherUserId) =>
        kind switch
        {
            ConversationKind.Direct => new ConversationAvatarDto("User", otherUserId, null, null),
            ConversationKind.Team => new ConversationAvatarDto("Team", null, teamId, null),
            ConversationKind.Party => new ConversationAvatarDto("Party", null, null, null),
            _ => new ConversationAvatarDto("Group", null, null, null),
        };
}
