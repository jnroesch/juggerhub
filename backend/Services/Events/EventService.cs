using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Events;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuggerHub.Services.Events;

/// <summary>
/// EF-Core-direct implementation of <see cref="IEventService"/>. Validation of the wizard's
/// conditional rules (location by kind, fee by paid, end ≥ start, positive limit) is enforced
/// here server-side (constitution Principle I); the creator becomes the first admin in a single
/// transaction (explicit <c>DbSet.Add</c> — client-set UUIDv7 nav-insert gotcha).
/// </summary>
public sealed class EventService : IEventService
{
    private readonly AppDbContext _db;
    private readonly EventOptions _options;
    private readonly EventCapacity _capacity;

    public EventService(AppDbContext db, IOptions<EventOptions> options, EventCapacity capacity)
    {
        _db = db;
        _options = options.Value;
        _capacity = capacity;
    }

    public async Task<CreateEventResult> CreateAsync(Guid userId, CreateEventRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length < _options.NameMinLength || name.Length > _options.NameMaxLength)
        {
            return CreateEventResult.Fail($"Use an event name of {_options.NameMinLength}–{_options.NameMaxLength} characters.");
        }

        string? customLabel = null;
        if (request.Type == EventType.Other)
        {
            customLabel = (request.CustomTypeLabel ?? string.Empty).Trim();
            if (customLabel.Length is 0 or > 40)
            {
                return CreateEventResult.Fail("A custom event type needs a short label (1–40 characters).");
            }
        }

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length is 0 || description.Length > _options.DescriptionMaxLength)
        {
            return CreateEventResult.Fail($"Add a description of up to {_options.DescriptionMaxLength} characters.");
        }

        var startsAt = ToUtc(request.StartsAt);
        var endsAt = ToUtc(request.EndsAt);
        if (endsAt < startsAt)
        {
            return CreateEventResult.Fail("The event must end on or after it starts.");
        }

        if (request.ParticipationLimit < 1 || request.ParticipationLimit > _options.MaxParticipationLimit)
        {
            return CreateEventResult.Fail($"Set a participation limit between 1 and {_options.MaxParticipationLimit}.");
        }

        var locationResult = ResolveLocation(request);
        if (locationResult.Reason is not null)
        {
            return CreateEventResult.Fail(locationResult.Reason);
        }

        var feeResult = ResolveFee(request.IsPaid, request.FeeAmount, request.FeeCurrency, request.FeeRecipientName, request.FeeIban);
        if (feeResult.Reason is not null)
        {
            return CreateEventResult.Fail(feeResult.Reason);
        }

        var ev = new Event
        {
            Name = name,
            Type = request.Type,
            CustomTypeLabel = customLabel,
            Description = description,
            StartsAt = startsAt,
            EndsAt = endsAt,
            LocationKind = request.LocationKind,
            VenueName = locationResult.VenueName,
            Street = locationResult.Street,
            PostalCode = locationResult.PostalCode,
            City = locationResult.City,
            Country = locationResult.Country,
            VirtualLink = locationResult.VirtualLink,
            Location = locationResult.LegacyLocation,
            ParticipantMode = request.ParticipantMode,
            ParticipationLimit = request.ParticipationLimit,
            IsPaid = feeResult.IsPaid,
            FeeAmount = feeResult.Amount,
            FeeCurrency = feeResult.Currency,
            FeeRecipientName = feeResult.RecipientName,
            FeeIban = feeResult.Iban,
            FeePaymentDeadline = request.IsPaid ? request.FeePaymentDeadline : null,
            Status = EventStatus.Published,
        };

        // Explicit DbSet.Add for both rows (client-set UUIDv7 nav-insert gotcha); one
        // SaveChanges wraps them in a single transaction.
        _db.Events.Add(ev);
        _db.EventAdmins.Add(new EventAdmin { EventId = ev.Id, UserId = userId, AddedDate = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);

        var viewer = new ViewerRelationDto(true, true, null, null, []);
        return CreateEventResult.Ok(ToDetail(ev, occupiedSpots: 0, viewer));
    }

    public async Task<EventDetailDto?> GetDetailAsync(Guid eventId, Guid? userId, CancellationToken ct = default)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
        {
            return null;
        }

        var occupied = await _capacity.OccupiedCountAsync(eventId, ct);

        // Anonymous viewer: no relationship. The page itself is public.
        if (userId is not Guid uid)
        {
            return ToDetail(ev, occupied, new ViewerRelationDto(false, false, null, null, []));
        }

        var isAdmin = await _db.EventAdmins.AsNoTracking().AnyAsync(a => a.EventId == eventId && a.UserId == uid, ct);

        SignupStatus? mySignupStatus = null;
        Guid? mySignupId = null;
        IReadOnlyList<ViewerTeamOptionDto> teamsICanEnter = [];

        if (ev.ParticipantMode == ParticipantMode.Individuals)
        {
            var mine = await _db.EventSignups.AsNoTracking()
                .Where(s => s.EventId == eventId && s.UserId == uid)
                .Select(s => new { s.Id, s.Status })
                .FirstOrDefaultAsync(ct);
            if (mine is not null)
            {
                mySignupStatus = mine.Status;
                mySignupId = mine.Id;
            }
        }
        else
        {
            // Teams-only: the caller acts through teams they administer (feature 005 roles).
            var adminTeamIds = await _db.TeamMemberships.AsNoTracking()
                .Where(m => m.UserId == uid && m.Role == TeamRole.Admin)
                .Select(m => m.TeamId)
                .ToListAsync(ct);

            if (adminTeamIds.Count > 0)
            {
                var mine = await _db.EventSignups.AsNoTracking()
                    .Where(s => s.EventId == eventId && s.TeamId != null && adminTeamIds.Contains(s.TeamId!.Value))
                    .Select(s => new { s.Id, s.Status })
                    .FirstOrDefaultAsync(ct);
                if (mine is not null)
                {
                    mySignupStatus = mine.Status;
                    mySignupId = mine.Id;
                }

                // Teams the caller administers that haven't already entered this event.
                teamsICanEnter = await _db.Teams.AsNoTracking()
                    .Where(t => adminTeamIds.Contains(t.Id)
                        && !_db.EventSignups.Any(s => s.EventId == eventId && s.TeamId == t.Id))
                    .OrderBy(t => t.Name)
                    .Select(t => new ViewerTeamOptionDto(t.Id, t.Name, t.Slug))
                    .ToListAsync(ct);
            }
        }

        var viewer = new ViewerRelationDto(true, isAdmin, mySignupStatus, mySignupId, teamsICanEnter);
        return ToDetail(ev, occupied, viewer);
    }

    public Task<EditEventResult> EditAsync(Guid eventId, Guid actorUserId, EditEventRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException("EditAsync is implemented in User Story 4.");

    public Task<CancelEventStatus> CancelAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default) =>
        throw new NotImplementedException("CancelAsync is implemented in User Story 8.");

    // --- Helpers --------------------------------------------------------------

    private readonly record struct LocationResult(
        string? VenueName, string? Street, string? PostalCode, string? City, string? Country,
        string? VirtualLink, string LegacyLocation, string? Reason);

    private static LocationResult ResolveLocation(CreateEventRequest r)
    {
        if (r.LocationKind == LocationKind.InPerson)
        {
            var venue = Trimmed(r.VenueName);
            var street = Trimmed(r.Street);
            var postal = Trimmed(r.PostalCode);
            var city = Trimmed(r.City);
            var country = Trimmed(r.Country);
            if (street is null || postal is null || city is null || country is null)
            {
                return new LocationResult(null, null, null, null, null, null, string.Empty,
                    "An in-person event needs a full address, including country.");
            }

            // Legacy free-text location (still read by activity): "City, Country".
            var legacy = $"{city}, {country}";
            return new LocationResult(venue, street, postal, city, country, null, legacy, null);
        }

        var link = Trimmed(r.VirtualLink);
        if (link is null || !Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new LocationResult(null, null, null, null, null, null, string.Empty,
                "A virtual event needs a valid link (https://…).");
        }

        return new LocationResult(null, null, null, null, null, link, "Online", null);
    }

    private readonly record struct FeeResult(
        bool IsPaid, decimal? Amount, string? Currency, string? RecipientName, string? Iban, string? Reason);

    private static FeeResult ResolveFee(bool isPaid, decimal? amount, string? currency, string? recipient, string? iban)
    {
        if (!isPaid)
        {
            return new FeeResult(false, null, null, null, null, null);
        }

        var recip = Trimmed(recipient);
        var ibanValue = Trimmed(iban);
        if (recip is null || ibanValue is null)
        {
            return new FeeResult(true, null, null, null, null,
                "A paid event needs a payment recipient and IBAN.");
        }

        if (amount is < 0)
        {
            return new FeeResult(true, null, null, null, null, "The fee amount can't be negative.");
        }

        var cur = Trimmed(currency)?.ToUpperInvariant() ?? "EUR";
        return new FeeResult(true, amount, cur, recip, ibanValue, null);
    }

    private static EventDetailDto ToDetail(Event e, int occupiedSpots, ViewerRelationDto viewer) => new(
        e.Id,
        e.Name,
        e.Type,
        e.CustomTypeLabel,
        e.Description,
        e.StartsAt,
        e.EndsAt,
        e.LocationKind,
        e.VenueName,
        e.Street,
        e.PostalCode,
        e.City,
        e.Country,
        e.VirtualLink,
        e.ParticipantMode,
        e.ParticipationLimit,
        occupiedSpots,
        occupiedSpots >= e.ParticipationLimit,
        e.IsPaid,
        e.FeeAmount,
        e.FeeCurrency,
        e.FeeRecipientName,
        e.FeeIban,
        e.FeePaymentDeadline,
        e.Status,
        viewer);

    private static string? Trimmed(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static DateTime ToUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
    };
}
