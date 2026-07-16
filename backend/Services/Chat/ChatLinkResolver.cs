using JuggerHub.Data;
using JuggerHub.Dtos.Chat;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Chat;

/// <summary>
/// Turns a message's stored <c>(LinkKind, LinkTargetId)</c> into a view-only card — resolved for the
/// <b>viewer</b>, not the sender (feature 019, User Story 7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-viewer resolution is the point.</b> The message stores only the item's kind and id, never a
/// snapshot of its fields. That is what lets this class re-run the viewer's own permission check at
/// read time: a sender who can see a team-only training must not be able to leak its name and time
/// into a DM with an outsider just by pasting a link (spec FR-040). The same message legitimately
/// renders a card for one reader and a bare link for another.
/// </para>
/// <para>
/// Returning <c>null</c> means "no card" — the client shows the link as plain text. That is also the
/// answer when the target has since been deleted, which is why the link columns are deliberately not
/// foreign keys: the message degrades quietly instead of cascading (spec FR-041).
/// </para>
/// </remarks>
public sealed class ChatLinkResolver
{
    private readonly AppDbContext _db;

    public ChatLinkResolver(AppDbContext db) => _db = db;

    /// <summary>Resolve cards for a page of messages in one pass per kind (no N+1).</summary>
    public async Task<Dictionary<Guid, LinkCardDto>> ResolveManyAsync(
        IReadOnlyCollection<(Guid MessageId, ChatLinkKind Kind, Guid TargetId)> links,
        Guid viewerId,
        CancellationToken ct = default)
    {
        var cards = new Dictionary<Guid, LinkCardDto>();
        if (links.Count == 0)
        {
            return cards;
        }

        foreach (var group in links.GroupBy(l => l.Kind))
        {
            var ids = group.Select(l => l.TargetId).Distinct().ToList();

            var resolved = group.Key switch
            {
                ChatLinkKind.Player => await ResolvePlayersAsync(ids, ct),
                ChatLinkKind.Team => await ResolveTeamsAsync(ids, ct),
                ChatLinkKind.Event => await ResolveEventsAsync(ids, ct),
                ChatLinkKind.Training => await ResolveTrainingsAsync(ids, viewerId, ct),
                _ => new Dictionary<Guid, LinkCardDto>(),
            };

            foreach (var link in group)
            {
                if (resolved.TryGetValue(link.TargetId, out var card))
                {
                    cards[link.MessageId] = card;
                }

                // Not resolved ⇒ no card ⇒ the body's plain link stands. Deleted target, or a target
                // this viewer may not see — deliberately indistinguishable to them.
            }
        }

        return cards;
    }

    private async Task<Dictionary<Guid, LinkCardDto>> ResolvePlayersAsync(List<Guid> userIds, CancellationToken ct) =>
        // PlayerProfiles carries a global query filter hiding banned accounts (feature 013), so a
        // banned player's card disappears here without this class knowing anything about bans.
        await _db.PlayerProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new
            {
                p.UserId,
                Card = new LinkCardDto(
                    ChatLinkKind.Player,
                    p.UserId,
                    p.DisplayName,
                    p.Hometown,
                    $"/u/{p.Handle}",
                    null),
            })
            .ToDictionaryAsync(x => x.UserId, x => x.Card, ct);

    private async Task<Dictionary<Guid, LinkCardDto>> ResolveTeamsAsync(List<Guid> teamIds, CancellationToken ct) =>
        // Teams are public (feature 005: name/city/activity are the public face), so every viewer who
        // can see the message can see this card.
        await _db.Teams.AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                Card = new LinkCardDto(
                    ChatLinkKind.Team,
                    t.Id,
                    t.Name,
                    t.City,
                    $"/t/{t.Slug}",
                    null),
            })
            .ToDictionaryAsync(x => x.Id, x => x.Card, ct);

    private async Task<Dictionary<Guid, LinkCardDto>> ResolveEventsAsync(List<Guid> eventIds, CancellationToken ct) =>
        await _db.Events.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                Card = new LinkCardDto(
                    ChatLinkKind.Event,
                    e.Id,
                    e.Name,
                    e.Location,
                    $"/events/{e.Id}",
                    null),
            })
            .ToDictionaryAsync(x => x.Id, x => x.Card, ct);

    /// <summary>
    /// Training sessions are the one kind with a real visibility rule, and therefore the one that
    /// makes per-viewer resolution matter.
    /// </summary>
    /// <remarks>
    /// A team-only session resolves <b>only</b> for a member of its team; everyone else gets no card
    /// and reads the bare link (spec FR-040). A public session resolves for anyone. The check is the
    /// viewer's own membership, re-evaluated here — never the sender's, and never a value frozen at
    /// send time.
    /// </remarks>
    private async Task<Dictionary<Guid, LinkCardDto>> ResolveTrainingsAsync(
        List<Guid> sessionIds,
        Guid viewerId,
        CancellationToken ct) =>
        await _db.TrainingSessions.AsNoTracking()
            .Where(s => sessionIds.Contains(s.Id))
            .Where(s =>
                (s.VisibilityOverride ?? s.Training.Visibility) == TrainingVisibility.Public
                || _db.TeamMemberships.Any(m => m.TeamId == s.TeamId && m.UserId == viewerId))
            .Select(s => new
            {
                s.Id,
                Card = new LinkCardDto(
                    ChatLinkKind.Training,
                    s.Id,
                    s.Training.Name,
                    s.LocationOverride ?? s.Training.Location,
                    $"/trainings/sessions/{s.Id}",
                    null),
            })
            .ToDictionaryAsync(x => x.Id, x => x.Card, ct);

    /// <summary>Resolve the id a parsed link refers to, turning a handle/slug into the stored target id.</summary>
    public async Task<Guid?> ResolveTargetIdAsync(ParsedLink link, CancellationToken ct = default) => link.Kind switch
    {
        ChatLinkKind.Player => await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.Handle == link.Slug)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(ct),

        ChatLinkKind.Team => await _db.Teams.AsNoTracking()
            .Where(t => t.Slug == link.Slug)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct),

        ChatLinkKind.Event or ChatLinkKind.Training => link.Id,

        _ => null,
    };
}
