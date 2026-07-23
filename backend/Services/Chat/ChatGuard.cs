using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Chat;

/// <summary>A caller's resolved access to a single conversation (feature 019, extended feature 027).</summary>
public readonly record struct ChatAccess(
    Guid ConversationId,
    ConversationKind Kind,
    ConversationState State,
    Guid? TeamId,
    Guid? PartyId,
    Guid? EventId = null,
    Guid? RequesterUserId = null)
{
    /// <summary>Archived conversations are readable but closed to writes (spec FR-027).</summary>
    public bool IsArchived => State == ConversationState.Archived;

    /// <summary>Only manually-created groups can be added to or left (spec FR-026, FR-044).</summary>
    public bool IsManualGroup => Kind == ConversationKind.Group;

    /// <summary>Blocks apply to direct conversations and nothing else (spec FR-032).</summary>
    public bool IsDirect => Kind == ConversationKind.Direct;

    /// <summary>A "contact the admins" thread for a team or event (feature 027). Mirrored membership.</summary>
    public bool IsInquiry => Kind is ConversationKind.TeamInquiry or ConversationKind.EventInquiry;
}

/// <summary>
/// The single home of chat's membership predicate (feature 019, constitution Principle I). Every read,
/// every send, every search scope and every realtime fan-out resolves access through this class, so
/// "who may see this conversation" is answered in exactly one place rather than re-implemented at each
/// call site.
/// </summary>
/// <remarks>
/// <para>
/// <b>Membership is derived, not stored, for team and party chats.</b> Direct/Group membership is a
/// <see cref="ConversationParticipant"/> row; Team/Party membership is a live query against
/// <see cref="TeamMembership"/>/<see cref="PartyMember"/>. That asymmetry is the point: the dangerous
/// failure in this feature is a player who left a team still reading its chat, and deriving membership
/// from the roster makes that unrepresentable — there is no copy to drift, no event handler to miss,
/// no transaction that can half-apply (spec FR-025). It costs a join per request and is worth it.
/// </para>
/// <para>
/// <b>A non-member is indistinguishable from a non-existent conversation.</b> <see cref="ResolveAsync"/>
/// returns null for both, and callers map that to 404 — never 403, which would confirm existence
/// (spec FR-048). Mirrors <c>TrainingGuard</c>/<c>PartyGuard</c>.
/// </para>
/// </remarks>
public sealed class ChatGuard
{
    private readonly AppDbContext _db;

    public ChatGuard(AppDbContext db) => _db = db;

    /// <summary>
    /// <b>The membership rule, as a composable predicate.</b> "Is <paramref name="userId"/> a member of
    /// this conversation?"
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists so the rule has exactly one home. <see cref="ResolveAsync"/> answers it for a single
    /// conversation; the inbox and search need the same rule as a <c>Where</c> clause. An earlier
    /// version wrote it out separately in each place and the copies drifted — an archived chat listed in
    /// the inbox but 404'd when opened, because only one copy had learned about R3a. One expression,
    /// three call sites, no drift.
    /// </para>
    /// <para>
    /// Order matters: archived is checked first, because archiving snapshots the roster into participant
    /// rows and nulls the team/party link (data-model R3a), so the roster branches would find nothing.
    /// </para>
    /// </remarks>
    public static System.Linq.Expressions.Expression<Func<Conversation, bool>> IsMemberOf(AppDbContext db, Guid userId) =>
        c =>
            c.State == ConversationState.Archived
                ? c.Participants.Any(p => p.UserId == userId && p.LeftDate == null)
            : (c.Kind == ConversationKind.Direct || c.Kind == ConversationKind.Group)
                ? c.Participants.Any(p => p.UserId == userId && p.LeftDate == null)
            : c.Kind == ConversationKind.Team
                ? db.TeamMemberships.Any(m => m.TeamId == c.TeamId && m.UserId == userId)
            // Inquiry threads (feature 027): the fixed requester, or whoever currently administers the
            // target. Derived live, so a granted/revoked admin gains/loses the thread by construction.
            : c.Kind == ConversationKind.TeamInquiry
                ? c.RequesterUserId == userId
                    || db.TeamMemberships.Any(m => m.TeamId == c.TeamId && m.UserId == userId && m.Role == TeamRole.Admin)
            : c.Kind == ConversationKind.EventInquiry
                ? c.RequesterUserId == userId
                    || db.EventAdmins.Any(a => a.EventId == c.EventId && a.UserId == userId)
            : c.Kind == ConversationKind.Party
                && db.PartyMembers.Any(pm => pm.PartyId == c.PartyId
                    && pm.UserId == userId
                    && pm.Status == PartyMemberStatus.In);

    /// <summary>
    /// Resolve a caller's access to a conversation. Returns null when the conversation does not exist
    /// <em>or</em> the caller is not a member — the caller cannot tell which, by design.
    /// </summary>
    public async Task<ChatAccess?> ResolveAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var row = await _db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new
            {
                c.Id,
                c.Kind,
                c.State,
                c.TeamId,
                c.PartyId,
                c.EventId,
                c.RequesterUserId,
                // The membership predicate, evaluated in the database in the same round trip.
                // Each kind reads its own source of truth; the roster branches are what make
                // removal take effect immediately, with no sync step.
                //
                // ARCHIVED auto chats are checked FIRST, and must be: archiving snapshots the roster
                // into participant rows and nulls TeamId/PartyId/EventId (data-model R3a), precisely
                // because the team/party/event link may be about to be deleted. After that the roster
                // branches below would find nothing and lock every former member out of history they
                // are entitled to read (FR-027).
                IsMember =
                    c.State == ConversationState.Archived
                        ? c.Participants.Any(p => p.UserId == userId && p.LeftDate == null)
                    : (c.Kind == ConversationKind.Direct || c.Kind == ConversationKind.Group)
                        ? c.Participants.Any(p => p.UserId == userId && p.LeftDate == null)
                    : c.Kind == ConversationKind.Team
                        ? _db.TeamMemberships.Any(m => m.TeamId == c.TeamId && m.UserId == userId)
                    : c.Kind == ConversationKind.TeamInquiry
                        ? c.RequesterUserId == userId
                            || _db.TeamMemberships.Any(m => m.TeamId == c.TeamId && m.UserId == userId && m.Role == TeamRole.Admin)
                    : c.Kind == ConversationKind.EventInquiry
                        ? c.RequesterUserId == userId
                            || _db.EventAdmins.Any(a => a.EventId == c.EventId && a.UserId == userId)
                    : c.Kind == ConversationKind.Party
                        ? _db.PartyMembers.Any(pm => pm.PartyId == c.PartyId
                            && pm.UserId == userId
                            && pm.Status == PartyMemberStatus.In)
                        : false,
            })
            .FirstOrDefaultAsync(ct);

        if (row is null || !row.IsMember)
        {
            return null;
        }

        return new ChatAccess(row.Id, row.Kind, row.State, row.TeamId, row.PartyId, row.EventId, row.RequesterUserId);
    }

    /// <summary>
    /// The current participant user ids of a conversation — the fan-out list for realtime and the
    /// recipient set for unread. Resolved server-side from the same sources as
    /// <see cref="ResolveAsync"/>, so a client can never influence who receives an event (spec FR-022).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ResolveParticipantUserIdsAsync(
        Guid conversationId,
        CancellationToken ct = default)
    {
        var conversation = await _db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.Kind, c.State, c.TeamId, c.PartyId, c.EventId, c.RequesterUserId })
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            return Array.Empty<Guid>();
        }

        // An archived auto chat has no roster left to ask — its membership was snapshotted into
        // participant rows before the team/party was deleted (data-model R3a). Same reason as
        // ResolveAsync: the Kind is still Team/Party, but the link it derived from is gone.
        if (conversation.State == ConversationState.Archived)
        {
            return await _db.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == conversationId && p.LeftDate == null)
                .Select(p => p.UserId)
                .ToListAsync(ct);
        }

        return conversation.Kind switch
        {
            ConversationKind.Team => await _db.TeamMemberships.AsNoTracking()
                .Where(m => m.TeamId == conversation.TeamId)
                .Select(m => m.UserId)
                .ToListAsync(ct),

            // Includes marketplace guests (PartyMember.ViaMarket) — they are In on the roster, so they
            // are in the chat, exactly as the party roster already treats them (feature 017).
            ConversationKind.Party => await _db.PartyMembers.AsNoTracking()
                .Where(pm => pm.PartyId == conversation.PartyId && pm.Status == PartyMemberStatus.In)
                .Select(pm => pm.UserId)
                .ToListAsync(ct),

            // Inquiry threads (feature 027): the fixed requester plus the target's live admin roster.
            // Distinct so a requester who is somehow also an admin (not offered the action, but
            // defensively) is counted once.
            ConversationKind.TeamInquiry => await _db.TeamMemberships.AsNoTracking()
                .Where(m => m.TeamId == conversation.TeamId && m.Role == TeamRole.Admin)
                .Select(m => m.UserId)
                .Concat(_db.Conversations.Where(c => c.Id == conversationId && c.RequesterUserId != null)
                    .Select(c => c.RequesterUserId!.Value))
                .Distinct()
                .ToListAsync(ct),

            ConversationKind.EventInquiry => await _db.EventAdmins.AsNoTracking()
                .Where(a => a.EventId == conversation.EventId)
                .Select(a => a.UserId)
                .Concat(_db.Conversations.Where(c => c.Id == conversationId && c.RequesterUserId != null)
                    .Select(c => c.RequesterUserId!.Value))
                .Distinct()
                .ToListAsync(ct),

            _ => await _db.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == conversationId && p.LeftDate == null)
                .Select(p => p.UserId)
                .ToListAsync(ct),
        };
    }

    /// <summary>
    /// Whether a block stands between two players, in <em>either</em> direction.
    /// </summary>
    /// <remarks>
    /// Blocks are stored directionally but enforced symmetrically. The question a caller actually has
    /// is "may these two hold a direct conversation?", and a block either way answers no — which is
    /// what stops a blocked player from starting a fresh conversation to get around it (spec FR-049b).
    /// </remarks>
    public Task<bool> IsBlockedBetweenAsync(Guid a, Guid b, CancellationToken ct = default) =>
        _db.UserBlocks.AsNoTracking()
            .AnyAsync(x => (x.BlockerUserId == a && x.BlockedUserId == b)
                        || (x.BlockerUserId == b && x.BlockedUserId == a), ct);

    /// <summary>
    /// The moment before which a caller should see nothing in a conversation: their <em>current</em>
    /// join. Messages older than this are neither shown nor counted as unread — being added to a
    /// team/party/group chat does not hand you the backlog from before you were there (spec FR-051).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived from the same source of truth as membership, per kind: a team member's
    /// <see cref="TeamMembership.JoinedDate"/>, a party member's <see cref="PartyMember"/> row date, a
    /// group/direct member's <see cref="ConversationParticipant.JoinedDate"/>. Because leave-and-rejoin
    /// starts a fresh roster/participant row, the cutoff resets on rejoin — a returning player picks up
    /// from their latest join, not their first.
    /// </para>
    /// <para>
    /// <b>Archived auto chats are exempt</b> (returns null): archival snapshots the roster into
    /// participant rows stamped at archive time, so a join-time cutoff would hide the whole history from
    /// everyone. FR-027 keeps archived history fully readable, so there is deliberately no cutoff there.
    /// A null return likewise means "no cutoff" for a caller with no resolvable join row.
    /// </para>
    /// </remarks>
    public async Task<DateTime?> ResolveJoinCutoffAsync(ChatAccess access, Guid userId, CancellationToken ct = default)
    {
        if (access.IsArchived)
        {
            return null;
        }

        // Inquiry threads (feature 027): the requester's cutoff is their stored participant row (≈ thread
        // creation); an admin's cutoff is when they became an admin, so a newly-granted admin sees history
        // from their grant forward and no earlier (FR-019).
        if (access.IsInquiry && access.RequesterUserId != userId)
        {
            return access.Kind == ConversationKind.TeamInquiry
                ? await _db.TeamMemberships.AsNoTracking()
                    .Where(m => m.TeamId == access.TeamId && m.UserId == userId && m.Role == TeamRole.Admin)
                    .Select(m => (DateTime?)m.JoinedDate)
                    .FirstOrDefaultAsync(ct)
                : await _db.EventAdmins.AsNoTracking()
                    .Where(a => a.EventId == access.EventId && a.UserId == userId)
                    .Select(a => (DateTime?)a.AddedDate)
                    .FirstOrDefaultAsync(ct);
        }

        return access.Kind switch
        {
            ConversationKind.Team => await _db.TeamMemberships.AsNoTracking()
                .Where(m => m.TeamId == access.TeamId && m.UserId == userId)
                .Select(m => (DateTime?)m.JoinedDate)
                .FirstOrDefaultAsync(ct),

            ConversationKind.Party => await _db.PartyMembers.AsNoTracking()
                .Where(pm => pm.PartyId == access.PartyId && pm.UserId == userId && pm.Status == PartyMemberStatus.In)
                .Select(pm => (DateTime?)pm.CreatedDate)
                .FirstOrDefaultAsync(ct),

            // Direct/Group, and the requester side of an inquiry: their stored participant JoinedDate.
            _ => await _db.ConversationParticipants.AsNoTracking()
                .Where(p => p.ConversationId == access.ConversationId && p.UserId == userId && p.LeftDate == null)
                .Select(p => (DateTime?)p.JoinedDate)
                .FirstOrDefaultAsync(ct),
        };
    }

    /// <summary>
    /// Batched <see cref="ResolveJoinCutoffAsync"/> for the inbox and nav-badge loops, which resolve a
    /// cutoff for many conversations at once. Three queries total (teams, parties, group/direct) rather
    /// than one per conversation. Archived conversations map to null (no cutoff).
    /// </summary>
    public async Task<Dictionary<Guid, DateTime?>> ResolveJoinCutoffsAsync(
        Guid userId,
        IReadOnlyCollection<ChatAccess> conversations,
        CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, DateTime?>(conversations.Count);
        foreach (var c in conversations)
        {
            result[c.ConversationId] = null;
        }

        var teamConvos = conversations
            .Where(c => !c.IsArchived && c.Kind == ConversationKind.Team && c.TeamId is not null)
            .ToList();
        if (teamConvos.Count > 0)
        {
            var teamIds = teamConvos.Select(c => c.TeamId!.Value).Distinct().ToList();
            var joins = (await _db.TeamMemberships.AsNoTracking()
                    .Where(m => m.UserId == userId && teamIds.Contains(m.TeamId))
                    .Select(m => new { m.TeamId, m.JoinedDate })
                    .ToListAsync(ct))
                .ToDictionary(x => x.TeamId, x => (DateTime?)x.JoinedDate);

            foreach (var c in teamConvos)
            {
                result[c.ConversationId] = joins.GetValueOrDefault(c.TeamId!.Value);
            }
        }

        var partyConvos = conversations
            .Where(c => !c.IsArchived && c.Kind == ConversationKind.Party && c.PartyId is not null)
            .ToList();
        if (partyConvos.Count > 0)
        {
            var partyIds = partyConvos.Select(c => c.PartyId!.Value).Distinct().ToList();
            var joins = (await _db.PartyMembers.AsNoTracking()
                    .Where(pm => pm.UserId == userId && pm.Status == PartyMemberStatus.In && partyIds.Contains(pm.PartyId))
                    .Select(pm => new { pm.PartyId, pm.CreatedDate })
                    .ToListAsync(ct))
                .ToDictionary(x => x.PartyId, x => (DateTime?)x.CreatedDate);

            foreach (var c in partyConvos)
            {
                result[c.ConversationId] = joins.GetValueOrDefault(c.PartyId!.Value);
            }
        }

        // Inquiry threads where the viewer is an ADMIN (not the requester): cutoff = their admin grant
        // date, so a newly-granted admin sees history only from their grant (FR-019). The requester side
        // falls through to the participant-row query below (they are a stored participant).
        var teamInquiryAdmin = conversations
            .Where(c => !c.IsArchived && c.Kind == ConversationKind.TeamInquiry
                && c.RequesterUserId != userId && c.TeamId is not null)
            .ToList();
        if (teamInquiryAdmin.Count > 0)
        {
            var teamIds = teamInquiryAdmin.Select(c => c.TeamId!.Value).Distinct().ToList();
            var grants = (await _db.TeamMemberships.AsNoTracking()
                    .Where(m => m.UserId == userId && m.Role == TeamRole.Admin && teamIds.Contains(m.TeamId))
                    .Select(m => new { m.TeamId, m.JoinedDate })
                    .ToListAsync(ct))
                .ToDictionary(x => x.TeamId, x => (DateTime?)x.JoinedDate);

            foreach (var c in teamInquiryAdmin)
            {
                result[c.ConversationId] = grants.GetValueOrDefault(c.TeamId!.Value);
            }
        }

        var eventInquiryAdmin = conversations
            .Where(c => !c.IsArchived && c.Kind == ConversationKind.EventInquiry
                && c.RequesterUserId != userId && c.EventId is not null)
            .ToList();
        if (eventInquiryAdmin.Count > 0)
        {
            var eventIds = eventInquiryAdmin.Select(c => c.EventId!.Value).Distinct().ToList();
            var grants = (await _db.EventAdmins.AsNoTracking()
                    .Where(a => a.UserId == userId && eventIds.Contains(a.EventId))
                    .Select(a => new { a.EventId, a.AddedDate })
                    .ToListAsync(ct))
                .ToDictionary(x => x.EventId, x => (DateTime?)x.AddedDate);

            foreach (var c in eventInquiryAdmin)
            {
                result[c.ConversationId] = grants.GetValueOrDefault(c.EventId!.Value);
            }
        }

        // Direct/Group, plus the requester side of an inquiry (a stored participant with a JoinedDate).
        var localConvos = conversations
            .Where(c => !c.IsArchived
                && (c.Kind is ConversationKind.Direct or ConversationKind.Group
                    || (c.IsInquiry && c.RequesterUserId == userId)))
            .ToList();
        if (localConvos.Count > 0)
        {
            var ids = localConvos.Select(c => c.ConversationId).ToList();
            var joins = (await _db.ConversationParticipants.AsNoTracking()
                    .Where(p => p.UserId == userId && p.LeftDate == null && ids.Contains(p.ConversationId))
                    .Select(p => new { p.ConversationId, p.JoinedDate })
                    .ToListAsync(ct))
                .ToDictionary(x => x.ConversationId, x => (DateTime?)x.JoinedDate);

            foreach (var c in localConvos)
            {
                result[c.ConversationId] = joins.GetValueOrDefault(c.ConversationId);
            }
        }

        return result;
    }

    /// <summary>
    /// Get or create the caller's per-user state row (read marker, mute, hide) for a conversation.
    /// </summary>
    /// <remarks>
    /// For team/party chats this row is created lazily because membership is derived — there is nothing
    /// to insert when someone joins a roster, so the row only appears the first time the player needs
    /// somewhere to keep state. It is <b>state only and never authority</b>: its absence does not deny
    /// access and its presence does not grant it. Only <see cref="ResolveAsync"/> decides access.
    /// </remarks>
    public async Task<ConversationParticipant> EnsureParticipantStateAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken ct = default)
    {
        var existing = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId, ct);

        if (existing is not null)
        {
            return existing;
        }

        var row = new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedDate = DateTime.UtcNow,
        };

        // DbSet.Add explicitly: adding through a navigation collection with a client-generated GUID key
        // can be misclassified by the change tracker (a known EF gotcha in this codebase).
        _db.ConversationParticipants.Add(row);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two of the player's own tabs raced to materialise the same state row; the unique index
            // on (ConversationId, UserId) caught it. Re-read the winner.
            _db.Entry(row).State = EntityState.Detached;
            return await _db.ConversationParticipants
                .FirstAsync(p => p.ConversationId == conversationId && p.UserId == userId, ct);
        }

        return row;
    }
}
