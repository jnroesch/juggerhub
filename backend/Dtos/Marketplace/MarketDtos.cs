using System.ComponentModel.DataAnnotations;
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Marketplace;

// --- Requests ---------------------------------------------------------------

/// <summary>Post or edit the caller's free-agent listing for an event (feature 017).</summary>
public sealed record PostListingRequest(
    [Required, MinLength(1)] IReadOnlyList<Pompfe> Positions,
    [Required, MaxLength(280)] string Pitch);

/// <summary>A free agent applies to a recruiting party, optionally naming what they'd play.</summary>
public sealed record ApplyRequest(IReadOnlyList<Pompfe>? Positions);

/// <summary>A party admin invites a user (board or direct), optionally naming the asked position(s).</summary>
public sealed record InviteRequest(
    [Required] Guid UserId,
    IReadOnlyList<Pompfe>? Positions);

/// <summary>Toggle/set a party's recruiting state (party admin).</summary>
public sealed record SetRecruitingRequest(
    bool IsRecruiting,
    [Range(0, 100)] int SpotsAdvertised,
    IReadOnlyList<Pompfe>? PositionsNeeded,
    [MaxLength(500)] string? Blurb);

// --- Board cards (public) ---------------------------------------------------

/// <summary>One free agent on the board's free-agents side (public event data).</summary>
public sealed record MarketListingCardDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    bool HasAvatar,
    IReadOnlyList<Pompfe> Positions,
    string Pitch);

/// <summary>One recruiting party on the board's parties side (public event data).</summary>
public sealed record RecruitingPartyCardDto(
    Guid PartyId,
    Guid TeamId,
    string TeamName,
    string TeamSlug,
    Guid EventId,
    int OpenSpots,
    int RosterCap,
    int InCount,
    IReadOnlyList<Pompfe> PositionsNeeded,
    string? Blurb);

// --- The caller's own listing ----------------------------------------------

/// <summary>The caller's own free-agent listing for an event.</summary>
public sealed record MarketListingDto(
    Guid Id,
    Guid EventId,
    IReadOnlyList<Pompfe> Positions,
    string Pitch);

/// <summary>One of the caller's active free-agent listings, with event context (dashboard).</summary>
public sealed record MyListingDto(
    Guid Id,
    Guid EventId,
    string EventName,
    DateTime StartsAt,
    IReadOnlyList<Pompfe> Positions,
    string Pitch);

// --- Requests in the shared inbox ------------------------------------------

/// <summary>One application/invite as seen in either inbox (carries both party and user identities).</summary>
public sealed record MarketRequestDto(
    Guid Id,
    Guid PartyId,
    string TeamName,
    string TeamSlug,
    Guid EventId,
    string EventName,
    Guid UserId,
    string Handle,
    string DisplayName,
    bool HasAvatar,
    MarketRequestDirection Direction,
    IReadOnlyList<Pompfe> Positions,
    MarketRequestStatus Status,
    DateTime CreatedDate);

/// <summary>A compact request row for the cross-event dashboard market module.</summary>
public sealed record MyMarketRequestDto(
    Guid Id,
    Guid PartyId,
    string TeamName,
    Guid EventId,
    string EventName,
    MarketRequestDirection Direction,
    IReadOnlyList<Pompfe> Positions,
    MarketRequestStatus Status,
    DateTime CreatedDate);

// --- The caller's market context for one event -----------------------------

/// <summary>A recruiting party the caller administers here (enables the Invite affordance + target).</summary>
public sealed record MyMarketAdminPartyDto(
    Guid PartyId,
    string TeamName,
    string TeamSlug,
    bool IsRecruiting,
    int OpenSpots);

/// <summary>The signed-in caller's marketplace context for one event (drives client affordances).</summary>
public sealed record MyMarketDto(
    Guid UserId,
    ParticipantMode Mode,
    bool Eligible,
    string? IneligibleReason,
    MarketListingDto? MyListing,
    IReadOnlyList<MyMarketAdminPartyDto> AdminParties,
    IReadOnlyList<MarketRequestDto> InvitesToAnswer,
    IReadOnlyList<MarketRequestDto> MyApplications);

// --- Recruiting settings ----------------------------------------------------

/// <summary>A party's recruiting settings + live fill for the manage screen.</summary>
public sealed record RecruitingSettingsDto(
    Guid PartyId,
    bool IsRecruiting,
    int SpotsAdvertised,
    IReadOnlyList<Pompfe> PositionsNeeded,
    string? Blurb,
    int RosterCap,
    int InCount,
    int OpenSpots);

// --- Direct-invite search ---------------------------------------------------

/// <summary>The candidate's relation to a direct invite from this party.</summary>
public enum MarketInviteRelation
{
    /// <summary>Eligible and not yet invited — can be invited.</summary>
    Invitable = 0,

    /// <summary>Already has a pending invite from this party.</summary>
    Invited = 1,

    /// <summary>Already In a party for this event — cannot be invited ("one event, one crew").</summary>
    Ineligible = 2,
}

/// <summary>One user-search candidate for a direct invite, with their relation to this party.</summary>
public sealed record MarketInvitableUserDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    string? Hometown,
    bool HasAvatar,
    MarketInviteRelation Relation);
