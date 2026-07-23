using JuggerHub.Common;
using JuggerHub.Data;
using JuggerHub.Dtos.Events;
using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly EventAdminGuard _guard;
    private readonly Email.EventEmailService _email;
    private readonly Chat.IChatConversationService _chat;
    private readonly ILogger<EventService> _logger;

    public EventService(
        AppDbContext db,
        IOptions<EventOptions> options,
        EventCapacity capacity,
        EventAdminGuard guard,
        Email.EventEmailService email,
        Chat.IChatConversationService chat,
        ILogger<EventService> logger)
    {
        _db = db;
        _options = options.Value;
        _capacity = capacity;
        _guard = guard;
        _email = email;
        _chat = chat;
        _logger = logger;
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

        // Roster cap (feature 016): a per-team party size, teams-only. Default 8, minimum 5.
        int? rosterCap = null;
        if (request.ParticipantMode == ParticipantMode.Teams)
        {
            rosterCap = request.RosterCap ?? 8;
            if (rosterCap < 5 || rosterCap > 100)
            {
                return CreateEventResult.Fail("Set a roster cap of at least 5 players per team.");
            }
        }
        else if (request.RosterCap is not null)
        {
            return CreateEventResult.Fail("A roster cap only applies to teams-only events.");
        }

        var locationResult = ResolveLocation(request.LocationKind, request.VenueName, request.Street,
            request.PostalCode, request.City, request.Country, request.VirtualLink);
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
            RosterCap = rosterCap,
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

    public async Task<EditEventResult> EditAsync(Guid eventId, Guid actorUserId, EditEventRequest request, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return EditEventResult.Fail(EditEventStatus.NotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return EditEventResult.Fail(EditEventStatus.Forbidden, "Only an event admin can edit it.");
        }

        var ev = await _db.Events.FirstAsync(e => e.Id == eventId, ct);

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length < _options.NameMinLength || name.Length > _options.NameMaxLength)
        {
            return EditEventResult.Fail(EditEventStatus.Invalid, $"Use an event name of {_options.NameMinLength}–{_options.NameMaxLength} characters.");
        }

        string? customLabel = null;
        if (request.Type == EventType.Other)
        {
            customLabel = (request.CustomTypeLabel ?? string.Empty).Trim();
            if (customLabel.Length is 0 or > 40)
            {
                return EditEventResult.Fail(EditEventStatus.Invalid, "A custom event type needs a short label (1–40 characters).");
            }
        }

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length is 0 || description.Length > _options.DescriptionMaxLength)
        {
            return EditEventResult.Fail(EditEventStatus.Invalid, $"Add a description of up to {_options.DescriptionMaxLength} characters.");
        }

        var startsAt = ToUtc(request.StartsAt);
        var endsAt = ToUtc(request.EndsAt);
        if (endsAt < startsAt)
        {
            return EditEventResult.Fail(EditEventStatus.Invalid, "The event must end on or after it starts.");
        }

        if (request.ParticipationLimit < 1 || request.ParticipationLimit > _options.MaxParticipationLimit)
        {
            return EditEventResult.Fail(EditEventStatus.Invalid, $"Set a participation limit between 1 and {_options.MaxParticipationLimit}.");
        }

        // The limit may rise freely but never drop below the current occupied count.
        var occupied = await _capacity.OccupiedCountAsync(eventId, ct);
        if (request.ParticipationLimit < occupied)
        {
            return EditEventResult.Fail(EditEventStatus.LimitBelowOccupied,
                $"There are already {occupied} taking part — the limit can't be lower than that.");
        }

        var locationResult = ResolveLocation(request.LocationKind, request.VenueName, request.Street,
            request.PostalCode, request.City, request.Country, request.VirtualLink);
        if (locationResult.Reason is not null)
        {
            return EditEventResult.Fail(EditEventStatus.Invalid, locationResult.Reason);
        }

        var feeResult = ResolveFee(request.IsPaid, request.FeeAmount, request.FeeCurrency, request.FeeRecipientName, request.FeeIban);
        if (feeResult.Reason is not null)
        {
            return EditEventResult.Fail(EditEventStatus.Invalid, feeResult.Reason);
        }

        // ParticipantMode is intentionally absent from the edit contract — it is immutable after
        // creation (a stricter form of "mode locked once sign-ups exist", spec FR-030).
        ev.Name = name;
        ev.Type = request.Type;
        ev.CustomTypeLabel = customLabel;
        ev.Description = description;
        ev.StartsAt = startsAt;
        ev.EndsAt = endsAt;
        ev.LocationKind = request.LocationKind;
        ev.VenueName = locationResult.VenueName;
        ev.Street = locationResult.Street;
        ev.PostalCode = locationResult.PostalCode;
        ev.City = locationResult.City;
        ev.Country = locationResult.Country;
        ev.VirtualLink = locationResult.VirtualLink;
        ev.Location = locationResult.LegacyLocation;
        ev.ParticipationLimit = request.ParticipationLimit;
        ev.IsPaid = feeResult.IsPaid;
        ev.FeeAmount = feeResult.Amount;
        ev.FeeCurrency = feeResult.Currency;
        ev.FeeRecipientName = feeResult.RecipientName;
        ev.FeeIban = feeResult.Iban;
        ev.FeePaymentDeadline = request.IsPaid ? request.FeePaymentDeadline : null;
        await _db.SaveChangesAsync(ct);

        var viewer = new ViewerRelationDto(true, true, null, null, []);
        return EditEventResult.Ok(ToDetail(ev, occupied, viewer));
    }

    public async Task<CancelEventStatus> CancelAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default)
    {
        var access = await _guard.ResolveAsync(eventId, actorUserId, ct);
        if (access is null)
        {
            return CancelEventStatus.NotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return CancelEventStatus.Forbidden;
        }

        if (access.Value.IsCancelled)
        {
            return CancelEventStatus.AlreadyCancelled;
        }

        var ev = await _db.Events.FirstAsync(e => e.Id == eventId, ct);
        ev.Status = EventStatus.Cancelled;
        ev.CancelledDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Close any "contact the admins" threads for this event — a cancelled event's inquiries become
        // read-only history (feature 027, FR-014).
        await _chat.ArchiveInquiriesForEventAsync(eventId, ct);

        await NotifyCancellationAsync(eventId, ev.Name, ct);
        return CancelEventStatus.Cancelled;
    }

    /// <summary>
    /// Email everyone joined/awaiting/waiting that the event is cancelled: individual sign-ups →
    /// the user; team sign-ups → the team's admins. Best-effort (a mail failure never rolls back
    /// the cancel — FR-032).
    /// </summary>
    private async Task NotifyCancellationAsync(Guid eventId, string eventName, CancellationToken ct)
    {
        // Individual participants.
        var individuals = await _db.EventSignups.AsNoTracking()
            .Where(s => s.EventId == eventId && s.UserId != null)
            .Select(s => new { Email = s.User!.Email, Name = s.User!.Profile!.DisplayName })
            .ToListAsync(ct);

        // Team participants → each team's admins.
        var teamIds = await _db.EventSignups.AsNoTracking()
            .Where(s => s.EventId == eventId && s.TeamId != null)
            .Select(s => s.TeamId!.Value)
            .ToListAsync(ct);

        var teamAdmins = teamIds.Count == 0
            ? []
            : await _db.TeamMemberships.AsNoTracking()
                .Where(m => teamIds.Contains(m.TeamId) && m.Role == Entities.TeamRole.Admin)
                .Select(m => new { Email = m.User.Email, Name = m.User.Profile!.DisplayName })
                .ToListAsync(ct);

        var recipients = individuals.Concat(teamAdmins)
            .Where(r => !string.IsNullOrEmpty(r.Email))
            .GroupBy(r => r.Email!, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        foreach (var r in recipients)
        {
            try
            {
                await _email.SendCancellationEmailAsync(r.Email!, r.Name ?? "there", eventName, eventId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send cancellation email to a recipient for event {EventId}", eventId);
            }
        }
    }

    // --- Helpers --------------------------------------------------------------

    private readonly record struct LocationResult(
        string? VenueName, string? Street, string? PostalCode, string? City, string? Country,
        string? VirtualLink, string LegacyLocation, string? Reason);

    private static LocationResult ResolveLocation(
        LocationKind kind, string? venueName, string? street, string? postalCode,
        string? city, string? country, string? virtualLink)
    {
        if (kind == LocationKind.InPerson)
        {
            var venue = Trimmed(venueName);
            var streetValue = Trimmed(street);
            var postalValue = Trimmed(postalCode);
            var cityValue = Trimmed(city);
            var countryValue = Trimmed(country);
            if (streetValue is null || postalValue is null || cityValue is null || countryValue is null)
            {
                return new LocationResult(null, null, null, null, null, null, string.Empty,
                    "An in-person event needs a full address, including country.");
            }

            // Legacy free-text location (still read by activity): "City, Country".
            var legacy = $"{cityValue}, {countryValue}";
            return new LocationResult(venue, streetValue, postalValue, cityValue, countryValue, null, legacy, null);
        }

        var link = Trimmed(virtualLink);
        // Be lenient: accept links without an explicit scheme (e.g. "zoom.us/j/123") by
        // defaulting to https, and store the normalized absolute URL.
        if (link is not null && !link.Contains("://", StringComparison.Ordinal))
        {
            link = "https://" + link;
        }

        if (link is null || !Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            return new LocationResult(null, null, null, null, null, null, string.Empty,
                "Add a link like zoom.us/… or https://meet.… so people can join.");
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
        viewer,
        e.RosterCap);

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
