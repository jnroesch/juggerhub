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

        // This path already anticipated a missing author. Feature 037 only changed the fallback text:
        // it was a hardcoded English "An organiser" on a page that ships in three languages, and it
        // now shares the one placeholder every other surface uses.
        var placeholder = Common.MemberPlaceholder.For(_culture.ResolveFromRequest());

        var items = await query
            .OrderByDescending(n => n.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(n => new EventNewsDto(
                n.Id,
                n.Author.Profile != null ? n.Author.Profile.DisplayName : placeholder,
                n.Body,
                n.CreatedDate))
            .ToListAsync(ct);

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

        var displayName = await _db.PlayerProfiles.AsNoTracking()
            .Where(p => p.UserId == actorUserId)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(ct) ?? "An organiser";

        return PostNewsResult.Ok(new EventNewsDto(post.Id, displayName, post.Body, post.CreatedDate));
    }
}
