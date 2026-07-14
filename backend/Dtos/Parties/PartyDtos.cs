using System.ComponentModel.DataAnnotations;
using JuggerHub.Dtos.Teams; // reuse UserRelation + InviteState (shared invitation concepts)
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Parties;

// --- Requests ---------------------------------------------------------------

/// <summary>Form a party for a teams-only event (feature 016). The caller must administer the team.</summary>
public sealed record FormPartyRequest(
    [Required] Guid EventId,
    [Required] Guid TeamId,
    [MaxLength(500)] string? Message);

/// <summary>Post a party news update (party admin).</summary>
public sealed record CreatePartyNewsRequest([Required, MaxLength(1000)] string Body);

/// <summary>Create a targeted co-admin invite for a team member.</summary>
public sealed record CreatePartyInviteRequest([Required] Guid UserId);

/// <summary>The roster group a members list requests.</summary>
public enum PartyRosterGroup
{
    In = 0,
    Declined = 1,
    NoResponse = 2,
}

// --- Responses --------------------------------------------------------------

/// <summary>The signed-in viewer's relationship to a party.</summary>
public enum PartyViewerState
{
    /// <summary>Not a member of the party's team (or anonymous).</summary>
    None = 0,

    /// <summary>A team member who has not answered the request.</summary>
    NoResponse = 1,

    /// <summary>In the crew.</summary>
    In = 2,

    /// <summary>Declined (may still rejoin while a spot is open).</summary>
    Declined = 3,

    /// <summary>A party admin (implies In).</summary>
    Admin = 4,
}

/// <summary>Readiness summary shown to the party admin before applying.</summary>
public sealed record PartyReadinessDto(bool EnoughToFieldTeam, int SpotsOpen, int Unanswered);

/// <summary>The full party detail / manage payload.</summary>
public sealed record PartyDto(
    Guid Id,
    Guid EventId,
    string EventName,
    EventType EventType,
    DateTime StartsAt,
    DateTime EndsAt,
    Guid TeamId,
    string TeamSlug,
    string TeamName,
    int RosterCap,
    int InCount,
    int DeclinedCount,
    int NoResponseCount,
    bool IsFull,
    PartyStatus Status,
    PartyViewerState MyState,
    PartyMemberRole? MyRole,
    string? Message,
    SignupStatus? AppliedGroup,
    PartyReadinessDto Readiness);

/// <summary>One roster row (In/Declined member, or a derived no-response team member).</summary>
public sealed record PartyMemberDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    PartyMemberRole? Role,
    bool IsYou,
    IReadOnlyList<Pompfe> Pompfen);

/// <summary>One party news item, newest-first (crew-only).</summary>
public sealed record PartyNewsDto(
    Guid Id,
    string AuthorDisplayName,
    PartyMemberRole AuthorRole,
    string Body,
    DateTime CreatedDate);

/// <summary>A pinned party-request card shown in the team space.</summary>
public sealed record PartyRequestCardDto(
    Guid PartyId,
    Guid EventId,
    string EventName,
    EventType EventType,
    DateTime StartsAt,
    DateTime EndsAt,
    int InCount,
    int RosterCap,
    string? Message,
    PartyViewerState MyState,
    bool IsFull,
    PartyStatus Status);

/// <summary>A team the signed-in viewer administers and could form/manage a party for.</summary>
public sealed record PartyContextTeamDto(
    Guid TeamId,
    string TeamName,
    string TeamSlug,
    bool IsAdmin,
    Guid? PartyId,
    bool CanForm,
    PartyViewerState MyState,
    int? InCount,
    int? RosterCap,
    PartyStatus? PartyStatus);

/// <summary>The viewer's party affordances for a teams-only event.</summary>
public sealed record PartyContextDto(
    ParticipantMode Mode,
    int? RosterCap,
    IReadOnlyList<PartyContextTeamDto> Teams);

/// <summary>One pending co-admin invitation in the admin list.</summary>
public sealed record PartyInvitationDto(
    Guid Id,
    InvitationKind Kind,
    string? TargetDisplayName,
    DateTime CreatedDate,
    DateTime ExpiresDate,
    InvitationStatus Status);

/// <summary>The party's current active co-admin invite link (admin re-displayable).</summary>
public sealed record PartyInviteLinkDto(string Url, string Token, DateTime ExpiresDate);

/// <summary>One team-member search candidate with their relation to the party's admin set.</summary>
public sealed record PartyInvitableUserDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    string? Hometown,
    UserRelation Relation);

/// <summary>Co-admin invite preview: party/team/event info + inviter + usability.</summary>
public sealed record PartyInvitePreviewDto(
    Guid PartyId,
    string TeamName,
    string EventName,
    DateTime StartsAt,
    string InviterDisplayName,
    InviteState State);
