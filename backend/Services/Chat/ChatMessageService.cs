using JuggerHub.Data;
using JuggerHub.Dtos.Chat;
using JuggerHub.Entities;
using JuggerHub.Services.Chat.Realtime;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Messages: send, read history, delete your own (feature 019).
/// </summary>
/// <remarks>
/// Realtime pushes here are strictly <b>after</b> the durable save and never in place of it: every
/// event has a REST equivalent returning the same truth, so a disconnected player is stale, never
/// wrong (spec FR-023). A failed push must never fail the send.
/// </remarks>
public sealed class ChatMessageService : IChatMessageService
{
    private readonly AppDbContext _db;
    private readonly ChatGuard _guard;
    private readonly IChatRealtime _realtime;
    private readonly ChatLinkResolver _links;
    private readonly IReadOnlyCollection<string> _allowedHosts;

    public ChatMessageService(
        AppDbContext db,
        ChatGuard guard,
        IChatRealtime realtime,
        ChatLinkResolver links,
        IConfiguration configuration)
    {
        _db = db;
        _guard = guard;
        _realtime = realtime;
        _links = links;

        // Which hosts count as "us" for unfurl. Derived from the frontend base URL the email templates
        // already use, so there is one source of truth for "where does JuggerHub live".
        var frontendBase = configuration["Email:FrontendBaseUrl"];
        var hosts = new List<string> { "localhost" };
        if (Uri.TryCreate(frontendBase, UriKind.Absolute, out var uri))
        {
            hosts.Add(uri.Host);
        }

        _allowedHosts = hosts;
    }

    // --- Send -------------------------------------------------------------------

    public async Task<ChatResult<MessageDto>> SendAsync(
        Guid callerId,
        Guid conversationId,
        string body,
        CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(conversationId, callerId, ct);
        if (access is not { } a)
        {
            return ChatResult<MessageDto>.Fail(ChatOutcome.NotFound);
        }

        if (a.IsArchived)
        {
            return ChatResult<MessageDto>.Fail(ChatOutcome.Conflict, "This chat is closed.");
        }

        var trimmed = body?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return ChatResult<MessageDto>.Fail(ChatOutcome.Invalid, "Write a message first.");
        }

        if (trimmed.Length > ChatConstants.MaxMessageLength)
        {
            return ChatResult<MessageDto>.Fail(
                ChatOutcome.Invalid,
                $"Messages can be up to {ChatConstants.MaxMessageLength} characters.");
        }

        // A block closes an existing DM as well as a new one — otherwise blocking would only stop
        // strangers, not the person actually bothering you (spec FR-031).
        if (a.IsDirect && await IsBlockedInDirectAsync(conversationId, callerId, ct))
        {
            return ChatResult<MessageDto>.Fail(ChatOutcome.Forbidden, "You can't message this player.");
        }

        // Parse a JuggerHub link out of the body — pattern-matching our own routes, never fetching
        // (spec FR-042). Only the kind and target id are stored; the card itself is built per viewer
        // at read time, which is what keeps a team-only training from leaking into a DM (FR-040).
        var parsed = ChatLinkParser.Parse(trimmed, _allowedHosts);
        var linkTargetId = parsed.Kind == ChatLinkKind.None
            ? null
            : await _links.ResolveTargetIdAsync(parsed, ct);

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = callerId,
            Kind = ChatMessageKind.Member,
            Body = trimmed,
            // A link whose target does not exist stays plain text rather than a broken card.
            LinkKind = linkTargetId is null ? ChatLinkKind.None : parsed.Kind,
            LinkTargetId = linkTargetId,
        };
        _db.ChatMessages.Add(message);

        // Denormalised so the inbox can order without a correlated subquery. Uses the entity's own
        // CreatedDate once the interceptor has stamped it.
        var conversation = await _db.Conversations.FirstAsync(c => c.Id == conversationId, ct);
        conversation.LastMessageDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var dto = await ProjectOneAsync(message.Id, callerId, ct);
        if (dto is null)
        {
            return ChatResult<MessageDto>.Fail(ChatOutcome.NotFound);
        }

        await PushMessageToOthersAsync(conversationId, callerId, message.Id, ct);

        return ChatResult<MessageDto>.Ok(dto);
    }

    /// <summary>
    /// Fan a new message out to the conversation's other members, and refresh each one's unread total.
    /// </summary>
    /// <remarks>
    /// The audience is resolved server-side from the roster/participants, never from client input
    /// (spec FR-022). The message is projected <em>per recipient</em> because <c>isOwn</c> and the
    /// read receipt differ by viewer — pushing the sender's projection to everyone would show the
    /// recipient their own message right-aligned.
    /// </remarks>
    private async Task PushMessageToOthersAsync(Guid conversationId, Guid senderId, Guid messageId, CancellationToken ct)
    {
        var recipients = (await _guard.ResolveParticipantUserIdsAsync(conversationId, ct))
            .Where(id => id != senderId)
            .ToList();

        if (recipients.Count == 0)
        {
            return;
        }

        foreach (var recipientId in recipients)
        {
            var forRecipient = await ProjectOneAsync(messageId, recipientId, ct);
            if (forRecipient is not null)
            {
                await _realtime.PushMessageCreatedAsync(new[] { recipientId }, conversationId, forRecipient, ct);
            }

            // The badge moves for the recipient too — otherwise a player with the app open but the
            // conversation closed would see the message appear with no badge to notice it by.
            await _realtime.PushUnreadCountAsync(recipientId, await UnreadTotalAsync(recipientId, ct), ct);
        }
    }

    /// <summary>
    /// The recipient's nav-badge total. Mirrors <c>ChatConversationService.GetUnreadTotalAsync</c>'s
    /// rule — non-muted, non-hidden, member-of — kept here rather than injecting the conversation
    /// service into the message service to avoid a circular dependency.
    /// </summary>
    private async Task<int> UnreadTotalAsync(Guid userId, CancellationToken ct)
    {
        var conversations = await _db.Conversations.AsNoTracking()
            .Where(c =>
                ((c.Kind == ConversationKind.Direct || c.Kind == ConversationKind.Group)
                    && c.Participants.Any(p => p.UserId == userId && p.LeftDate == null))
                || (c.Kind == ConversationKind.Team
                    && _db.TeamMemberships.Any(m => m.TeamId == c.TeamId && m.UserId == userId))
                || (c.Kind == ConversationKind.Party
                    && _db.PartyMembers.Any(pm => pm.PartyId == c.PartyId
                        && pm.UserId == userId
                        && pm.Status == PartyMemberStatus.In)))
            .Where(c => !c.Participants.Any(p => p.UserId == userId && (p.IsMuted || p.IsHidden)))
            .Select(c => new
            {
                c.Id,
                c.Kind,
                c.State,
                c.TeamId,
                c.PartyId,
                LastRead = c.Participants.Where(p => p.UserId == userId).Select(p => p.LastReadMessageId).FirstOrDefault(),
            })
            .ToListAsync(ct);

        // Messages from before the caller joined never count toward the badge (spec FR-051), the same
        // rule the inbox applies — resolved once, in batch, through the guard.
        var cutoffs = await _guard.ResolveJoinCutoffsAsync(
            userId,
            conversations.Select(c => new ChatAccess(c.Id, c.Kind, c.State, c.TeamId, c.PartyId)).ToList(),
            ct);

        var total = 0;
        foreach (var c in conversations)
        {
            var q = _db.ChatMessages.AsNoTracking()
                .Where(m => m.ConversationId == c.Id && m.SenderId != userId && !m.IsDeleted);

            if (cutoffs.GetValueOrDefault(c.Id) is { } joinedAt)
            {
                q = q.Where(m => m.CreatedDate >= joinedAt);
            }

            if (c.LastRead is { } lastRead)
            {
                q = q.Where(m => m.Id.CompareTo(lastRead) > 0);
            }

            total += await q.CountAsync(ct);
        }

        return total;
    }

    private Task<bool> IsBlockedInDirectAsync(Guid conversationId, Guid callerId, CancellationToken ct) =>
        _db.ConversationParticipants.AsNoTracking()
            .Where(p => p.ConversationId == conversationId && p.UserId != callerId)
            .AnyAsync(p => _db.UserBlocks.Any(b =>
                (b.BlockerUserId == callerId && b.BlockedUserId == p.UserId)
                || (b.BlockerUserId == p.UserId && b.BlockedUserId == callerId)), ct);

    // --- History ----------------------------------------------------------------

    public async Task<ChatResult<MessagePageDto>> GetPageAsync(
        Guid callerId,
        Guid conversationId,
        Guid? before,
        int take,
        CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(conversationId, callerId, ct);
        if (access is not { } a)
        {
            return ChatResult<MessagePageDto>.Fail(ChatOutcome.NotFound);
        }

        var size = take is <= 0 or > ChatConstants.MaxMessagePageSize
            ? ChatConstants.MessagePageSize
            : take;

        // Keyset, not skip/take: the cursor is the UUIDv7 id, which is monotonic, so a message
        // arriving mid-scroll cannot shift a later page (constitution III prefers this for large,
        // rapidly-changing tables).
        var q = _db.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        // Nothing from before the caller joined — being added to a team/party/group chat does not hand
        // you the backlog (spec FR-051). Null for archived chats and DMs, where full history is correct.
        var cutoff = await _guard.ResolveJoinCutoffAsync(a, callerId, ct);
        if (cutoff is { } joinedAt)
        {
            q = q.Where(m => m.CreatedDate >= joinedAt);
        }

        if (before is { } cursor)
        {
            q = q.Where(m => m.Id.CompareTo(cursor) < 0);
        }

        // Read the other side's marker once, for the Sent/Read receipt on the caller's own messages.
        // Only meaningful in a DM — group read receipts are out of scope.
        Guid? otherLastRead = a.IsDirect
            ? await _db.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == conversationId && p.UserId != callerId)
                .Select(p => p.LastReadMessageId)
                .FirstOrDefaultAsync(ct)
            : null;

        var rows = await q
            .OrderByDescending(m => m.Id)
            .Take(size + 1) // one extra to know whether another page exists
            .Select(m => new Row(
                m.Id,
                m.Kind,
                m.SenderId,
                m.Sender!.Profile!.DisplayName,
                m.Body,
                m.CreatedDate,
                m.IsDeleted,
                m.SystemEvent,
                m.SystemSubjectUserId,
                m.LinkKind,
                m.LinkTargetId))
            .ToListAsync(ct);

        Guid? nextBefore = null;
        if (rows.Count > size)
        {
            rows.RemoveAt(rows.Count - 1);
            nextBefore = rows[^1].Id;
        }

        var subjectNames = await ResolveSubjectNamesAsync(rows, ct);
        var cards = await ResolveCardsAsync(rows, callerId, ct);

        var items = rows
            .Select(r => ToDto(r, callerId, otherLastRead, subjectNames, cards))
            .ToList();

        return ChatResult<MessagePageDto>.Ok(new MessagePageDto(items, nextBefore));
    }

    /// <summary>
    /// Build the link cards for a page, batched per kind — and resolved for <paramref name="viewerId"/>,
    /// never for the sender (spec FR-040).
    /// </summary>
    private async Task<Dictionary<Guid, LinkCardDto>> ResolveCardsAsync(
        IReadOnlyCollection<Row> rows,
        Guid viewerId,
        CancellationToken ct)
    {
        var links = rows
            .Where(r => r.LinkKind != ChatLinkKind.None && r.LinkTargetId is not null && !r.IsDeleted)
            .Select(r => (r.Id, r.LinkKind, r.LinkTargetId!.Value))
            .ToList();

        return links.Count == 0
            ? new Dictionary<Guid, LinkCardDto>()
            : await _links.ResolveManyAsync(links, viewerId, ct);
    }

    private async Task<MessageDto?> ProjectOneAsync(Guid messageId, Guid callerId, CancellationToken ct)
    {
        var row = await _db.ChatMessages.AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(m => new Row(
                m.Id,
                m.Kind,
                m.SenderId,
                m.Sender!.Profile!.DisplayName,
                m.Body,
                m.CreatedDate,
                m.IsDeleted,
                m.SystemEvent,
                m.SystemSubjectUserId,
                m.LinkKind,
                m.LinkTargetId))
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var subjectNames = await ResolveSubjectNamesAsync(new[] { row }, ct);
        var cards = await ResolveCardsAsync(new[] { row }, callerId, ct);
        return ToDto(row, callerId, null, subjectNames, cards);
    }

    private async Task<Dictionary<Guid, string>> ResolveSubjectNamesAsync(
        IReadOnlyCollection<Row> rows,
        CancellationToken ct)
    {
        var ids = rows
            .Where(r => r.SystemSubjectUserId is not null)
            .Select(r => r.SystemSubjectUserId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _db.PlayerProfiles.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
    }

    private static MessageDto ToDto(
        Row r,
        Guid callerId,
        Guid? otherLastRead,
        IReadOnlyDictionary<Guid, string> subjectNames,
        IReadOnlyDictionary<Guid, LinkCardDto> cards)
    {
        var isOwn = r.SenderId == callerId;

        string? readState = null;
        if (isOwn && !r.IsDeleted && r.Kind == ChatMessageKind.Member)
        {
            // Read iff the other participant's marker has reached this message. UUIDv7 ordering makes
            // that a straight comparison (research §3).
            readState = otherLastRead is { } lr && lr.CompareTo(r.Id) >= 0 ? "Read" : "Sent";
        }

        return new MessageDto(
            r.Id,
            r.Kind,
            r.SenderId,
            // A soft-deleted or banned account's profile is hidden by a global query filter
            // (feature 013), so DisplayName projects to null here rather than the row being absent.
            // Their past messages must still read coherently, so they get a neutral placeholder
            // instead of a blank or a crash — history is preserved, not rewritten.
            isOwn || r.Kind == ChatMessageKind.System
                ? null
                : r.SenderDisplayName ?? ChatConversationService.PlaceholderName,
            isOwn,
            r.IsDeleted ? string.Empty : r.Body,
            r.CreatedDate,
            r.IsDeleted,
            readState,
            r.SystemEvent,
            r.SystemSubjectUserId is { } sid
                ? subjectNames.GetValueOrDefault(sid, ChatConversationService.PlaceholderName)
                : null,
            // Null ⇒ the client renders the body's link as plain text. That covers three cases the
            // viewer cannot tell apart, by design: no link, a target they may not see (FR-040), and a
            // target that no longer exists (FR-041).
            cards.GetValueOrDefault(r.Id));
    }

    // --- Delete -----------------------------------------------------------------

    public async Task<ChatResult> DeleteAsync(Guid callerId, Guid messageId, CancellationToken ct = default)
    {
        var message = await _db.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);

        if (message is null)
        {
            return ChatResult.Fail(ChatOutcome.NotFound);
        }

        // Resolve membership first: a non-member must not be able to tell a real message id from a
        // made-up one, so they get 404 before any 403 logic runs (spec FR-048).
        var access = await _guard.ResolveAsync(message.ConversationId, callerId, ct);
        if (access is null)
        {
            return ChatResult.Fail(ChatOutcome.NotFound);
        }

        if (message.Kind == ChatMessageKind.System)
        {
            return ChatResult.Fail(ChatOutcome.Forbidden, "System messages can't be deleted.");
        }

        // Sender only. A member of the conversation who did not write it gets 403 — they can see it,
        // so denying existence would be a lie; they simply may not do this (spec FR-050a).
        if (message.SenderId != callerId)
        {
            return ChatResult.Fail(ChatOutcome.Forbidden, "You can only delete your own messages.");
        }

        if (message.IsDeleted)
        {
            return ChatResult.Ok();
        }

        // The content is genuinely cleared, not just flagged — a flag would leave the body in the row
        // for any query that forgot to check it, and "deleted" would be a rendering convention rather
        // than a fact. The row survives only to hold its place in the order (data-model R12).
        message.IsDeleted = true;
        message.Body = string.Empty;
        message.LinkKind = ChatLinkKind.None;
        message.LinkTargetId = null;

        await _db.SaveChangesAsync(ct);

        // Everyone, including the sender: their other tabs must swap the bubble for a tombstone too.
        var recipients = await _guard.ResolveParticipantUserIdsAsync(message.ConversationId, ct);
        if (recipients.Count > 0)
        {
            await _realtime.PushMessageDeletedAsync(recipients, message.ConversationId, message.Id, ct);
        }

        return ChatResult.Ok();
    }

    // --- System lines -----------------------------------------------------------

    public async Task WriteSystemMessageAsync(
        Guid conversationId,
        ChatSystemEvent systemEvent,
        Guid subjectUserId,
        CancellationToken ct = default)
    {
        _db.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversationId,
            // Null sender is what makes a system line unforgeable by a member (data-model R13).
            SenderId = null,
            Kind = ChatMessageKind.System,
            Body = string.Empty,
            SystemEvent = systemEvent,
            SystemSubjectUserId = subjectUserId,
        });

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Flat projection row; kept private so the query shape stays in one place.</summary>
    private sealed record Row(
        Guid Id,
        ChatMessageKind Kind,
        Guid? SenderId,
        string? SenderDisplayName,
        string Body,
        DateTime CreatedDate,
        bool IsDeleted,
        ChatSystemEvent? SystemEvent,
        Guid? SystemSubjectUserId,
        ChatLinkKind LinkKind,
        Guid? LinkTargetId);
}
