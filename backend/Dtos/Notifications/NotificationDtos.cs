using System.Text.Json;
using JuggerHub.Entities;

namespace JuggerHub.Dtos.Notifications;

// --- Client-facing DTOs -----------------------------------------------------

/// <summary>
/// A single notification as the client sees it (feature 010). <see cref="Payload"/> is the raw,
/// type-specific JSON (camelCase) written by the producer; the Angular client narrows it by
/// <see cref="Type"/>. <see cref="Resolved"/> applies only to <see cref="NotificationType.TeamInvite"/>
/// — true when the underlying invite is no longer usable, so the inline actions hide.
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
