using JuggerHub.Dtos.Events;

namespace JuggerHub.Services.Events;

/// <summary>Outcome of creating an event.</summary>
public enum CreateEventStatus
{
    Created,
    Invalid,
}

/// <summary>Result of a create attempt (carries the created event or a reason).</summary>
public sealed record CreateEventResult(CreateEventStatus Status, EventDetailDto? Event, string? Reason)
{
    public static CreateEventResult Ok(EventDetailDto ev) => new(CreateEventStatus.Created, ev, null);

    public static CreateEventResult Fail(string reason) => new(CreateEventStatus.Invalid, null, reason);
}

/// <summary>Outcome of editing an event.</summary>
public enum EditEventStatus
{
    Updated,
    NotFound,
    Forbidden,
    Invalid,
    ModeLocked,
    LimitBelowOccupied,
}

/// <summary>Result of an edit attempt.</summary>
public sealed record EditEventResult(EditEventStatus Status, EventDetailDto? Event, string? Reason)
{
    public static EditEventResult Ok(EventDetailDto ev) => new(EditEventStatus.Updated, ev, null);

    public static EditEventResult Fail(EditEventStatus status, string? reason = null) => new(status, null, reason);
}

/// <summary>Outcome of cancelling an event.</summary>
public enum CancelEventStatus
{
    Cancelled,
    NotFound,
    Forbidden,
    AlreadyCancelled,
}

/// <summary>
/// Event domain service: create (creator becomes first admin), the public detail read (+ the
/// signed-in viewer's relationship), admin edit (mode/limit guards), and cancel. Accesses EF
/// Core directly (no repository layer) and returns DTOs.
/// </summary>
public interface IEventService
{
    /// <summary>Create an event; the creator becomes its first admin.</summary>
    Task<CreateEventResult> CreateAsync(Guid userId, CreateEventRequest request, CancellationToken ct = default);

    /// <summary>
    /// Public event detail. <paramref name="userId"/> is null for an anonymous caller; when set,
    /// the returned <see cref="EventDetailDto.Viewer"/> reflects that user's relationship. Null when
    /// no event has that id.
    /// </summary>
    Task<EventDetailDto?> GetDetailAsync(Guid eventId, Guid? userId, CancellationToken ct = default);

    /// <summary>Edit an event (admin only; mode locked once sign-ups exist; limit ≥ occupied).</summary>
    Task<EditEventResult> EditAsync(Guid eventId, Guid actorUserId, EditEventRequest request, CancellationToken ct = default);

    /// <summary>Cancel an event (admin only; irreversible; notifies joined + waiting by email).</summary>
    Task<CancelEventStatus> CancelAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default);
}
