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

    public TeamNewsService(
        AppDbContext db,
        TeamMembershipGuard guard,
        INotificationService notifications,
        INotificationPreferenceService preferences,
        TeamEmailService email,
        ILogger<TeamNewsService> logger)
    {
        _db = db;
        _guard = guard;
        _notifications = notifications;
        _preferences = preferences;
        _email = email;
        _logger = logger;
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
        var items = await query
            .OrderByDescending(n => n.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(n => new TeamNewsDto(
                n.Author.Profile!.DisplayName,
                n.Author.Profile!.Handle,
                // Author's current role in this team (defaults to Member if they've left).
                _db.TeamMemberships
                    .Where(m => m.TeamId == n.TeamId && m.UserId == n.AuthorUserId)
                    .Select(m => m.Role)
                    .FirstOrDefault(),
                n.CreatedDate,
                n.Body))
            .ToListAsync(ct);

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

        var dto = new TeamNewsDto(author.DisplayName, author.Handle, TeamRole.Admin, post.CreatedDate, post.Body);

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
                dedupeKeyPrefix: $"news:{post.Id}",
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

    private static string Excerpt(string body) =>
        body.Length <= ExcerptLength ? body : body[..ExcerptLength].TrimEnd() + "…";
}
