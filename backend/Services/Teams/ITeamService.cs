using JuggerHub.Common;
using JuggerHub.Dtos.Teams;
using JuggerHub.Entities;

namespace JuggerHub.Services.Teams;

/// <summary>Outcome of creating a team.</summary>
public enum CreateTeamStatus
{
    Created,
    InvalidName,
    InvalidSlug,
    SlugTaken,
    InvalidCity,
}

/// <summary>Result of a create attempt (carries the created team or a reason).</summary>
public sealed record CreateTeamResult(CreateTeamStatus Status, TeamDetailDto? Team, string? Reason)
{
    public static CreateTeamResult Ok(TeamDetailDto team) => new(CreateTeamStatus.Created, team, null);

    public static CreateTeamResult Fail(CreateTeamStatus status, string reason) => new(status, null, reason);
}

/// <summary>Outcome of a membership/role mutation.</summary>
public enum MemberOpStatus
{
    Ok,
    NotFoundOrNotMember,
    Forbidden,
    MemberNotFound,
    LastAdmin,
}

/// <summary>Result of a membership/role mutation (carries the updated member on success).</summary>
public sealed record MemberOpResult(MemberOpStatus Status, string? Reason, TeamMemberDto? Member = null)
{
    public static MemberOpResult Ok(TeamMemberDto? member = null) => new(MemberOpStatus.Ok, null, member);

    public static MemberOpResult Fail(MemberOpStatus status, string? reason = null) => new(status, reason);
}

/// <summary>Outcome of a team delete.</summary>
public enum DeleteTeamStatus
{
    Deleted,
    NotFoundOrNotMember,
    Forbidden,
}

/// <summary>Outcome of a team-settings update (feature 007).</summary>
public enum UpdateTeamSettingsStatus
{
    Updated,
    Forbidden,
    NotFoundOrNotMember,
}

/// <summary>Outcome of a team-details save (feature 061).</summary>
public enum TeamDetailsStatus
{
    Updated,
    Invalid,
    Forbidden,
    NotFoundOrNotMember,
}

/// <summary>
/// Result of a team-details save: the updated team, or why it was refused. <see cref="Code"/> and
/// <see cref="LinkIndex"/> are what the client renders; <see cref="Reason"/> is English for API readers.
/// </summary>
public sealed record TeamDetailsResult(
    TeamDetailsStatus Status, TeamDetailDto? Team, TeamDetailsCode? Code, int? LinkIndex, string? Reason)
{
    public static TeamDetailsResult Ok(TeamDetailDto team) => new(TeamDetailsStatus.Updated, team, null, null, null);

    public static TeamDetailsResult Fail(TeamDetailsStatus status) => new(status, null, null, null, null);

    public static TeamDetailsResult Refused(TeamDetailsProblem problem) =>
        new(TeamDetailsStatus.Invalid, null, problem.Code, problem.LinkIndex, problem.Reason);
}

/// <summary>
/// Team domain service: create + slug checks, member-gated reads (detail/roster), the public
/// projection, role/remove/step-down (under the last-admin guard), and delete. Accesses EF
/// Core directly (no repository layer) and returns DTOs.
/// </summary>
public interface ITeamService
{
    /// <summary>Format + reserved + uniqueness check for live UX (availability endpoint).</summary>
    Task<SlugAvailabilityDto> CheckSlugAsync(string rawSlug, CancellationToken ct = default);

    /// <summary>Create a team; the creator becomes its first admin.</summary>
    Task<CreateTeamResult> CreateAsync(Guid userId, CreateTeamRequest request, CancellationToken ct = default);

    /// <summary>Members-only team header; null when unknown OR the caller is not a member.</summary>
    Task<TeamDetailDto?> GetDetailAsync(string slug, Guid userId, CancellationToken ct = default);

    /// <summary>Anonymous public team info; null when unknown.</summary>
    Task<TeamPublicDto?> GetPublicAsync(string slug, CancellationToken ct = default);

    /// <summary>The public team page (feature 009): overview + the viewer's relation + capped public
    /// roster, recent activity, and upcoming trainings. Anonymous when <paramref name="viewerUserId"/>
    /// is null. Null when no team has that slug.</summary>
    Task<TeamPublicDetailDto?> GetPublicDetailAsync(string slug, Guid? viewerUserId, CancellationToken ct = default);

    /// <summary>Members-only roster (paginated); null when unknown OR the caller is not a member.</summary>
    Task<PagedResult<TeamMemberDto>?> GetRosterAsync(string slug, Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Promote/demote a member (admin only; last-admin guarded).</summary>
    Task<MemberOpResult> SetRoleAsync(string slug, Guid actorUserId, Guid targetUserId, TeamRole role, CancellationToken ct = default);

    /// <summary>Remove a member (admin only) or leave (self); last-admin guarded.</summary>
    Task<MemberOpResult> RemoveMemberAsync(string slug, Guid actorUserId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>Step down from admin to member (self); last-admin guarded.</summary>
    Task<MemberOpResult> StepDownAsync(string slug, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Delete the team (admin only); cascades roster/invites/news, preserves event history.</summary>
    Task<DeleteTeamStatus> DeleteAsync(string slug, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Update the team's self-managed settings (admin only). Feature 007.</summary>
    Task<UpdateTeamSettingsStatus> UpdateSettingsAsync(
        string slug, Guid actorUserId, UpdateTeamSettingsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Replace the team's name, type, city, description and links as a whole (admin only). Feature
    /// 061. A rename also brings the team's name up to date in every delivered alert that names it
    /// and in every tournament placement connected to it — in the same transaction, silently.
    /// </summary>
    Task<TeamDetailsResult> UpdateDetailsAsync(
        string slug, Guid actorUserId, UpdateTeamDetailsRequest request, CancellationToken ct = default);
}
