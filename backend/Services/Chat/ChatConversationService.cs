using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Chat;
using JuggerHub.Entities;
using JuggerHub.Services.Chat.Realtime;
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
    private readonly IChatRealtime _realtime;
    private readonly IChatMessageService _messages;

    public ChatConversationService(
        AppDbContext db,
        ChatGuard guard,
        IChatRealtime realtime,
        IChatMessageService messages)
    {
        _db = db;
        _guard = guard;
        _realtime = realtime;
        _messages = messages;
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
        var ensured = await EnsureDirectAsync(callerId, otherId, ct);
        return ensured.IsOk
            ? await SummariseAsync(callerId, ensured.Value, ct)
            : ChatResult<ConversationSummaryDto>.Fail(ensured.Outcome, ensured.Error);
    }

    /// <summary>
    /// Get the direct conversation for a pair, creating it if absent. The one place a DM comes into
    /// existence — shared by <see cref="StartDirectAsync"/> (idempotent open) and
    /// <see cref="SendFirstDirectAsync"/> (feature 022 lazy creation).
    /// </summary>
    /// <remarks>
    /// Reach is open (spec FR-049) — no shared-context check. Block is the one gate, and it holds in
    /// both directions so a blocked player cannot open a fresh thread to get around it (FR-049b). The
    /// create is race-safe by the unique filtered index on <c>DirectPairKey</c>: two concurrent creates
    /// collide and the loser resolves to the winner rather than making a second row (spec FR-008).
    /// </remarks>
    private async Task<ChatResult<Guid>> EnsureDirectAsync(Guid callerId, Guid otherId, CancellationToken ct)
    {
        if (await _guard.IsBlockedBetweenAsync(callerId, otherId, ct))
        {
            return ChatResult<Guid>.Fail(ChatOutcome.Forbidden, "You can't message this player.");
        }

        var pairKey = Conversation.BuildDirectPairKey(callerId, otherId);

        var existing = await _db.Conversations.AsNoTracking()
            .Where(c => c.DirectPairKey == pairKey)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != Guid.Empty)
        {
            return ChatResult<Guid>.Ok(existing);
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
            // Two clients created the same DM at the same moment; the unique index on DirectPairKey
            // caught the loser. Resolve to the winner rather than returning an error the user would not
            // understand (spec FR-008 / FR-006).
            _db.ChangeTracker.Clear();
            var winner = await _db.Conversations.AsNoTracking()
                .Where(c => c.DirectPairKey == pairKey)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);

            if (winner == Guid.Empty)
            {
                throw;
            }

            return ChatResult<Guid>.Ok(winner);
        }

        return ChatResult<Guid>.Ok(conversation.Id);
    }

    /// <summary>
    /// Lazy DM creation (feature 022): create the direct conversation only when the first message is
    /// sent. Validates the target, ensures the (race-safe) conversation, then writes the message —
    /// so a chat opened but never written to leaves nothing behind.
    /// </summary>
    public async Task<ChatResult<DirectMessageSentDto>> SendFirstDirectAsync(
        Guid callerId,
        Guid targetUserId,
        string body,
        CancellationToken ct = default)
    {
        if (targetUserId == callerId)
        {
            return ChatResult<DirectMessageSentDto>.Fail(ChatOutcome.Invalid, "Pick someone to chat with.");
        }

        // Never trust the client's target id: it must be a real, non-banned account (mirrors StartAsync).
        var known = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == targetUserId && u.Status != AccountStatus.Banned, ct);
        if (!known)
        {
            return ChatResult<DirectMessageSentDto>.Fail(ChatOutcome.Invalid, "That player is unavailable.");
        }

        var ensured = await EnsureDirectAsync(callerId, targetUserId, ct);
        if (!ensured.IsOk)
        {
            return ChatResult<DirectMessageSentDto>.Fail(ensured.Outcome, ensured.Error);
        }

        var conversationId = ensured.Value;
        var sent = await _messages.SendAsync(callerId, conversationId, body, ct);
        if (!sent.IsOk)
        {
            return ChatResult<DirectMessageSentDto>.Fail(sent.Outcome, sent.Error);
        }

        // Return the inbox summary too, so the client can show the new thread in the rail immediately.
        var summary = await SummariseAsync(callerId, conversationId, ct);
        if (!summary.IsOk || summary.Value is null)
        {
            return ChatResult<DirectMessageSentDto>.Fail(ChatOutcome.NotFound);
        }

        return ChatResult<DirectMessageSentDto>.Ok(new DirectMessageSentDto(summary.Value, sent.Value!));
    }

    // --- Inquiry threads (feature 027 — contact the admins) ---------------------

    /// <summary>
    /// Send the first message to a team's/event's admins, creating the inquiry thread on first send
    /// (feature 027, mirrors feature 022's lazy DM creation). Validates the target, rejects a caller who
    /// already administers it (FR-002), ensures the race-safe thread, then writes the message — so an
    /// entry point opened but never sent from leaves nothing behind.
    /// </summary>
    public async Task<ChatResult<InquiryMessageSentDto>> SendFirstInquiryAsync(
        Guid callerId,
        ConversationKind kind,
        Guid targetId,
        string body,
        CancellationToken ct = default)
    {
        if (kind is not (ConversationKind.TeamInquiry or ConversationKind.EventInquiry))
        {
            return ChatResult<InquiryMessageSentDto>.Fail(ChatOutcome.Invalid, "Not an inquiry conversation.");
        }

        // Validate the target server-side, and reject a caller who is already an admin — they ARE the
        // admin group and cannot meaningfully contact themselves (FR-002). Absent/invisible ⇒ NotFound.
        if (kind == ConversationKind.TeamInquiry)
        {
            var exists = await _db.Teams.AsNoTracking().AnyAsync(t => t.Id == targetId, ct);
            if (!exists)
            {
                return ChatResult<InquiryMessageSentDto>.Fail(ChatOutcome.NotFound);
            }

            var isAdmin = await _db.TeamMemberships.AsNoTracking()
                .AnyAsync(m => m.TeamId == targetId && m.UserId == callerId && m.Role == TeamRole.Admin, ct);
            if (isAdmin)
            {
                return ChatResult<InquiryMessageSentDto>.Fail(ChatOutcome.Conflict, "You're already an admin of this team.");
            }
        }
        else
        {
            var status = await _db.Events.AsNoTracking()
                .Where(e => e.Id == targetId)
                .Select(e => (EventStatus?)e.Status)
                .FirstOrDefaultAsync(ct);
            if (status is null)
            {
                return ChatResult<InquiryMessageSentDto>.Fail(ChatOutcome.NotFound);
            }

            if (status == EventStatus.Cancelled)
            {
                return ChatResult<InquiryMessageSentDto>.Fail(ChatOutcome.Conflict, "This event is closed.");
            }

            var isAdmin = await _db.EventAdmins.AsNoTracking()
                .AnyAsync(a => a.EventId == targetId && a.UserId == callerId, ct);
            if (isAdmin)
            {
                return ChatResult<InquiryMessageSentDto>.Fail(ChatOutcome.Conflict, "You're already an admin of this event.");
            }
        }

        var conversationId = await EnsureInquiryAsync(callerId, kind, targetId, ct);

        var sent = await _messages.SendAsync(callerId, conversationId, body, ct);
        if (!sent.IsOk)
        {
            return ChatResult<InquiryMessageSentDto>.Fail(sent.Outcome, sent.Error);
        }

        var summary = await SummariseAsync(callerId, conversationId, ct);
        if (!summary.IsOk || summary.Value is null)
        {
            return ChatResult<InquiryMessageSentDto>.Fail(ChatOutcome.NotFound);
        }

        return ChatResult<InquiryMessageSentDto>.Ok(new InquiryMessageSentDto(summary.Value, sent.Value!));
    }

    public Task<Guid?> FindInquiryThreadAsync(
        Guid callerId,
        ConversationKind kind,
        Guid targetId,
        CancellationToken ct = default) =>
        FindInquiryAsync(callerId, kind, targetId, ct);

    /// <summary>
    /// Get the inquiry thread for a (requester, target), creating it if absent. Race-safe by the unique
    /// filtered indexes on (TeamId, RequesterUserId)/(EventId, RequesterUserId): two concurrent first
    /// sends collide and the loser resolves to the winner rather than making a second row (FR-004).
    /// </summary>
    private async Task<Guid> EnsureInquiryAsync(Guid callerId, ConversationKind kind, Guid targetId, CancellationToken ct)
    {
        var existing = await FindInquiryAsync(callerId, kind, targetId, ct);
        if (existing is { } found)
        {
            return found;
        }

        var conversation = new Conversation
        {
            Kind = kind,
            TeamId = kind == ConversationKind.TeamInquiry ? targetId : null,
            EventId = kind == ConversationKind.EventInquiry ? targetId : null,
            RequesterUserId = callerId,
            State = ConversationState.Active,
        };
        _db.Conversations.Add(conversation);
        // The requester is a STORED participant (their side of the thread is fixed, not derived), which
        // also anchors their join-time read cutoff. Admins are never stored — they derive from the roster.
        AddParticipants(conversation.Id, new[] { callerId });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var winner = await FindInquiryAsync(callerId, kind, targetId, ct);
            if (winner is not { } id)
            {
                throw;
            }

            return id;
        }

        return conversation.Id;
    }

    private async Task<Guid?> FindInquiryAsync(Guid callerId, ConversationKind kind, Guid targetId, CancellationToken ct)
    {
        var id = await _db.Conversations.AsNoTracking()
            .Where(c => c.Kind == kind
                && c.RequesterUserId == callerId
                && (kind == ConversationKind.TeamInquiry ? c.TeamId == targetId : c.EventId == targetId))
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        return id == Guid.Empty ? null : id;
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
        await EnsureAutoChatsForAsync(callerId, ct);

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
                c.EventId,
                c.RequesterUserId,
                TeamName = c.Team!.Name,
                EventName = c.Event!.Name,
                RequesterName = c.Requester!.Profile!.DisplayName,
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

        // A member sees nothing sent before they joined (spec FR-051): it drops out of their unread
        // count and, when there is no newer message, out of their inbox preview too.
        var cutoffs = await _guard.ResolveJoinCutoffsAsync(
            callerId,
            rows.Select(r => new ChatAccess(r.Id, r.Kind, r.State, r.TeamId, r.PartyId, r.EventId, r.RequesterUserId)).ToList(),
            ct);

        var items = new List<ConversationSummaryDto>(rows.Count);
        foreach (var r in rows)
        {
            var cutoff = cutoffs.GetValueOrDefault(r.Id);
            var unread = await CountUnreadAsync(r.Id, callerId, r.Me?.LastReadMessageId, cutoff, ct);

            // The last message is the newest, so if it predates the cutoff every message does — the
            // caller has no post-join history yet and the row shows no preview.
            var last = r.Last is not null && cutoff is { } joinedAt && r.Last.CreatedDate < joinedAt
                ? null
                : r.Last;

            var isRequester = r.RequesterUserId == callerId;

            items.Add(new ConversationSummaryDto(
                r.Id,
                r.Kind,
                DisplayName(r.Kind, r.Name, r.TeamName, r.Other?.DisplayName, r.EventName, r.RequesterName, isRequester),
                BuildAvatar(r.Kind, r.TeamId, r.Other?.UserId, r.RequesterUserId, isRequester),
                last is null
                    ? null
                    : new LastMessageDto(
                        // A deleted message surrenders its preview — the inbox must not keep showing
                        // content the sender withdrew (spec FR-050c).
                        last.IsDeleted ? string.Empty : last.Body,
                        last.CreatedDate,
                        last.SenderId == callerId ? null : last.SenderName ?? PlaceholderName,
                        last.SenderId == callerId,
                        last.Kind == ChatMessageKind.System),
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
                c.Kind,
                c.State,
                c.TeamId,
                c.PartyId,
                LastRead = c.Participants
                    .Where(p => p.UserId == callerId)
                    .Select(p => p.LastReadMessageId)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        // Pre-join messages never count toward the badge (spec FR-051).
        var cutoffs = await _guard.ResolveJoinCutoffsAsync(
            callerId,
            rows.Select(r => new ChatAccess(r.Id, r.Kind, r.State, r.TeamId, r.PartyId)).ToList(),
            ct);

        var total = 0;
        foreach (var r in rows)
        {
            total += await CountUnreadAsync(r.Id, callerId, r.LastRead, cutoffs.GetValueOrDefault(r.Id), ct);
        }

        return total;
    }

    /// <summary>
    /// Materialise the caller's team/party chats if this is the first time anyone has looked.
    /// </summary>
    /// <remarks>
    /// This is what "your team chat shows up the moment you join a team" actually is — no creation
    /// step, no migration, no event handler on the roster. It also means a team that existed long
    /// before this feature shipped gets its chat the first time a member opens the inbox, which is
    /// FR-024's backfill requirement satisfied by construction (research §4).
    /// </remarks>
    private async Task EnsureAutoChatsForAsync(Guid callerId, CancellationToken ct)
    {
        var teamIds = await _db.TeamMemberships.AsNoTracking()
            .Where(m => m.UserId == callerId)
            .Select(m => m.TeamId)
            .Where(id => !_db.Conversations.Any(c => c.TeamId == id))
            .ToListAsync(ct);

        foreach (var teamId in teamIds)
        {
            await EnsureForTeamAsync(teamId, ct);
        }

        var partyIds = await _db.PartyMembers.AsNoTracking()
            .Where(pm => pm.UserId == callerId && pm.Status == PartyMemberStatus.In)
            .Select(pm => pm.PartyId)
            .Where(id => !_db.Conversations.Any(c => c.PartyId == id))
            .ToListAsync(ct);

        foreach (var partyId in partyIds)
        {
            await EnsureForPartyAsync(partyId, ct);
        }
    }

    /// <summary>
    /// The conversations a caller can see in their inbox: member of (by the same rule as
    /// <see cref="ChatGuard"/>), not hidden, and — for DMs — not with someone they have blocked.
    /// </summary>
    /// <remarks>
    /// The membership half of this is <see cref="ChatGuard.IsMemberOf"/> — the <em>same expression</em>
    /// the guard uses to answer "may I open this one?", shared rather than restated. An earlier version
    /// duplicated it here and the copies drifted: the guard was taught about archived auto chats and
    /// this one already knew, so an archived chat listed in the inbox but 404'd when opened. Sharing the
    /// expression makes that class of bug unrepresentable.
    /// </remarks>
    private IQueryable<Conversation> VisibleConversations(Guid callerId) =>
        _db.Conversations.AsNoTracking()
            .Where(ChatGuard.IsMemberOf(_db, callerId))
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
    private Task<int> CountUnreadAsync(Guid conversationId, Guid callerId, Guid? lastReadMessageId, DateTime? cutoff, CancellationToken ct)
    {
        var q = _db.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId
                && m.SenderId != callerId
                && !m.IsDeleted);

        // Messages from before the caller joined are not theirs to be notified about (spec FR-051).
        if (cutoff is { } joinedAt)
        {
            q = q.Where(m => m.CreatedDate >= joinedAt);
        }

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
                c.EventId,
                c.RequesterUserId,
                TeamName = c.Team!.Name,
                EventName = c.Event!.Name,
                RequesterName = c.Requester!.Profile!.DisplayName,
                Other = c.Participants
                    .Where(p => p.UserId != callerId)
                    .Select(p => new { p.UserId, p.User.Profile!.DisplayName })
                    .FirstOrDefault(),
                Me = c.Participants.FirstOrDefault(p => p.UserId == callerId),
            })
            .FirstAsync(ct);

        var memberCount = (await _guard.ResolveParticipantUserIdsAsync(conversationId, ct)).Count;
        var isRequester = row.RequesterUserId == callerId;

        return ChatResult<ConversationDetailDto>.Ok(new ConversationDetailDto(
            conversationId,
            row.Kind,
            DisplayName(row.Kind, row.Name, row.TeamName, row.Other?.DisplayName, row.EventName, row.RequesterName, isRequester),
            BuildAvatar(row.Kind, row.TeamId, row.Other?.UserId, row.RequesterUserId, isRequester),
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

        // Push the caller's own new total to their OWN group, so reading on one device converges the
        // badge on their other open tabs (spec FR-016). Nobody else's total changed.
        await _realtime.PushUnreadCountAsync(callerId, await GetUnreadTotalAsync(callerId, ct), ct);

        return ChatResult.Ok();
    }

    // --- Typing -----------------------------------------------------------------

    public async Task<ChatResult> SignalTypingAsync(Guid callerId, Guid conversationId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(conversationId, callerId, ct);
        if (access is not { } a)
        {
            return ChatResult.Fail(ChatOutcome.NotFound);
        }

        if (a.IsArchived)
        {
            return ChatResult.Fail(ChatOutcome.Conflict, "This chat is closed.");
        }

        var recipients = (await _guard.ResolveParticipantUserIdsAsync(conversationId, ct))
            .Where(id => id != callerId)
            .ToList();

        if (recipients.Count == 0)
        {
            return ChatResult.Ok();
        }

        var name = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == callerId)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(ct) ?? PlaceholderName;

        // Nothing is persisted: the signal carries its own expiry and the client also expires it on a
        // timer, so a typist who closes their tab mid-word never leaves a stuck indicator (FR-020).
        await _realtime.PushTypingAsync(recipients, conversationId, callerId, name, ct);

        return ChatResult.Ok();
    }

    // --- Group membership (US3) -------------------------------------------------

    public async Task<ChatResult> AddMembersAsync(
        Guid callerId,
        Guid conversationId,
        IReadOnlyList<Guid> userIds,
        CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(conversationId, callerId, ct);
        if (access is not { } a)
        {
            return ChatResult.Fail(ChatOutcome.NotFound);
        }

        // Direct/Team/Party membership is not addable: a DM is a pair by definition, and an auto chat's
        // roster is the team's or party's business, not the chat's (spec FR-026).
        if (!a.IsManualGroup)
        {
            return ChatResult.Fail(ChatOutcome.Invalid, "You can only add people to a group you made.");
        }

        if (a.IsArchived)
        {
            return ChatResult.Fail(ChatOutcome.Conflict, "This chat is closed.");
        }

        var wanted = userIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return ChatResult.Ok();
        }

        var known = await _db.Users.AsNoTracking()
            .Where(u => wanted.Contains(u.Id) && u.Status != AccountStatus.Banned)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (known.Count != wanted.Count)
        {
            return ChatResult.Fail(ChatOutcome.Invalid, "Some of those players are unavailable.");
        }

        var current = await _db.ConversationParticipants.AsNoTracking()
            .Where(p => p.ConversationId == conversationId && p.LeftDate == null)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        // Already-a-member is a no-op — no duplicate row, no second system line (US3 edge case).
        var toAdd = wanted.Except(current).ToList();
        if (toAdd.Count == 0)
        {
            return ChatResult.Ok();
        }

        if (current.Count + toAdd.Count > ChatConstants.MaxGroupMembers)
        {
            return ChatResult.Fail(
                ChatOutcome.Invalid,
                $"A group can have up to {ChatConstants.MaxGroupMembers} people.");
        }

        var now = DateTime.UtcNow;
        foreach (var userId in toAdd)
        {
            // Someone who left and is being re-added has a row already — revive it rather than
            // inserting a second one, which the unique index would reject anyway.
            var existing = await _db.ConversationParticipants
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId, ct);

            if (existing is not null)
            {
                existing.LeftDate = null;
                existing.JoinedDate = now;
            }
            else
            {
                _db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conversationId,
                    UserId = userId,
                    JoinedDate = now,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var userId in toAdd)
        {
            await _messages.WriteSystemMessageAsync(conversationId, ChatSystemEvent.Joined, userId, ct);
        }

        await PushUpsertToMembersAsync(conversationId, ct);

        return ChatResult.Ok();
    }

    public async Task<ChatResult> LeaveAsync(Guid callerId, Guid conversationId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(conversationId, callerId, ct);
        if (access is not { } a)
        {
            return ChatResult.Fail(ChatOutcome.NotFound);
        }

        if (!a.IsManualGroup)
        {
            // The whole point of FR-026: an auto chat follows the roster, and a DM is hidden or the
            // other person blocked. Neither is "left".
            return a.IsDirect
                ? ChatResult.Fail(ChatOutcome.Invalid, "You can hide this chat, or block the person.")
                : ChatResult.Fail(ChatOutcome.Invalid, "This chat follows the roster — you can mute or hide it instead.");
        }

        var row = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == callerId, ct);

        if (row is null || row.LeftDate is not null)
        {
            return ChatResult.Ok();
        }

        // Kept, not deleted: the leaver's past messages keep an attributable sender and the thread
        // stays coherent. A non-null LeftDate fails the membership check, so they read nothing more.
        row.LeftDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _messages.WriteSystemMessageAsync(conversationId, ChatSystemEvent.Left, callerId, ct);

        return ChatResult.Ok();
    }

    // --- Per-user flags (US5) ---------------------------------------------------

    public async Task<ChatResult> PatchStateAsync(
        Guid callerId,
        Guid conversationId,
        bool? isMuted,
        bool? isHidden,
        CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(conversationId, callerId, ct);
        if (access is null)
        {
            return ChatResult.Fail(ChatOutcome.NotFound);
        }

        var state = await _guard.EnsureParticipantStateAsync(conversationId, callerId, ct);

        if (isMuted is { } m)
        {
            state.IsMuted = m;
        }

        if (isHidden is { } h)
        {
            state.IsHidden = h;
        }

        await _db.SaveChangesAsync(ct);

        // Muting/hiding changes the badge immediately — push so the caller's other tabs agree.
        await _realtime.PushUnreadCountAsync(callerId, await GetUnreadTotalAsync(callerId, ct), ct);

        return ChatResult.Ok();
    }

    // --- Auto chats (US4) -------------------------------------------------------

    public Task<Guid> EnsureForTeamAsync(Guid teamId, CancellationToken ct = default) =>
        EnsureAutoAsync(ConversationKind.Team, teamId, ct);

    public Task<Guid> EnsureForPartyAsync(Guid partyId, CancellationToken ct = default) =>
        EnsureAutoAsync(ConversationKind.Party, partyId, ct);

    /// <summary>
    /// Materialise a team's/party's chat on first sight. This is the whole of FR-024's "backfill":
    /// no migration writes rows for teams that may never chat — the first roster member to open Chat
    /// creates it (research §4).
    /// </summary>
    private async Task<Guid> EnsureAutoAsync(ConversationKind kind, Guid ownerId, CancellationToken ct)
    {
        var existing = await FindAutoAsync(kind, ownerId, ct);
        if (existing != Guid.Empty)
        {
            return existing;
        }

        var conversation = new Conversation
        {
            Kind = kind,
            TeamId = kind == ConversationKind.Team ? ownerId : null,
            PartyId = kind == ConversationKind.Party ? ownerId : null,
            State = ConversationState.Active,
        };
        _db.Conversations.Add(conversation);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two roster members opened Chat at the same moment. The unique filtered index on
            // TeamId/PartyId caught the loser — which is exactly why the constraint exists rather than
            // a check-then-insert that can interleave.
            _db.ChangeTracker.Clear();
            var winner = await FindAutoAsync(kind, ownerId, ct);
            if (winner == Guid.Empty)
            {
                throw;
            }

            return winner;
        }

        return conversation.Id;
    }

    private async Task<Guid> FindAutoAsync(ConversationKind kind, Guid ownerId, CancellationToken ct) =>
        await _db.Conversations.AsNoTracking()
            .Where(c => kind == ConversationKind.Team ? c.TeamId == ownerId : c.PartyId == ownerId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Archive a team's chat AND every inquiry thread addressed to that team, before the team row is
    /// hard-deleted (data-model R3a). Both derive membership from <c>TeamMemberships</c>, which cascades
    /// away with the team, so both must be snapshotted first.
    /// </summary>
    public async Task ArchiveForTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var ids = await _db.Conversations.AsNoTracking()
            .Where(c => c.TeamId == teamId
                && (c.Kind == ConversationKind.Team || c.Kind == ConversationKind.TeamInquiry))
            .Select(c => c.Id)
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            await ArchiveConversationAsync(id, ct);
        }
    }

    public Task ArchiveForPartyAsync(Guid partyId, CancellationToken ct = default) =>
        ArchiveAutoAsync(ConversationKind.Party, partyId, ct);

    /// <summary>
    /// Archive every inquiry thread addressed to an event when the event is cancelled (feature 027).
    /// Unlike a team delete, cancellation does not remove the <c>EventAdmin</c> roster, but archiving
    /// still snapshots it so the "archived = detached, read-only" invariant holds uniformly (research §8).
    /// </summary>
    public async Task ArchiveInquiriesForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var ids = await _db.Conversations.AsNoTracking()
            .Where(c => c.EventId == eventId && c.Kind == ConversationKind.EventInquiry)
            .Select(c => c.Id)
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            await ArchiveConversationAsync(id, ct);
        }
    }

    private async Task ArchiveAutoAsync(ConversationKind kind, Guid ownerId, CancellationToken ct)
    {
        var conversationId = await FindAutoAsync(kind, ownerId, ct);
        if (conversationId == Guid.Empty)
        {
            return; // Nobody ever opened the chat, so there is nothing to preserve.
        }

        await ArchiveConversationAsync(conversationId, ct);
    }

    /// <summary>
    /// Snapshot a derived-membership conversation and close it, <b>before</b> its team/party/event link
    /// can be hard-deleted (data-model R3a).
    /// </summary>
    /// <remarks>
    /// This is a <b>snapshot, not a flag</b>, and the distinction is the whole point. Team delete and
    /// party disband are hard deletes, and <c>TeamMemberships</c>/<c>PartyMembers</c> cascade away with
    /// them. Since a live auto chat / inquiry <em>derives</em> membership from that roster, simply setting
    /// <c>State = Archived</c> would leave a conversation nobody can read — the roster it asks "are you
    /// a member?" no longer exists — silently breaking FR-027's "members can still read the history".
    /// So: freeze the roster into real participant rows, freeze the name, drop the link. One-way and
    /// idempotent. Works uniformly for <see cref="ConversationKind.Team"/>, <see cref="ConversationKind.Party"/>,
    /// and the two inquiry kinds (feature 027).
    /// </remarks>
    private async Task ArchiveConversationAsync(Guid conversationId, CancellationToken ct)
    {
        var conversation = await _db.Conversations.FirstAsync(c => c.Id == conversationId, ct);
        if (conversation.State == ConversationState.Archived)
        {
            return; // One-way, and idempotent.
        }

        // 1. Freeze the derived roster into stored membership, while the roster still exists.
        var rosterUserIds = await _guard.ResolveParticipantUserIdsAsync(conversationId, ct);
        var haveRows = await _db.ConversationParticipants
            .Where(p => p.ConversationId == conversationId)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var userId in rosterUserIds)
        {
            var row = haveRows.FirstOrDefault(p => p.UserId == userId);
            if (row is null)
            {
                _db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conversationId,
                    UserId = userId,
                    JoinedDate = now,
                });
            }
            else
            {
                row.LeftDate = null;
            }
        }

        // 2. Freeze the display name — the link it was derived from is about to vanish. An inquiry's
        //    per-viewer name collapses to a single stable label (what it was about) once archived.
        conversation.Name ??= conversation.Kind switch
        {
            ConversationKind.Team or ConversationKind.TeamInquiry => conversation.TeamId is { } tId
                ? await _db.Teams.AsNoTracking().Where(t => t.Id == tId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? "Team chat"
                : "Team chat",
            ConversationKind.EventInquiry => conversation.EventId is { } eId
                ? await _db.Events.AsNoTracking().Where(e => e.Id == eId).Select(e => e.Name).FirstOrDefaultAsync(ct) ?? "Event"
                : "Event",
            _ => "Party chat",
        };

        // 3. Drop the links so a subsequent hard delete is not blocked by the Restrict FKs…
        conversation.TeamId = null;
        conversation.PartyId = null;
        conversation.EventId = null;

        // 4. …and close it to writes. Kind is deliberately left alone, so the inbox still tags it and
        //    the history still reads as what it was.
        conversation.State = ConversationState.Archived;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Push a refreshed inbox row to every current member (each gets their own projection).</summary>
    private async Task PushUpsertToMembersAsync(Guid conversationId, CancellationToken ct)
    {
        var members = await _guard.ResolveParticipantUserIdsAsync(conversationId, ct);
        foreach (var memberId in members)
        {
            var summary = await SummariseAsync(memberId, conversationId, ct);
            if (summary.IsOk && summary.Value is not null)
            {
                await _realtime.PushConversationUpsertedAsync(new[] { memberId }, summary.Value, ct);
            }
        }
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
    /// <remarks>
    /// Inquiry threads (feature 027) name themselves <b>per viewer</b>: the requester sees the team
    /// name / event title (what they're asking about); an admin sees the requester's name <em>and</em>
    /// the team/event it concerns — e.g. "Ada K. · Rheinfeuer" — because an admin of several teams/events
    /// needs the context to tell inquiries apart. A frozen <paramref name="stored"/> name (set at
    /// archival, when the link is severed) wins for every derived kind.
    /// </remarks>
    private static string DisplayName(
        ConversationKind kind,
        string? stored,
        string? teamName,
        string? otherName,
        string? eventName,
        string? requesterName,
        bool isRequester) =>
        kind switch
        {
            ConversationKind.Group => stored ?? "Group",
            ConversationKind.Direct => otherName ?? PlaceholderName,
            ConversationKind.Team => stored ?? teamName ?? "Team chat",
            ConversationKind.Party => stored ?? "Party chat",
            ConversationKind.TeamInquiry => stored ?? (isRequester ? teamName : InquiryAdminLabel(requesterName, teamName)) ?? PlaceholderName,
            ConversationKind.EventInquiry => stored ?? (isRequester ? eventName : InquiryAdminLabel(requesterName, eventName)) ?? PlaceholderName,
            _ => stored ?? "Chat",
        };

    /// <summary>
    /// The admin-side label for an inquiry row: the requester's name plus the team/event it concerns,
    /// so an admin who manages several can tell them apart (feature 027). Degrades gracefully when
    /// either part is missing.
    /// </summary>
    private static string InquiryAdminLabel(string? requesterName, string? context) =>
        (requesterName, context) switch
        {
            ({ } r, { } c) => $"{r} · {c}",
            ({ } r, null) => r,
            (null, { } c) => c,
            _ => PlaceholderName,
        };

    private static ConversationAvatarDto BuildAvatar(
        ConversationKind kind,
        Guid? teamId,
        Guid? otherUserId,
        Guid? requesterUserId,
        bool isRequester) =>
        kind switch
        {
            ConversationKind.Direct => new ConversationAvatarDto("User", otherUserId, null, null),
            ConversationKind.Team => new ConversationAvatarDto("Team", null, teamId, null),
            ConversationKind.Party => new ConversationAvatarDto("Party", null, null, null),
            // Requester sees the team/event crest; an admin sees the requester's avatar.
            ConversationKind.TeamInquiry => isRequester
                ? new ConversationAvatarDto("Team", null, teamId, null)
                : new ConversationAvatarDto("User", requesterUserId, null, null),
            ConversationKind.EventInquiry => isRequester
                ? new ConversationAvatarDto("Event", null, null, null)
                : new ConversationAvatarDto("User", requesterUserId, null, null),
            _ => new ConversationAvatarDto("Group", null, null, null),
        };
}
