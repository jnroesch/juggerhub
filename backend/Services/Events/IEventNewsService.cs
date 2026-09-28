using JuggerHub.Common;
using JuggerHub.Dtos.Events;

namespace JuggerHub.Services.Events;

/// <summary>Outcome of posting event news.</summary>
public enum PostNewsStatus
{
    Posted,
    NotFound,
    Forbidden,
}

/// <summary>Result of posting news (carries the created post on success).</summary>
public sealed record PostNewsResult(PostNewsStatus Status, EventNewsDto? Post)
{
    public static PostNewsResult Ok(EventNewsDto post) => new(PostNewsStatus.Posted, post);

    public static PostNewsResult Fail(PostNewsStatus status) => new(status, null);
}

/// <summary>Outcome of an attempt to edit an event news post (feature 059).</summary>
public enum EventNewsEditStatus
{
    /// <summary>Saved — or the text was already the same, in which case nothing was written.</summary>
    Updated,
    EventNotFound,
    Forbidden,
    /// <summary>No such post in this event: deleted, never existed, or another event's.</summary>
    PostNotFound,
}

/// <summary>The post as it now stands, plus the outcome.</summary>
public sealed record EventNewsEditResult(EventNewsEditStatus Status, EventNewsDto? Post);

/// <summary>Outcome of an attempt to delete an event news post (feature 059).</summary>
public enum EventNewsDeleteStatus
{
    Deleted,
    EventNotFound,
    Forbidden,
    /// <summary>No such post in this event: already deleted, never existed, or another event's.</summary>
    PostNotFound,
}

/// <summary>
/// Event news: a feed every signed-in player reads, an admin-only compose that notifies nobody, and
/// (feature 059) edit and delete open to any current event admin, for any post.
/// </summary>
public interface IEventNewsService
{
    /// <summary>News feed (paginated, newest-first). Null when no event has that id.</summary>
    Task<PagedResult<EventNewsDto>?> GetFeedAsync(Guid eventId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Post a news update (admin only).</summary>
    Task<PostNewsResult> PostAsync(Guid eventId, Guid actorUserId, string body, CancellationToken ct = default);

    /// <summary>
    /// Replace a post's text (any current event admin, any post, whatever the event's state). The
    /// post keeps its author, date and place in the feed and is marked edited. Nobody is notified.
    /// Text equal to the current text (after trimming) writes nothing.
    /// </summary>
    Task<EventNewsEditResult> EditAsync(Guid eventId, Guid postId, Guid actorUserId, string body, CancellationToken ct = default);

    /// <summary>Delete a post for good (any current event admin, any post). Nobody is notified.</summary>
    Task<EventNewsDeleteStatus> DeleteAsync(Guid eventId, Guid postId, Guid actorUserId, CancellationToken ct = default);
}
