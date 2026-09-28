using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Events;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Events;

/// <summary>EF-Core-direct implementation of <see cref="IEventNewsService"/>.</summary>
public sealed class EventNewsService : IEventNewsService
{
    private readonly AppDbContext _db;
    private readonly EventAdminGuard _guard;
    private readonly Localization.IRecipientCultureResolver _culture;

    public EventNewsService(AppDbContext db, EventAdminGuard guard, Localization.IRecipientCultureResolver culture)
    {
        _db = db;
        _guard = guard;
        _culture = culture;
    }

    public async Task<PagedResult<EventNewsDto>?> GetFeedAsync(Guid eventId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var exists = await _db.Events.AsNoTracking().AnyAsync(e => e.Id == eventId, ct);
        if (!exists)
        {
            return null;
        }

        var query = _db.EventNewsPosts.AsNoTracking().Where(n => n.EventId == eventId);
        var total = await query.CountAsync(ct);

        var page = query
            .OrderByDescending(n => n.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake);
        var items = await Project(page, AuthorPlaceholder()).ToListAsync(ct);

        return new PagedResult<EventNewsDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<PostNewsResult> PostAsync(Guid eventId, Guid actorUserId, string body, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return PostNewsResult.Fail(PostNewsStatus.NotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return PostNewsResult.Fail(PostNewsStatus.Forbidden);
        }

        var post = new EventNewsPost { EventId = eventId, AuthorUserId = actorUserId, Body = body.Trim() };
        _db.EventNewsPosts.Add(post);
        await _db.SaveChangesAsync(ct);

        // Through the one projection, so the author falls back to the shared placeholder here too
        // (this path alone still said a hardcoded English "An organiser" before feature 059).
        var dto = await Project(_db.EventNewsPosts.AsNoTracking().Where(n => n.Id == post.Id), AuthorPlaceholder())
            .FirstAsync(ct);
        return PostNewsResult.Ok(dto);
    }

    public async Task<EventNewsEditResult> EditAsync(
        Guid eventId, Guid postId, Guid actorUserId, string body, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return new EventNewsEditResult(EventNewsEditStatus.EventNotFound, null);
        }

        // Any current admin, for any post, whoever wrote it (FR-012). Authorship grants nothing on
        // its own. The event's state does not matter (FR-015): a cancelled event's page stays
        // readable, so a wrong post on it stays harmful.
        if (!access.Value.IsAdmin)
        {
            return new EventNewsEditResult(EventNewsEditStatus.Forbidden, null);
        }

        // Scoped to the event addressed, so another event's post id is simply absent here (FR-013)
        // — even for someone who administers both.
        var thisPost = _db.EventNewsPosts.Where(n => n.Id == postId && n.EventId == eventId);
        var current = await thisPost.AsNoTracking().Select(n => n.Body).FirstOrDefaultAsync(ct);
        if (current is null)
        {
            return new EventNewsEditResult(EventNewsEditStatus.PostNotFound, null);
        }

        // Saving the same text is not an edit (FR-003): nothing is written and the post does not
        // become "edited".
        var trimmed = body.Trim();
        if (trimmed != current)
        {
            // One statement with fixed values, so the provider's execution strategy may replay it
            // safely and no transaction is needed: event news has no alerts to change alongside it.
            // ExecuteUpdate skips the audit interceptor, so ModifiedDate is set here or nowhere
            // (constitution Principle III).
            var now = DateTime.UtcNow;
            var rows = await thisPost.ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Body, trimmed)
                .SetProperty(n => n.EditedDate, now)
                .SetProperty(n => n.ModifiedDate, now), ct);
            if (rows == 0)
            {
                // Another admin deleted it between the read above and this write (FR-020).
                return new EventNewsEditResult(EventNewsEditStatus.PostNotFound, null);
            }
        }

        var dto = await Project(thisPost.AsNoTracking(), AuthorPlaceholder()).FirstOrDefaultAsync(ct);
        return dto is null
            ? new EventNewsEditResult(EventNewsEditStatus.PostNotFound, null)
            : new EventNewsEditResult(EventNewsEditStatus.Updated, dto);
    }

    public async Task<EventNewsDeleteStatus> DeleteAsync(
        Guid eventId, Guid postId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return EventNewsDeleteStatus.EventNotFound;
        }

        // Any current admin, for any post (FR-012), whatever the event's state (FR-015).
        if (!access.Value.IsAdmin)
        {
            return EventNewsDeleteStatus.Forbidden;
        }

        // Event news never produced alerts or anything else to take with it, so this is one
        // statement. A replay after a commit whose answer was lost finds nothing: "not found",
        // which the page treats as already gone.
        var rows = await _db.EventNewsPosts
            .Where(n => n.Id == postId && n.EventId == eventId)
            .ExecuteDeleteAsync(ct);
        return rows == 0 ? EventNewsDeleteStatus.PostNotFound : EventNewsDeleteStatus.Deleted;
    }

    /// <summary>
    /// The one shape of a news item, shared by the feed, the post response and the edit response so
    /// they cannot drift apart.
    /// </summary>
    private static IQueryable<EventNewsDto> Project(IQueryable<EventNewsPost> posts, string placeholder) =>
        posts.Select(n => new EventNewsDto(
            n.Id,
            n.Author.Profile != null ? n.Author.Profile.DisplayName : placeholder,
            n.Body,
            n.CreatedDate,
            n.EditedDate));

    /// <summary>
    /// The author's profile is absent once they are banned (filtered, 013) or erased (deleted, 037),
    /// so the projection yields null rather than the row going missing. The post stays and the author
    /// collapses to the one placeholder every surface uses.
    /// </summary>
    private string AuthorPlaceholder() => MemberPlaceholder.For(_culture.ResolveFromRequest());
}
