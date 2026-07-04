using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Events;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Services.Events;

/// <summary>EF-Core-direct implementation of <see cref="IEventContactService"/>.</summary>
public sealed class EventContactService : IEventContactService
{
    private readonly AppDbContext _db;
    private readonly EventAdminGuard _guard;

    public EventContactService(AppDbContext db, EventAdminGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    public async Task<PagedResult<EventContactDto>?> ListAsync(Guid eventId, PaginationRequest pagination, CancellationToken ct = default)
    {
        var exists = await _db.Events.AsNoTracking().AnyAsync(e => e.Id == eventId, ct);
        if (!exists)
        {
            return null;
        }

        var query = _db.EventContacts.AsNoTracking().Where(c => c.EventId == eventId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.CreatedDate)
            .Skip(pagination.NormalizedSkip)
            .Take(pagination.NormalizedTake)
            .Select(c => new EventContactDto(c.Id, c.Name, c.Role, c.Phone, c.Email))
            .ToListAsync(ct);

        return new PagedResult<EventContactDto>(items, total, pagination.NormalizedSkip, pagination.NormalizedTake);
    }

    public async Task<ContactOpResult> AddAsync(Guid eventId, Guid actorUserId, CreateContactRequest request, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(eventId, actorUserId, ct);
        if (gate is not null)
        {
            return gate;
        }

        var (name, role, phone, email, invalid) = Normalize(request);
        if (invalid is not null)
        {
            return invalid;
        }

        var contact = new EventContact { EventId = eventId, Name = name, Role = role, Phone = phone, Email = email };
        _db.EventContacts.Add(contact);
        await _db.SaveChangesAsync(ct);

        return ContactOpResult.Ok(new EventContactDto(contact.Id, contact.Name, contact.Role, contact.Phone, contact.Email));
    }

    public async Task<ContactOpResult> UpdateAsync(Guid eventId, Guid contactId, Guid actorUserId, CreateContactRequest request, CancellationToken ct = default)
    {
        var gate = await GateAdminAsync(eventId, actorUserId, ct);
        if (gate is not null)
        {
            return gate;
        }

        var (name, role, phone, email, invalid) = Normalize(request);
        if (invalid is not null)
        {
            return invalid;
        }

        var contact = await _db.EventContacts.FirstOrDefaultAsync(c => c.Id == contactId && c.EventId == eventId, ct);
        if (contact is null)
        {
            return ContactOpResult.Fail(ContactOpStatus.ContactNotFound);
        }

        contact.Name = name;
        contact.Role = role;
        contact.Phone = phone;
        contact.Email = email;
        await _db.SaveChangesAsync(ct);

        return ContactOpResult.Ok(new EventContactDto(contact.Id, contact.Name, contact.Role, contact.Phone, contact.Email));
    }

    public async Task<ContactOpStatus> RemoveAsync(Guid eventId, Guid contactId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return ContactOpStatus.NotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return ContactOpStatus.Forbidden;
        }

        var affected = await _db.EventContacts
            .Where(c => c.Id == contactId && c.EventId == eventId)
            .ExecuteDeleteAsync(ct);

        return affected > 0 ? ContactOpStatus.Ok : ContactOpStatus.ContactNotFound;
    }

    // --- Helpers --------------------------------------------------------------

    private async Task<ContactOpResult?> GateAdminAsync(Guid eventId, Guid actorUserId, CancellationToken ct)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return ContactOpResult.Fail(ContactOpStatus.NotFound);
        }

        return access.Value.IsAdmin ? null : ContactOpResult.Fail(ContactOpStatus.Forbidden, "Only an event admin can manage contacts.");
    }

    private static (string Name, string Role, string? Phone, string? Email, ContactOpResult? Invalid) Normalize(CreateContactRequest r)
    {
        var name = (r.Name ?? string.Empty).Trim();
        var role = (r.Role ?? string.Empty).Trim();
        var phone = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone!.Trim();
        var email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email!.Trim();

        if (name.Length == 0 || role.Length == 0)
        {
            return (name, role, phone, email, ContactOpResult.Fail(ContactOpStatus.Invalid, "A contact needs a name and a role."));
        }

        if (phone is null && email is null)
        {
            return (name, role, phone, email, ContactOpResult.Fail(ContactOpStatus.Invalid, "Add a phone number, an email, or both."));
        }

        return (name, role, phone, email, null);
    }
}
