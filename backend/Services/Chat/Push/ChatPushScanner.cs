using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Entities;
using JuggerHub.Services.Notifications;
using JuggerHub.Services.Notifications.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Chat.Push;

/// <inheritdoc cref="IChatPushScanner" />
public sealed class ChatPushScanner : IChatPushScanner
{
    private readonly AppDbContext _db;
    private readonly ChatGuard _guard;
    private readonly IChatPushComposer _composer;
    private readonly IPushDispatcher _dispatcher;
    private readonly INotificationPreferenceService _preferences;
    private readonly ChatPushOptions _options;
    private readonly ILogger<ChatPushScanner> _logger;

    public ChatPushScanner(
        AppDbContext db,
        ChatGuard guard,
        IChatPushComposer composer,
        IPushDispatcher dispatcher,
        INotificationPreferenceService preferences,
        IOptions<ChatPushOptions> options,
        ILogger<ChatPushScanner> logger)
    {
        _db = db;
        _guard = guard;
        _composer = composer;
        _dispatcher = dispatcher;
        _preferences = preferences;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var quiet = now.AddSeconds(-_options.QuietDelaySeconds);

        // --- Select ----------------------------------------------------------------------------
        //
        // Member messages nobody has looked at yet, old enough that reading them would already
        // have happened. System lines are excluded HERE rather than by a later branch, which is
        // what makes FR-015 ("a joined/left/archived line never notifies anyone") structural: a
        // future widening of this predicate would have to repeal it deliberately.
        //
        // There is NO lower age bound in this query. Max age is a dispatch filter further down,
        // because a message that is never selected is never marked — and an unmarked message stays
        // in the partial index behind this query for ever.
        var candidates = await _db.ChatMessages.AsNoTracking()
            .Where(m => m.PushConsideredAt == null
                && m.Kind == ChatMessageKind.Member
                && m.CreatedDate <= quiet)
            .OrderBy(m => m.Id)
            .Take(_options.MaxMessagesPerPass)
            .Select(m => new Candidate(m.Id, m.ConversationId, m.SenderId, m.CreatedDate, m.IsDeleted))
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return 0;
        }

        // --- Claim -----------------------------------------------------------------------------
        //
        // Every replica runs this pass, so two can select the same rows. Claiming them before
        // dispatching narrows that to an interleave, and the interleave is harmless: both
        // notifications carry the same collapse tag, so the second replaces the first on the
        // device and the member sees one. The cost of losing the race is one extra outbound call.
        //
        // ExecuteUpdateAsync bypasses the change tracker, so AuditFieldsInterceptor does not run
        // and ModifiedDate is set here (constitution III).
        var ids = candidates.Select(c => c.Id).ToList();
        var claimed = await _db.ChatMessages
            .Where(m => ids.Contains(m.Id) && m.PushConsideredAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.PushConsideredAt, now)
                      .SetProperty(m => m.ModifiedDate, now),
                ct);

        if (claimed == 0)
        {
            // Another replica took the whole batch between the select and here.
            return 0;
        }

        // --- Decide what is worth a notification -----------------------------------------------
        //
        // Everything selected is now marked, including what is dropped here. That is the rule the
        // partial index depends on.
        var oldest = now.AddMinutes(-_options.MaxMessageAgeMinutes);
        var eligible = candidates
            .Where(c => !c.IsDeleted                 // FR-016: withdrawn before anyone was told
                && c.SenderId is not null            // a member message always has one; defensive
                && c.CreatedDate >= oldest)          // FR-025: no flood after an interruption
            .ToList();

        // One notification per conversation, about the NEWEST message the recipient has not read.
        // Safe because the read marker is a monotonic UUIDv7 cursor: "read the newest but not an
        // older one" cannot happen. This is what stops four messages costing four dispatches.
        var newestPerConversation = eligible
            .GroupBy(c => c.ConversationId)
            .Select(g => g.MaxBy(c => c.Id)!)
            .ToList();

        foreach (var message in newestPerConversation)
        {
            try
            {
                await NotifyAsync(message, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One conversation's failure must not end the pass, and none of this may reach the
                // member who sent the message — they sent it successfully a minute ago.
                //
                // Ids only. Never the message, never a name, never the conversation's name: this
                // feature is the one that carries member-written content, so its logs are held to
                // a stricter line than the rest of the platform (FR-021c).
                _logger.LogWarning(
                    ex,
                    "Chat push failed for conversation {ConversationId}.",
                    message.ConversationId);
            }
        }

        return candidates.Count;
    }

    /// <summary>
    /// Works out who should hear about one message, and tells them.
    /// </summary>
    private async Task NotifyAsync(Candidate message, CancellationToken ct)
    {
        var conversation = await _db.Conversations.AsNoTracking()
            .Where(c => c.Id == message.ConversationId)
            .Select(c => new
            {
                c.Id,
                c.Kind,
                c.State,
                c.TeamId,
                c.PartyId,
                c.EventId,
                c.RequesterUserId,
                c.Name,
                TeamName = c.Team!.Name,
                EventName = c.Event!.Name,
                // Read through PlayerProfiles rather than the User.Profile navigation: the ban
                // query filter on that DbSet makes a banned member's name resolve to nothing for
                // free, and the navigation misbehaves against it (feature 044's lesson).
                RequesterName = _db.PlayerProfiles
                    .Where(p => p.UserId == c.RequesterUserId)
                    .Select(p => p.DisplayName)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        // FR-017: a closed conversation notifies nobody. Its members can still read it; there is
        // simply nothing new to come.
        if (conversation is null || conversation.State == ConversationState.Archived)
        {
            return;
        }

        var access = new ChatAccess(
            conversation.Id,
            conversation.Kind,
            conversation.State,
            conversation.TeamId,
            conversation.PartyId,
            conversation.EventId,
            conversation.RequesterUserId);

        // Resolved server-side from the roster, never from anything a client said, and handling
        // all six kinds plus the archived-snapshot case. Never a second membership oracle.
        var audience = (await _guard.ResolveParticipantUserIdsAsync(message.ConversationId, ct))
            .Where(id => id != message.SenderId)   // FR-008: never your own message
            .Distinct()
            .ToList();

        if (audience.Count == 0)
        {
            return;
        }

        var recipients = await EligibleAsync(access, message, audience, ct);
        if (recipients.Count == 0)
        {
            return;
        }

        var sender = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == message.SenderId)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(ct);

        var body = await _db.ChatMessages.AsNoTracking()
            .Where(m => m.Id == message.Id)
            .Select(m => new { m.BodyCipher, HasAttachments = m.Attachments.Any() })
            .FirstOrDefaultAsync(ct);

        if (body is null)
        {
            return;
        }

        var subject = new ChatPushConversation(
            conversation.Id,
            conversation.Kind,
            conversation.Name,
            conversation.TeamName,
            conversation.EventName,
            conversation.RequesterName,
            conversation.RequesterUserId);

        var content = new ChatPushMessage(message.Id, sender, body.BodyCipher, body.HasAttachments);

        // The language is the RECIPIENT's stored preference. There is no request culture to fall
        // back on — this is a background pass — and the sender's language is irrelevant to whoever
        // is reading. Composed once per language rather than once per person, the same shape
        // PushFanOut already uses.
        var languages = await _db.Users.AsNoTracking()
            .Where(u => recipients.Contains(u.Id))
            .Select(u => new { u.Id, u.PreferredLanguage })
            .ToListAsync(ct);

        foreach (var group in languages.GroupBy(u => SupportedLanguages.ResolveOrDefault(u.PreferredLanguage)))
        {
            // Only an inquiry names itself differently for different viewers, and then only for
            // its one requester — so the group splits at most once more, never per person.
            foreach (var bySide in group.GroupBy(u => u.Id == conversation.RequesterUserId))
            {
                var composed = _composer.Compose(subject, content, group.Key, bySide.Key);
                await _dispatcher.DispatchAsync(bySide.Select(u => u.Id).ToList(), composed, ct);
            }
        }
    }

    /// <summary>
    /// Narrows the conversation's audience to the members who should actually be disturbed.
    /// </summary>
    /// <remarks>
    /// Every clause here is a promise the product already made somewhere else, and each one fails
    /// silently if it is dropped: the symptom is a notification somebody was not supposed to get,
    /// seen only by them.
    /// </remarks>
    private async Task<List<Guid>> EligibleAsync(
        ChatAccess access,
        Candidate message,
        List<Guid> audience,
        CancellationToken ct)
    {
        // Participant rows are created lazily for Team/Party chats, so most members of a busy team
        // chat have no row at all. A missing row means: read nothing, muted nothing, hid nothing,
        // has not left — which is exactly the default this leaves them at.
        var state = await _db.ConversationParticipants.AsNoTracking()
            .Where(p => p.ConversationId == access.ConversationId && audience.Contains(p.UserId))
            .Select(p => new { p.UserId, p.LastReadMessageId, p.IsMuted, p.IsHidden, p.LeftDate })
            .ToListAsync(ct);

        var byUser = state.ToDictionary(s => s.UserId);

        var remaining = audience.Where(id =>
        {
            if (!byUser.TryGetValue(id, out var s))
            {
                return true;
            }

            // FR-010 — MUTE IS THE ONLY LEVER THAT SUPPRESSES A CHAT NOTIFICATION, and feature 048
            // made it the product's named answer to "I want to stay on this team but I don't want
            // this chat bothering me". People are relying on it.
            if (s.IsMuted)
            {
                return false;
            }

            // FR-011 — and this one needs its reason written down or it reads as dead code.
            // A member-written message ALREADY clears IsHidden for everyone, through
            // ChatMessageService.ReturnToArchiversInboxesAsync (feature 048, FR-007). By the time
            // this runs, the flag is false for anybody who had archived the conversation before the
            // message arrived. So this matches in exactly one situation: the member archived it
            // DURING the quiet delay — which is the moment they said they did not want to hear
            // about it. Hide is not a general suppressor here, and must not be tested as one.
            if (s.IsHidden)
            {
                return false;
            }

            // FR-012: a group's leaver keeps their row so their past messages stay attributable.
            if (s.LeftDate is not null)
            {
                return false;
            }

            // FR-009: already read it. The ids are UUIDv7, so this comparison is chronological.
            return s.LastReadMessageId is not { } lastRead || message.Id.CompareTo(lastRead) > 0;
        }).ToList();

        if (remaining.Count == 0)
        {
            return remaining;
        }

        // FR-014: being added to a chat does not hand you the backlog from before you were there.
        var cutoffs = await _guard.ResolveJoinCutoffsForMembersAsync(access, remaining, ct);
        remaining = remaining
            .Where(id => cutoffs.GetValueOrDefault(id) is not { } joined || message.CreatedDate >= joined)
            .ToList();

        // FR-013: blocks are stored one way and enforced both ways. Only a direct conversation can
        // hold two people who have blocked each other — a team chat's membership is its roster.
        if (access.IsDirect)
        {
            var survivors = new List<Guid>(remaining.Count);
            foreach (var id in remaining)
            {
                if (!await _guard.IsBlockedBetweenAsync(message.SenderId!.Value, id, ct))
                {
                    survivors.Add(id);
                }
            }

            remaining = survivors;
        }

        if (remaining.Count == 0)
        {
            return remaining;
        }

        // FR-027 — THE OFF SWITCH, AND IT HAS TO BE APPLIED HERE.
        //
        // IPushDispatcher filters no preferences: NotificationService reads them itself before
        // calling its fan-out, and chat reaches the dispatcher directly. Without this call the
        // Chat toggle in settings stores a value that nothing ever consults, and every test of the
        // happy path still passes.
        return (await _preferences.GetEnabledRecipientsAsync(
            remaining, NotificationCategory.Chat, NotificationChannel.Push, ct)).ToList();
    }

    /// <summary>What the selection query reads. Deliberately no message body: only one message per conversation needs one.</summary>
    private sealed record Candidate(
        Guid Id,
        Guid ConversationId,
        Guid? SenderId,
        DateTime CreatedDate,
        bool IsDeleted);
}
