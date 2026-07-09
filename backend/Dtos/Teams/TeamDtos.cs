using System.ComponentModel.DataAnnotations;
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Teams;

// Validation attributes go on record constructor parameters (MVC reads parameter-level
// metadata for positional records) — matching the Dtos/Auth + Dtos/Profile convention.

/// <summary>How a searched user relates to a team (drives the invite button state).</summary>
public enum UserRelation
{
    Invitable,
    Invited,
    Member,
}

/// <summary>The usability of an opened invitation (drives the accept screen).</summary>
public enum InviteState
{
    Usable,
    Expired,
    Invalid,
}

// --- Requests ---------------------------------------------------------------

/// <summary>Create a team. The <see cref="Slug"/> format/reserved/uniqueness and the
/// city rule (required iff CityTeam) are enforced server-side.</summary>
public sealed record CreateTeamRequest(
    [Required, MinLength(2), MaxLength(50)] string Name,
    [Required, MaxLength(30), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, numbers, and single hyphens.")] string Slug,
    [Required] TeamType Type,
    [MaxLength(80)] string? City);

/// <summary>Set a member's role (promote/demote). Subject to the last-admin guard.</summary>
public sealed record SetMemberRoleRequest([Required] TeamRole Role);

/// <summary>Create a targeted invite for a specific user.</summary>
public sealed record CreateTargetedInviteRequest([Required] Guid UserId);

/// <summary>Admin update to a team's self-managed settings (feature 007). Currently the
/// beginners-welcome recruitment flag surfaced in browse.</summary>
public sealed record UpdateTeamSettingsRequest([Required] bool BeginnersWelcome);

/// <summary>Post a news update to a team (feature 010, admin-only). Body length matches the
/// <c>TeamNewsPost</c> column limit; posting fans out an in-app notification to the roster.</summary>
public sealed record PostTeamNewsRequest([Required, MinLength(1), MaxLength(1000)] string Body);

// --- Responses --------------------------------------------------------------

/// <summary>Members-only team header. Roster/news are fetched from their own endpoints.</summary>
public sealed record TeamDetailDto(
    string Slug,
    string Name,
    TeamType Type,
    string? City,
    int MemberCount,
    TeamRole MyRole,
    // Feature 007 — self-managed recruitment flag, editable in team settings.
    bool BeginnersWelcome = false);

/// <summary>Anonymous public team info. MUST NOT contain roster identities or news.</summary>
public sealed record TeamPublicDto(
    string Slug,
    string Name,
    TeamType Type,
    string? City,
    int MemberCount);

/// <summary>How the viewer relates to a team (feature 009) — drives which sections and which
/// join action the public page shows. Decided server-side from the session.</summary>
public enum TeamViewerRelation
{
    Anonymous,
    NonMember,
    Requested,
    Member,
    Admin,
}

/// <summary>The public team page (feature 009): overview + the viewer's relation + capped public
/// roster, recent activity, and upcoming trainings. Carries NO contact details or news.</summary>
public sealed record TeamPublicDetailDto(
    string Slug,
    string Name,
    TeamType Type,
    string? City,
    int MemberCount,
    bool BeginnersWelcome,
    bool IsActive,
    TeamViewerRelation ViewerRelation,
    IReadOnlyList<PublicMemberDto> Roster,
    IReadOnlyList<JuggerHub.Dtos.Profile.ActivityItemDto> RecentActivity,
    IReadOnlyList<TrainingDto> UpcomingTrainings,
    // Feature 012 — the team's earned badges & achievements (active only).
    IReadOnlyList<JuggerHub.Dtos.Recognition.EarnedRecognitionDto> Badges,
    IReadOnlyList<JuggerHub.Dtos.Recognition.EarnedRecognitionDto> Achievements);

/// <summary>One public roster row — identity + position only, never contact details.</summary>
public sealed record PublicMemberDto(
    string Handle,
    string DisplayName,
    TeamRole Role,
    bool HasAvatar,
    IReadOnlyList<Pompfe> Pompfen);

/// <summary>An upcoming event the team is entered in (a "training"/match), shown publicly.</summary>
public sealed record TrainingDto(Guid EventId, string Name, DateTime StartsAt, string LocationLabel);

/// <summary>One pending join request in the admin queue (feature 009).</summary>
public sealed record JoinRequestDto(
    Guid Id,
    string Handle,
    string DisplayName,
    bool HasAvatar,
    DateTime CreatedDate);

/// <summary>One roster row (member-only view).</summary>
public sealed record TeamMemberDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    TeamRole Role,
    bool HasAvatar,
    IReadOnlyList<Pompfe> Pompfen);

/// <summary>One read-only news-feed item (author role resolved from their membership).</summary>
public sealed record TeamNewsDto(
    string AuthorDisplayName,
    string AuthorHandle,
    TeamRole AuthorRole,
    DateTime CreatedDate,
    string Body);

/// <summary>One pending invitation in the admin list (link or targeted).</summary>
public sealed record TeamInvitationDto(
    Guid Id,
    InvitationKind Kind,
    string? TargetDisplayName,
    DateTime CreatedDate,
    DateTime ExpiresDate,
    InvitationStatus Status);

/// <summary>The team's current active invite link (admin re-displayable).</summary>
public sealed record InviteLinkDto(string Url, string Token, DateTime ExpiresDate);

/// <summary>One user-search candidate with their relation to the team.</summary>
public sealed record InvitableUserDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    string? City,
    UserRelation Relation);

/// <summary>Anonymous invite preview: public team info + inviter + usability state.</summary>
public sealed record InvitePreviewDto(
    string TeamName,
    string TeamSlug,
    TeamType Type,
    string? City,
    int MemberCount,
    string InviterDisplayName,
    InviteState State);

/// <summary>Result of accepting an invite (where to land).</summary>
public sealed record AcceptInviteResultDto(string TeamSlug);

/// <summary>Result of a live team-slug availability/format check (UX aid; not a security boundary).</summary>
public sealed record SlugAvailabilityDto(string Slug, string Normalized, bool Available, string? Reason);
