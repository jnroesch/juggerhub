namespace JuggerHub.Entities;

/// <summary>
/// A Jugger event. Originally a minimal seeded record backing recent-activity; feature 006
/// extends it into a real, ownable event (type, schedule, location, participation, fee,
/// lifecycle) hung with admins, sign-ups, contacts, and news.
/// </summary>
/// <remarks>
/// <see cref="Location"/> is the legacy free-text field still read by profile/team
/// <c>ActivityItemDto</c>; the structured address (<see cref="Street"/>…<see cref="Country"/>)
/// is separate and used only by the event page. Live sign-ups live in <see cref="EventSignup"/>
/// (distinct from the historical <see cref="EventParticipation"/>).
/// </remarks>
public sealed class Event : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public EventType Type { get; set; }

    /// <summary>Free-text label when <see cref="Type"/> is <see cref="EventType.Other"/>; else null.</summary>
    public string? CustomTypeLabel { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>When the event starts (UTC). Replaces the old single <c>Date</c>.</summary>
    public DateTime StartsAt { get; set; }

    /// <summary>When the event ends (UTC); on or after <see cref="StartsAt"/> (multi-day allowed).</summary>
    public DateTime EndsAt { get; set; }

    public LocationKind LocationKind { get; set; }

    // --- In-person address (LocationKind == InPerson) ---
    public string? VenueName { get; set; }
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    /// <summary>Join link for a virtual event (LocationKind == Virtual).</summary>
    public string? VirtualLink { get; set; }

    /// <summary>Legacy free-text location, retained for existing activity display (see remarks).</summary>
    public string Location { get; set; } = string.Empty;

    public ParticipantMode ParticipantMode { get; set; }

    /// <summary>How many teams/people may hold a spot. Positive; never below current occupied count when edited.</summary>
    public int ParticipationLimit { get; set; }

    /// <summary>
    /// Players-per-team cap for a <see cref="ParticipantMode.Teams"/> event — the maximum size of a
    /// team's <see cref="Party"/> (feature 016). Set at creation: default 8, minimum 5, no upper
    /// cap beyond a sane guard. Null for an individuals-only event.
    /// </summary>
    public int? RosterCap { get; set; }

    // --- Fee (paid events; out-of-band bank transfer, no in-app payment) ---
    public bool IsPaid { get; set; }
    public decimal? FeeAmount { get; set; }
    public string? FeeCurrency { get; set; }
    public string? FeeRecipientName { get; set; }
    public string? FeeIban { get; set; }
    public DateOnly? FeePaymentDeadline { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Published;

    /// <summary>Set when the event is cancelled (UTC); the state is terminal/irreversible.</summary>
    public DateTime? CancelledDate { get; set; }

    public ICollection<EventSignup> Signups { get; set; } = [];

    public ICollection<EventAdmin> Admins { get; set; } = [];

    public ICollection<EventAdminInvitation> Invitations { get; set; } = [];

    public ICollection<EventContact> Contacts { get; set; } = [];

    public ICollection<EventNewsPost> News { get; set; } = [];

    /// <summary>Historical attendance records (recent activity) — distinct from <see cref="Signups"/>.</summary>
    public ICollection<EventParticipation> Participations { get; set; } = [];
}
