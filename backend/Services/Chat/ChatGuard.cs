using JuggerHub.Data;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Chat;

/// <summary>A caller's resolved access to a single conversation (feature 019).</summary>
public readonly record struct ChatAccess(
    Guid ConversationId,
    ConversationKind Kind,
    ConversationState State,
    Guid? TeamId,
    Guid? PartyId)
{
    /// <summary>Archived conversations are readable but closed to writes (spec FR-027).</summary>
    public bool IsArchived => State == ConversationState.Archived;

    /// <summary>Only manually-created groups can be added to or left (spec FR-026, FR-044).</summary>
    public bool IsManualGroup => Kind == ConversationKind.Group;

    /// <summary>Blocks apply to direct conversations and nothing else (spec FR-032).</summary>
    public bool IsDirect => Kind == ConversationKind.Direct;
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
                // The membership predicate, evaluated in the database in the same round trip.
                // Each kind reads its own source of truth; the roster branches are what make
                // removal take effect immediately, with no sync step.
                IsMember =
                    (c.Kind == ConversationKind.Direct || c.Kind == ConversationKind.Group)
                        ? c.Participants.Any(p => p.UserId == userId && p.LeftDate == null)
                    : c.Kind == ConversationKind.Team
                        ? _db.TeamMemberships.Any(m => m.TeamId == c.TeamId && m.UserId == userId)
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

        return new ChatAccess(row.Id, row.Kind, row.State, row.TeamId, row.PartyId);
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
            .Select(c => new { c.Kind, c.TeamId, c.PartyId })
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            return Array.Empty<Guid>();
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
