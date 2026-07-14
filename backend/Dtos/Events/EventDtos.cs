using System.ComponentModel.DataAnnotations;
using JuggerHub.Dtos.Teams; // reuse UserRelation + InviteState (shared invitation concepts)
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Events;

// Validation attributes go on record constructor parameters (MVC reads parameter-level
// metadata for positional records). Conditional rules (country iff in-person, link iff virtual,
// recipient/IBAN iff paid, end >= start, limit > 0) are enforced server-side in EventService.

// --- Requests ---------------------------------------------------------------

/// <summary>Create an event via the wizard. Conditional fields validated server-side.</summary>
public sealed record CreateEventRequest(
    [Required, MinLength(3), MaxLength(120)] string Name,
    [Required] EventType Type,
    [MaxLength(40)] string? CustomTypeLabel,
    [Required, MinLength(1), MaxLength(4000)] string Description,
    [Required] DateTime StartsAt,
    [Required] DateTime EndsAt,
    [Required] LocationKind LocationKind,
    [MaxLength(120)] string? VenueName,
    [MaxLength(160)] string? Street,
    [MaxLength(20)] string? PostalCode,
    [MaxLength(120)] string? City,
    [MaxLength(80)] string? Country,
    [MaxLength(500)] string? VirtualLink,
    [Required] ParticipantMode ParticipantMode,
    [Range(1, int.MaxValue)] int ParticipationLimit,
    bool IsPaid,
    decimal? FeeAmount,
    [MaxLength(3)] string? FeeCurrency,
    [MaxLength(120)] string? FeeRecipientName,
    [MaxLength(34)] string? FeeIban,
    DateOnly? FeePaymentDeadline,
    // Players-per-team cap for a teams-only event (feature 016): default 8, min 5. Ignored
    // (must be null) for individuals-only. Bounds enforced server-side in EventService.
    int? RosterCap = null);

/// <summary>Edit an event (admin). ParticipantMode is refused when sign-ups exist; the limit
/// may not drop below the current occupied count (both enforced server-side).</summary>
public sealed record EditEventRequest(
    [Required, MinLength(3), MaxLength(120)] string Name,
    [Required] EventType Type,
    [MaxLength(40)] string? CustomTypeLabel,
    [Required, MinLength(1), MaxLength(4000)] string Description,
    [Required] DateTime StartsAt,
    [Required] DateTime EndsAt,
    [Required] LocationKind LocationKind,
    [MaxLength(120)] string? VenueName,
    [MaxLength(160)] string? Street,
    [MaxLength(20)] string? PostalCode,
    [MaxLength(120)] string? City,
    [MaxLength(80)] string? Country,
    [MaxLength(500)] string? VirtualLink,
    [Range(1, int.MaxValue)] int ParticipationLimit,
    bool IsPaid,
    decimal? FeeAmount,
    [MaxLength(3)] string? FeeCurrency,
    [MaxLength(120)] string? FeeRecipientName,
    [MaxLength(34)] string? FeeIban,
    DateOnly? FeePaymentDeadline);

/// <summary>Sign up. <see cref="TeamId"/> is required for teams-only events (the caller must
/// administer that team) and omitted for individuals-only (the subject is the caller).</summary>
public sealed record SignupRequest(Guid? TeamId);

/// <summary>Add/update a contact. At least one of phone/email is required (server-guarded).</summary>
public sealed record CreateContactRequest(
    [Required, MaxLength(120)] string Name,
    [Required, MaxLength(80)] string Role,
    [MaxLength(40)] string? Phone,
    [MaxLength(256)] string? Email);

/// <summary>Post a news update (admin).</summary>
public sealed record CreateNewsRequest([Required, MaxLength(2000)] string Body);

/// <summary>Create a targeted co-admin invite for a specific user.</summary>
public sealed record CreateEventInviteRequest([Required] Guid UserId);

// --- Responses --------------------------------------------------------------

/// <summary>A team the signed-in viewer administers and could enter (teams-only events).</summary>
public sealed record ViewerTeamOptionDto(Guid TeamId, string Name, string Slug);

/// <summary>The signed-in viewer's relationship to an event (anonymous → all false/empty).</summary>
public sealed record ViewerRelationDto(
    bool IsAuthenticated,
    bool IsAdmin,
    SignupStatus? MySignupStatus,
    Guid? MySignupId,
    IReadOnlyList<ViewerTeamOptionDto> TeamsICanEnter);

/// <summary>The full public event page payload (fee block is public payment instructions).</summary>
public sealed record EventDetailDto(
    Guid Id,
    string Name,
    EventType Type,
    string? CustomTypeLabel,
    string Description,
    DateTime StartsAt,
    DateTime EndsAt,
    LocationKind LocationKind,
    string? VenueName,
    string? Street,
    string? PostalCode,
    string? City,
    string? Country,
    string? VirtualLink,
    ParticipantMode ParticipantMode,
    int ParticipationLimit,
    int OccupiedSpots,
    bool IsFull,
    bool IsPaid,
    decimal? FeeAmount,
    string? FeeCurrency,
    string? FeeRecipientName,
    string? FeeIban,
    DateOnly? FeePaymentDeadline,
    EventStatus Status,
    ViewerRelationDto Viewer,
    // Players-per-team cap for teams-only events (feature 016); null for individuals-only.
    int? RosterCap = null);

/// <summary>One participant row (individual or team) in a group.</summary>
public sealed record SignupDto(
    Guid Id,
    SignupStatus Status,
    DateTime JoinedAt,
    string? UserHandle,
    string? UserDisplayName,
    string? TeamSlug,
    string? TeamName);

/// <summary>One public contact.</summary>
public sealed record EventContactDto(Guid Id, string Name, string Role, string? Phone, string? Email);

/// <summary>One public news item, newest-first.</summary>
public sealed record EventNewsDto(Guid Id, string AuthorDisplayName, string Body, DateTime CreatedDate);

/// <summary>One event admin.</summary>
public sealed record EventAdminDto(Guid UserId, string Handle, string DisplayName);

/// <summary>One pending co-admin invitation in the admin list.</summary>
public sealed record EventInvitationDto(
    Guid Id,
    InvitationKind Kind,
    string? TargetDisplayName,
    DateTime CreatedDate,
    DateTime ExpiresDate,
    InvitationStatus Status);

/// <summary>The event's current active co-admin invite link (admin re-displayable).</summary>
public sealed record EventInviteLinkDto(string Url, string Token, DateTime ExpiresDate);

/// <summary>One user-search candidate with their relation to the event's admin set.</summary>
public sealed record EventInvitableUserDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    string? Hometown,
    UserRelation Relation);

/// <summary>Anonymous co-admin invite preview: public event info + inviter + usability.</summary>
public sealed record EventInvitePreviewDto(
    Guid EventId,
    string EventName,
    DateTime StartsAt,
    string InviterDisplayName,
    InviteState State);
