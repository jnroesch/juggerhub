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

/// <summary>Event news: a public read feed and an admin-only compose.</summary>
public interface IEventNewsService
{
    /// <summary>Public news feed (paginated, newest-first). Null when no event has that id.</summary>
    Task<PagedResult<EventNewsDto>?> GetFeedAsync(Guid eventId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Post a news update (admin only).</summary>
    Task<PostNewsResult> PostAsync(Guid eventId, Guid actorUserId, string body, CancellationToken ct = default);
}
