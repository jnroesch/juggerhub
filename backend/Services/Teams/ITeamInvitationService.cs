using JuggerHub.Common;
using JuggerHub.Dtos.Teams;

namespace JuggerHub.Services.Teams;

/// <summary>Admin-gate outcome shared by the invitation admin operations.</summary>
public enum InviteAdminStatus
{
    Ok,
    NotFoundOrNotMember,
    Forbidden,
}

/// <summary>Result of getting/rotating the active invite link.</summary>
public sealed record InviteLinkResult(InviteAdminStatus Status, InviteLinkDto? Link);

/// <summary>Result of listing pending invitations.</summary>
public sealed record InviteListResult(InviteAdminStatus Status, PagedResult<TeamInvitationDto>? Page);

/// <summary>Result of a user search for inviting.</summary>
public sealed record UserSearchResult(InviteAdminStatus Status, PagedResult<InvitableUserDto>? Page);

/// <summary>Outcome of creating a targeted invite.</summary>
public enum TargetedInviteStatus
{
    Created,
    AlreadyInvited,
    AlreadyMember,
    TargetNotFound,
    NotFoundOrNotMember,
    Forbidden,
}

/// <summary>Result of a targeted-invite create (carries the invite on Created/AlreadyInvited).</summary>
public sealed record TargetedInviteResult(TargetedInviteStatus Status, TeamInvitationDto? Invitation);

/// <summary>Outcome of revoking an invitation.</summary>
public enum RevokeStatus
{
    Revoked,
    NotFound,
    Forbidden,
    NotFoundOrNotMember,
}

/// <summary>Outcome of accepting an invitation.</summary>
public enum AcceptStatus
{
    Joined,
    AlreadyMember,
    NotUsable,
    NotFound,
}

/// <summary>Result of accepting (carries the team slug to land on).</summary>
public sealed record AcceptResult(AcceptStatus Status, string? TeamSlug);

/// <summary>Outcome of declining an invitation.</summary>
public enum DeclineStatus
{
    Declined,
    NotFound,
}

/// <summary>
/// Team invitation domain service: the single reusable link (get/rotate/revoke), targeted
/// invites (create + email, list, user-search, revoke), and the invitee token flow (preview,
/// accept, decline). All admin operations are authorized server-side via
/// <see cref="TeamMembershipGuard"/>; the preview is anonymous.
/// </summary>
public interface ITeamInvitationService
{
    Task<InviteLinkResult> GetActiveLinkAsync(string slug, Guid actorUserId, CancellationToken ct = default);

    Task<InviteLinkResult> CreateOrRotateLinkAsync(string slug, Guid actorUserId, CancellationToken ct = default);

    Task<RevokeStatus> RevokeAsync(string slug, Guid actorUserId, Guid invitationId, CancellationToken ct = default);

    Task<TargetedInviteResult> CreateTargetedAsync(string slug, Guid actorUserId, Guid targetUserId, CancellationToken ct = default);

    Task<InviteListResult> ListPendingAsync(string slug, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    Task<UserSearchResult> SearchUsersAsync(string slug, Guid actorUserId, string query, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>The caller's own usable (pending + unexpired) targeted invitations (feature 023 —
    /// the "My team" home). Scoped to the authenticated subject; carries each invite's token so the
    /// UI can accept/decline via the existing token endpoints. Newest-first, paginated.</summary>
    Task<PagedResult<MyInvitationDto>> ListMineAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default);

    /// <summary>Anonymous invite preview (public team info + inviter + usability); null if unknown token.</summary>
    Task<InvitePreviewDto?> GetPreviewAsync(string token, CancellationToken ct = default);

    /// <summary>Accept an invite and join as a member (authenticated).</summary>
    Task<AcceptResult> AcceptAsync(string token, Guid userId, CancellationToken ct = default);

    /// <summary>Decline an invite (authenticated).</summary>
    Task<DeclineStatus> DeclineAsync(string token, Guid userId, CancellationToken ct = default);
}
