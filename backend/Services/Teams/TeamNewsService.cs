using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Notifications;
using JuggerHub.Dtos.Teams;
using JuggerHub.Entities;
using JuggerHub.Services.Email;
using JuggerHub.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Teams;

/// <summary>EF-Core-direct implementation of <see cref="ITeamNewsService"/>.</summary>
public sealed class TeamNewsService : ITeamNewsService
{
    private const int ExcerptLength = 140;

    private readonly AppDbContext _db;
    private readonly TeamMembershipGuard _guard;
    private readonly INotificationService _notifications;
    private readonly INotificationPreferenceService _preferences;
    private readonly TeamEmailService _email;
    private readonly ILogger<TeamNewsService> _logger;
    private readonly Localization.IRecipientCultureResolver _culture;

    public TeamNewsService(
        AppDbContext db,
        TeamMembershipGuard guard,
        INotificationService notifications,
        INotificationPreferenceService preferences,
        TeamEmailService email,
        ILogger<TeamNewsService> logger,
        Localization.IRecipientCultureResolver culture)
    {
        _db = db;
        _guard = guard;
        _notifications = notifications;
        _preferences = preferences;
        _email = email;
        _logger = logger;
        _culture = culture;
    }

    public async Task<PagedResult<TeamNewsDto>?> GetFeedAsync(
        string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, userId, ct);
        if (access is not { IsMember: true } a)
        {
            return null;
        }

        var query = _db.TeamNewsPosts.AsNoTracking().Where(n => n.TeamId == a.TeamId);
        var total = await query.CountAsync(ct);

        var page = query
            .OrderByDescending(n => n.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake);
        var items = await Project(page, AuthorPlaceholder()).ToListAsync(ct);

        return new PagedResult<TeamNewsDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<TeamNewsPostResult> PostAsync(string slug, Guid actorUserId, string body, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { IsMember: true } a)
        {
            return new TeamNewsPostResult(TeamNewsPostStatus.NotFoundOrNotMember, null);
        }

        // Posting fans out to the whole roster, so it is admin-only (spec FR-014).
        if (!a.IsAdmin)
        {
            return new TeamNewsPostResult(TeamNewsPostStatus.Forbidden, null);
        }

        var trimmed = body.Trim();
        var post = new TeamNewsPost
        {
            TeamId = a.TeamId,
            AuthorUserId = actorUserId,
            Body = trimmed,
        };
        _db.TeamNewsPosts.Add(post);
        await _db.SaveChangesAsync(ct);

        var team = await _db.Teams.AsNoTracking()
            .Where(t => t.Id == a.TeamId)
            .Select(t => new { t.Slug, t.Name })
            .FirstAsync(ct);

        var author = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == actorUserId)
            .Select(p => new { p.DisplayName, p.Handle })
            .FirstAsync(ct);

        var dto = new TeamNewsDto(
            post.Id, author.DisplayName, author.Handle, TeamRole.Admin, post.CreatedDate, EditedDate: null, post.Body);

        // Fan out to every other current member (never the author). Best-effort — a notification
        // failure must not fail the post itself (spec FR-016).
        var recipients = await _db.TeamMemberships.AsNoTracking()
            .Where(m => m.TeamId == a.TeamId && m.UserId != actorUserId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        var excerpt = Excerpt(trimmed);

        // In-app: the engine drops recipients who turned Team news → In-app off (feature 011).
        try
        {
            await _notifications.CreateManyAsync(
                recipients,
                NotificationType.TeamNews,
                new TeamNewsPayload(team.Slug, team.Name, post.Id, excerpt),
                actorUserId: actorUserId,
                dedupeKeyPrefix: NewsDedupePrefix(post.Id),
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fan out team-news notifications for post {PostId}.", post.Id);
        }

        // Email: only members with Team news → Email on (feature 011). Best-effort.
        try
        {
            var emailRecipients = await _preferences.GetEnabledRecipientsAsync(
                recipients, NotificationCategory.TeamNews, NotificationChannel.Email, ct);

            if (emailRecipients.Count > 0)
            {
                var emails = await _db.Users.AsNoTracking()
                    .Where(u => emailRecipients.Contains(u.Id) && u.Email != null)
                    .Select(u => u.Email!)
                    .ToListAsync(ct);

                foreach (var email in emails)
                {
                    await _email.SendTeamNewsEmailAsync(email, team.Name, team.Slug, author.DisplayName, excerpt, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fan out team-news emails for post {PostId}.", post.Id);
        }

        return new TeamNewsPostResult(TeamNewsPostStatus.Posted, dto);
    }

    public async Task<TeamNewsEditResult> EditAsync(
        string slug, Guid postId, Guid actorUserId, string body, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { IsMember: true } a)
        {
            return new TeamNewsEditResult(TeamNewsEditStatus.NotFoundOrNotMember, null);
        }

        // Any current admin, for any post, whoever wrote it (spec FR-013). Authorship grants
        // nothing on its own: an author who has since lost admin is refused like any member.
        if (!a.IsAdmin)
        {
            return new TeamNewsEditResult(TeamNewsEditStatus.Forbidden, null);
        }

        // Scoped to the team the slug resolved to, so another team's post id is simply absent here
        // (FR-012) — even for someone who administers both teams.
        var thisPost = _db.TeamNewsPosts.Where(n => n.Id == postId && n.TeamId == a.TeamId);
        var current = await thisPost.AsNoTracking().Select(n => n.Body).FirstOrDefaultAsync(ct);
        if (current is null)
        {
            return new TeamNewsEditResult(TeamNewsEditStatus.PostNotFound, null);
        }

        // Saving the same text is not an edit (FR-003): nothing is written and the post does not
        // become "edited".
        var trimmed = body.Trim();
        if (trimmed != current)
        {
            var now = DateTime.UtcNow;
            var team = await _db.Teams.AsNoTracking()
                .Where(t => t.Id == a.TeamId)
                .Select(t => new { t.Slug, t.Name })
                .FirstAsync(ct);
            // What every alert already delivered for the post will say from now on (FR-006): the
            // same shape PostAsync wrote, carrying the corrected excerpt.
            var payload = new TeamNewsPayload(team.Slug, team.Name, postId, Excerpt(trimmed));

            // The post and the alerts that quote it change together or not at all (research R3).
            // Every statement is an ExecuteUpdate with fixed values, so the execution strategy can
            // replay the delegate safely; and ExecuteUpdate skips the audit interceptor, so
            // ModifiedDate is set here or nowhere (constitution Principle III).
            var strategy = _db.Database.CreateExecutionStrategy();
            var updated = await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                var rows = await thisPost.ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.Body, trimmed)
                    .SetProperty(n => n.EditedDate, now)
                    .SetProperty(n => n.ModifiedDate, now), ct);
                if (rows == 0)
                {
                    // Another admin deleted it between the read above and this write (FR-019).
                    // Returning before the alerts is also what keeps another team's post id away
                    // from that team's rows (FR-012).
                    return false;
                }

                // Silently: the rows keep their read state and place, and nothing is pushed.
                await _notifications.ReplacePayloadAsync(NotificationType.TeamNews, NewsDedupePrefix(postId), payload, ct);

                await tx.CommitAsync(ct);
                return true;
            });

            if (!updated)
            {
                return new TeamNewsEditResult(TeamNewsEditStatus.PostNotFound, null);
            }
        }

        var dto = await Project(thisPost.AsNoTracking(), AuthorPlaceholder()).FirstOrDefaultAsync(ct);
        return dto is null
            ? new TeamNewsEditResult(TeamNewsEditStatus.PostNotFound, null)
            : new TeamNewsEditResult(TeamNewsEditStatus.Updated, dto);
    }

    public async Task<TeamNewsDeleteStatus> DeleteAsync(
        string slug, Guid postId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(slug, actorUserId, ct);
        if (access is not { IsMember: true } a)
        {
            return TeamNewsDeleteStatus.NotFoundOrNotMember;
        }

        // Any current admin, for any post (FR-013).
        if (!a.IsAdmin)
        {
            return TeamNewsDeleteStatus.Forbidden;
        }

        // The post and its alerts go together or not at all (research R3). Deleted separately, a
        // failure in between would leave the post gone and its text still quoted in every inbox,
        // with no way back: deleting again answers "not found". Both statements are idempotent
        // ExecuteDeletes, so a replay by the execution strategy converges.
        var strategy = _db.Database.CreateExecutionStrategy();
        var (deleted, unreadRecipients) = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var rows = await _db.TeamNewsPosts
                .Where(n => n.Id == postId && n.TeamId == a.TeamId)
                .ExecuteDeleteAsync(ct);
            if (rows == 0)
            {
                // Never reach the alerts for a post that is not this team's: this early return is
                // what keeps another team's post id away from that team's rows (FR-012).
                return (false, (IReadOnlyCollection<Guid>)[]);
            }

            var recipients = await _notifications.DeleteManyAsync(NotificationType.TeamNews, NewsDedupePrefix(postId), ct);
            await tx.CommitAsync(ct);
            return (true, recipients);
        });

        if (!deleted)
        {
            return TeamNewsDeleteStatus.PostNotFound;
        }

        // Only now that the delete is committed: a badge lowered before it could be contradicted
        // by a rollback. Best-effort, and it never fails the delete.
        await _notifications.RefreshUnreadBadgesAsync(unreadRecipients, ct);
        return TeamNewsDeleteStatus.Deleted;
    }

    /// <summary>
    /// The one shape of a news item, shared by the feed and the edit response so the two cannot
    /// drift apart.
    /// </summary>
    private IQueryable<TeamNewsDto> Project(IQueryable<TeamNewsPost> posts, string placeholder) =>
        posts.Select(n => new TeamNewsDto(
            n.Id,
            n.Author.Profile != null ? n.Author.Profile.DisplayName : placeholder,
            n.Author.Profile != null ? n.Author.Profile.Handle : null,
            // Author's current role in this team (defaults to Member if they've left).
            _db.TeamMemberships
                .Where(m => m.TeamId == n.TeamId && m.UserId == n.AuthorUserId)
                .Select(m => m.Role)
                .FirstOrDefault(),
            n.CreatedDate,
            n.EditedDate,
            n.Body));

    /// <summary>
    /// The author's profile is absent once they are banned (filtered, 013) or erased (deleted, 037),
    /// so the projection yields null rather than the row going missing. The post itself stays — it
    /// is a record the team relies on — and the author collapses to the neutral placeholder.
    /// </summary>
    private string AuthorPlaceholder() => MemberPlaceholder.For(_culture.ResolveFromRequest());

    private static string Excerpt(string body) =>
        body.Length <= ExcerptLength ? body : body[..ExcerptLength].TrimEnd() + "…";

    /// <summary>
    /// The dedupe-key prefix a post's alerts are written under. Posting creates them under it, and
    /// editing and deleting find them by it (feature 057), so it is spelled once.
    /// </summary>
    private static string NewsDedupePrefix(Guid postId) => $"news:{postId}";
}
