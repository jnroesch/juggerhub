using JuggerHub.Data;
using JuggerHub.Dtos.Chat;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Messages: send, read history, delete your own (feature 019).
/// </summary>
public sealed class ChatMessageService : IChatMessageService
{
    private readonly AppDbContext _db;
    private readonly ChatGuard _guard;

    public ChatMessageService(AppDbContext db, ChatGuard guard)
    {
        _db = db;
        _guard = guard;
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

        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = callerId,
            Kind = ChatMessageKind.Member,
            Body = trimmed,
        };
        _db.ChatMessages.Add(message);

        // Denormalised so the inbox can order without a correlated subquery. Uses the entity's own
        // CreatedDate once the interceptor has stamped it.
        var conversation = await _db.Conversations.FirstAsync(c => c.Id == conversationId, ct);
        conversation.LastMessageDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var dto = await ProjectOneAsync(message.Id, callerId, ct);
        return dto is null
            ? ChatResult<MessageDto>.Fail(ChatOutcome.NotFound)
            : ChatResult<MessageDto>.Ok(dto);
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

        var items = rows
            .Select(r => ToDto(r, callerId, otherLastRead, subjectNames))
            .ToList();

        return ChatResult<MessagePageDto>.Ok(new MessagePageDto(items, nextBefore));
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
        return ToDto(row, callerId, null, subjectNames);
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
        IReadOnlyDictionary<Guid, string> subjectNames)
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
            // Link cards are resolved per viewer in a later slice (US7). Until then a link simply
            // renders as text in the body, which is the correct fallback anyway (spec FR-039).
            null);
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
