using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Notifications;

// --- Client-facing DTOs -----------------------------------------------------

/// <summary>
/// A single notification as the client sees it (feature 010). <see cref="Payload"/> is the raw,
/// type-specific JSON (camelCase) written by the producer; the Angular client narrows it by
/// <see cref="Type"/>. <see cref="Resolved"/> applies to two types and is worked out when the
/// inbox is read, never stored: for <see cref="NotificationType.TeamInvite"/> it is true when the
/// invite is no longer usable, so the inline actions hide; for
/// <see cref="NotificationType.TeamJoinRequest"/> (feature 058) it is true when the request no
/// longer waits for an answer, so the row stops asking for one.
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    DateTime CreatedDate,
    bool IsRead,
    string? ActorDisplayName,
    bool Resolved,
    JsonElement Payload);

/// <summary>The signed-in user's current unread count (the bell badge). Display capping is a UI concern.</summary>
public sealed record UnreadCountDto(int Count);

// --- Producer payloads (serialized into Notification.Payload as camelCase jsonb) ------

/// <summary>Payload for <see cref="NotificationType.TeamInvite"/>. Inline actions act on <see cref="Token"/>.</summary>
public sealed record TeamInvitePayload(
    Guid InvitationId,
    string Token,
    string TeamSlug,
    string TeamName,
    string InviterName);

/// <summary>Payload for <see cref="NotificationType.TeamRoleChanged"/>.</summary>
public sealed record TeamRoleChangedPayload(
    string TeamSlug,
    string TeamName,
    TeamRole NewRole);

/// <summary>Payload for <see cref="NotificationType.TeamNews"/>. <see cref="Excerpt"/> is a short body preview.</summary>
public sealed record TeamNewsPayload(
    string TeamSlug,
    string TeamName,
    Guid NewsPostId,
    string Excerpt);

/// <summary>
/// Payload for <see cref="NotificationType.TeamJoinRequest"/> (feature 058): which request, and
/// which team. <see cref="RequestId"/> is what the inbox reads to decide whether the request still
/// waits (<see cref="NotificationDto.Resolved"/>).
/// </summary>
/// <remarks>
/// <b>Never add the player's name or handle here.</b> The admin's row outlives the player's
/// account, and feature 037 (FR-023) forbids a surviving record that identifies an erased member.
/// The player is the notification's actor, so <see cref="NotificationDto.ActorDisplayName"/> names
/// them from their current profile, and names no one once they are banned or gone.
/// </remarks>
public sealed record TeamJoinRequestPayload(
    Guid RequestId,
    string TeamSlug,
    string TeamName);

/// <summary>
/// Payload for <see cref="NotificationType.TeamJoinRequestAnswered"/> (feature 058): which team, and
/// whether it accepted the recipient.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never add the admin who answered</b> — the team answers, not a person (spec FR-011).
/// </para>
/// <para>
/// <b><see cref="Accepted"/> is a bool on purpose, not <c>JoinRequestStatus</c>.</b> Payloads are
/// serialized with the notification engine's own options, which carry no string-enum converter
/// (the global one is registered for MVC only), so an enum here would be stored as a number and
/// read back by the client as one. <c>TeamRoleChangedPayload.NewRole</c> is stored that way today.
/// </para>
/// </remarks>
public sealed record TeamJoinRequestAnsweredPayload(
    string TeamSlug,
    string TeamName,
    bool Accepted);
