using JuggerHub.Common;
using JuggerHub.Dtos.Events;

namespace JuggerHub.Services.Events;

/// <summary>Outcome of a contact mutation.</summary>
public enum ContactOpStatus
{
    Ok,
    NotFound,
    Forbidden,
    Invalid,       // neither phone nor email
    ContactNotFound,
}

/// <summary>Result of a contact add/update (carries the contact on success).</summary>
public sealed record ContactOpResult(ContactOpStatus Status, EventContactDto? Contact, string? Reason)
{
    public static ContactOpResult Ok(EventContactDto contact) => new(ContactOpStatus.Ok, contact, null);

    public static ContactOpResult Fail(ContactOpStatus status, string? reason = null) => new(status, null, reason);
}

/// <summary>Event contacts: a public read list and admin-only add/update/remove (≥1 method required).</summary>
public interface IEventContactService
{
    /// <summary>Public contacts list (paginated). Null when no event has that id.</summary>
    Task<PagedResult<EventContactDto>?> ListAsync(Guid eventId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Add a contact (admin only; requires at least one of phone/email).</summary>
    Task<ContactOpResult> AddAsync(Guid eventId, Guid actorUserId, CreateContactRequest request, CancellationToken ct = default);

    /// <summary>Update a contact (admin only; requires at least one of phone/email).</summary>
    Task<ContactOpResult> UpdateAsync(Guid eventId, Guid contactId, Guid actorUserId, CreateContactRequest request, CancellationToken ct = default);

    /// <summary>Remove a contact (admin only).</summary>
    Task<ContactOpStatus> RemoveAsync(Guid eventId, Guid contactId, Guid actorUserId, CancellationToken ct = default);
}
