using JuggerHub.Common;
using JuggerHub.Dtos.Events;

namespace JuggerHub.Services.Events;

/// <summary>Result of an admin gate (does the event exist + is the caller an admin?).</summary>
public enum EventAdminGate
{
    Ok,
    NotFound,
    Forbidden,
}

/// <summary>The active co-admin invite link (or none), behind an admin gate.</summary>
public sealed record EventInviteLinkResult(EventAdminGate Gate, EventInviteLinkDto? Link);

/// <summary>Outcome of creating a targeted co-admin invite.</summary>
public enum TargetedInviteOutcome
{
    Created,
    NotFound,
    Forbidden,
    TargetNotFound,
    AlreadyAdmin,
    AlreadyInvited,
}

/// <summary>Result of a targeted co-admin invite.</summary>
public sealed record EventTargetedInviteResult(TargetedInviteOutcome Outcome, EventInvitationDto? Invitation);

/// <summary>Pending co-admin invitations behind an admin gate.</summary>
public sealed record EventInviteListResult(EventAdminGate Gate, PagedResult<EventInvitationDto>? Page);

/// <summary>User-search candidates behind an admin gate.</summary>
public sealed record EventUserSearchResult(EventAdminGate Gate, PagedResult<EventInvitableUserDto>? Page);

/// <summary>Outcome of revoking a pending invitation.</summary>
public enum RevokeOutcome
{
    Revoked,
    NotFound,
    Forbidden,
    InviteNotFound,
}

/// <summary>Outcome of accepting a co-admin invite.</summary>
public enum AcceptOutcome
{
    Granted,
    AlreadyAdmin,
    NotUsable,
    NotFound,
}

/// <summary>Result of accepting (carries the event to land on).</summary>
public sealed record EventAcceptResult(AcceptOutcome Outcome, Guid? EventId);

/// <summary>Outcome of declining a co-admin invite.</summary>
public enum DeclineOutcome
{
    Declined,
    NotFound,
}

/// <summary>
/// Co-admin invitations for an event: a shared link (rotate/revoke), targeted emailed invites,
/// the pending list + user search (all admin-gated), and the anonymous preview + authed
/// accept/decline. Mirrors the team invitation slice; accepting grants an <c>EventAdmin</c>.
/// </summary>
public interface IEventInvitationService
{
    Task<EventInviteLinkResult> GetActiveLinkAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default);

    Task<EventInviteLinkResult> CreateOrRotateLinkAsync(Guid eventId, Guid actorUserId, CancellationToken ct = default);

    Task<RevokeOutcome> RevokeAsync(Guid eventId, Guid actorUserId, Guid invitationId, CancellationToken ct = default);

    Task<EventTargetedInviteResult> CreateTargetedAsync(Guid eventId, Guid actorUserId, Guid targetUserId, CancellationToken ct = default);

    Task<EventInviteListResult> ListPendingAsync(Guid eventId, Guid actorUserId, PaginationRequest pagination, CancellationToken ct = default);

    Task<EventUserSearchResult> SearchUsersAsync(Guid eventId, Guid actorUserId, string query, PaginationRequest pagination, CancellationToken ct = default);

    Task<EventInvitePreviewDto?> GetPreviewAsync(string token, CancellationToken ct = default);

    Task<EventAcceptResult> AcceptAsync(string token, Guid userId, CancellationToken ct = default);

    Task<DeclineOutcome> DeclineAsync(string token, Guid userId, CancellationToken ct = default);
}
