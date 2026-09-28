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
/// email), mirroring team news. Any current party admin may edit or delete any post (feature 059).
/// Posts are deleted with the party on disband (cascade).
/// </summary>
public sealed class PartyNewsService : IPartyNewsService
{
    private readonly AppDbContext _db;
    private readonly PartyGuard _guard;
    private readonly INotificationService _notifications;
    private readonly INotificationPreferenceService _preferences;
    private readonly PartyEmailService _email;
    private readonly Localization.IRecipientCultureResolver _culture;
    private readonly ILogger<PartyNewsService> _logger;

    public PartyNewsService(
        AppDbContext db,
        PartyGuard guard,
        INotificationService notifications,
        INotificationPreferenceService preferences,
        PartyEmailService email,
        Localization.IRecipientCultureResolver culture,
        ILogger<PartyNewsService> logger)
    {
        _db = db;
        _guard = guard;
        _notifications = notifications;
        _preferences = preferences;
        _email = email;
        _culture = culture;
        _logger = logger;
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

        var page = query
            .OrderByDescending(n => n.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake);
        var items = await Project(page, AuthorPlaceholder()).ToListAsync(ct);

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
        var dto = new PartyNewsDto(post.Id, authorName, PartyMemberRole.Admin, post.Body, post.CreatedDate, EditedDate: null);
        return PartyResult<PartyNewsDto>.Ok(dto);
    }

    public async Task<PartyNewsEditResult> EditAsync(
        Guid partyId, Guid postId, Guid actorUserId, string body, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is not { } a || !CanSeeNews(a))
        {
            return new PartyNewsEditResult(PartyNewsEditStatus.PartyNotFound, null);
        }

        // Any current party admin, for any post, whoever wrote it (FR-012). Authorship grants nothing
        // on its own; neither the party's nor its event's state matters (FR-015).
        if (!a.IsPartyAdmin)
        {
            return new PartyNewsEditResult(PartyNewsEditStatus.Forbidden, null);
        }

        // Scoped to the party addressed, so another party's post id is simply absent here (FR-013).
        var thisPost = _db.PartyNewsPosts.Where(n => n.Id == postId && n.PartyId == partyId);
        var current = await thisPost.AsNoTracking().Select(n => n.Body).FirstOrDefaultAsync(ct);
        if (current is null)
        {
            return new PartyNewsEditResult(PartyNewsEditStatus.PostNotFound, null);
        }

        // Saving the same text is not an edit (FR-003).
        var trimmed = body.Trim();
        if (trimmed != current)
        {
            // Only the post changes. Its alerts quote none of its text (they name the team and the
            // event), so there is nothing in them to correct, and rewriting them anyway would move
            // their ModifiedDate for a change nobody made (research R1, FR-006). One statement with
            // fixed values: the execution strategy may replay it safely. ExecuteUpdate skips the
            // audit interceptor, so ModifiedDate is set here or nowhere (constitution Principle III).
            var now = DateTime.UtcNow;
            var rows = await thisPost.ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Body, trimmed)
                .SetProperty(n => n.EditedDate, now)
                .SetProperty(n => n.ModifiedDate, now), ct);
            if (rows == 0)
            {
                // Another admin deleted it between the read above and this write (FR-020).
                return new PartyNewsEditResult(PartyNewsEditStatus.PostNotFound, null);
            }
        }

        var dto = await Project(thisPost.AsNoTracking(), AuthorPlaceholder()).FirstOrDefaultAsync(ct);
        return dto is null
            ? new PartyNewsEditResult(PartyNewsEditStatus.PostNotFound, null)
            : new PartyNewsEditResult(PartyNewsEditStatus.Updated, dto);
    }

    public async Task<PartyNewsDeleteStatus> DeleteAsync(
        Guid partyId, Guid postId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(partyId, actorUserId, ct);
        if (access is not { } a || !CanSeeNews(a))
        {
            return PartyNewsDeleteStatus.PartyNotFound;
        }

        // Any current party admin, for any post (FR-012).
        if (!a.IsPartyAdmin)
        {
            return PartyNewsDeleteStatus.Forbidden;
        }

        // The post and its alerts go together or not at all (057's research R3). Deleted separately, a
        // failure in between would leave the post gone and alerts still announcing it, with no way
        // back: deleting again answers "not found". Both statements are idempotent ExecuteDeletes, so
        // a replay by the execution strategy converges.
        var strategy = _db.Database.CreateExecutionStrategy();
        var (deleted, unreadRecipients) = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var rows = await _db.PartyNewsPosts
                .Where(n => n.Id == postId && n.PartyId == partyId)
                .ExecuteDeleteAsync(ct);
            if (rows == 0)
            {
                // Never reach the alerts for a post that is not this party's: this early return is
                // what keeps another party's post id away from that party's rows (FR-013).
                return (false, (IReadOnlyCollection<Guid>)[]);
            }

            // Found by the prefix they were written under, never by the crew: that reaches people
            // who have left the party since (FR-009).
            var recipients = await _notifications.DeleteManyAsync(NotificationType.PartyNews, NewsDedupePrefix(postId), ct);
            await tx.CommitAsync(ct);
            return (true, recipients);
        });

        if (!deleted)
        {
            return PartyNewsDeleteStatus.PostNotFound;
        }

        // Only now that the delete is committed: a badge lowered before it could be contradicted by a
        // rollback. Best-effort, and it never fails the delete.
        await _notifications.RefreshUnreadBadgesAsync(unreadRecipients, ct);
        return PartyNewsDeleteStatus.Deleted;
    }

    /// <summary>
    /// Whoever may read the party's news, which is the crew. A party admin is in the crew in practice
    /// (declining demotes), but posting has only ever asked for the admin role, so this is not
    /// stricter than posting. Everyone else gets the answer reading gives: no such party.
    /// </summary>
    private static bool CanSeeNews(PartyAccess access) => access.IsCrew || access.IsPartyAdmin;

    /// <summary>
    /// The one shape of a news item, shared by the feed and the edit response so the two cannot drift
    /// apart. The author's role is their current one in this party.
    /// </summary>
    private IQueryable<PartyNewsDto> Project(IQueryable<PartyNewsPost> posts, string placeholder) =>
        posts.Select(n => new PartyNewsDto(
            n.Id,
            // Absent once the author is banned (filtered, 013) or erased (deleted, 037), so this
            // projects to null. The post stays; the author collapses to the placeholder.
            n.Author.Profile != null ? n.Author.Profile.DisplayName : placeholder,
            _db.PartyMembers
                .Where(m => m.PartyId == n.PartyId && m.UserId == n.AuthorUserId)
                .Select(m => m.Role)
                .FirstOrDefault(),
            n.Body,
            n.CreatedDate,
            n.EditedDate));

    private string AuthorPlaceholder() => Common.MemberPlaceholder.For(_culture.ResolveFromRequest());

    /// <summary>
    /// The dedupe-key prefix a post's alerts are written under. Posting creates them under it, and
    /// deleting finds them by it (feature 059), so it is spelled once.
    /// </summary>
    private static string NewsDedupePrefix(Guid postId) => $"party-news:{postId}";

    /// <summary>
    /// Matches <c>TeamNewsService.Excerpt</c> (feature 039, FR-005): the party-news email used to
    /// carry the full post body while the team-news email carried a short excerpt. Same rule now.
    /// </summary>
    private const int ExcerptLength = 140;

    private static string Excerpt(string body) =>
        body.Length <= ExcerptLength ? body : body[..ExcerptLength].TrimEnd() + "…";

    private async Task NotifyCrewAsync(Guid partyId, Guid actorUserId, Guid postId, CancellationToken ct)
    {
        var info = await _db.Parties.AsNoTracking()
            .Where(p => p.Id == partyId)
            .Select(p => new { p.EventId, EventName = p.Event.Name, TeamName = p.Team.Name, TeamSlug = p.Team.Slug })
            .FirstAsync(ct);

        // PreferredLanguage rides along on the projection that already runs (feature 039).
        var crew = await _db.PartyMembers.AsNoTracking()
            .Where(m => m.PartyId == partyId && m.Status == PartyMemberStatus.In && m.UserId != actorUserId)
            .Select(m => new { m.UserId, m.User.Email, Name = m.User.Profile!.DisplayName, m.User.PreferredLanguage })
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
            dedupeKeyPrefix: NewsDedupePrefix(postId),
            ct);

        // Body isn't stored on the post at fan-out time — re-read for the email copy.
        var body = await _db.PartyNewsPosts.AsNoTracking().Where(n => n.Id == postId).Select(n => n.Body).FirstAsync(ct);
        var excerpt = Excerpt(body);

        // Email: only crew with Team news → Email on (feature 011, wired up in 039). Best-effort.
        try
        {
            var emailRecipients = await _preferences.GetEnabledRecipientsAsync(
                crew.Select(c => c.UserId).ToList(),
                NotificationCategory.TeamNews,
                NotificationChannel.Email,
                ct);

            foreach (var c in crew.Where(c => !string.IsNullOrEmpty(c.Email) && emailRecipients.Contains(c.UserId)))
            {
                await _email.SendPartyNewsEmailAsync(
                    c.Email!, c.Name, info.TeamName, info.EventName, info.TeamSlug, info.EventId, excerpt,
                    SupportedLanguages.ResolveOrDefault(c.PreferredLanguage), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send party-news emails for post {PostId}.", postId);
        }
    }
}
