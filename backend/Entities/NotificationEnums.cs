namespace JuggerHub.Entities;

/// <summary>
/// The kind of an in-app notification (feature 010). The discriminator drives the icon, the
/// rendering, the navigation target, and whether the row carries inline actions. The set is
/// extensible — new members append without touching existing rows or their payloads. Serialized
/// as its name (global JsonStringEnumConverter), so the API and Angular client speak
/// "TeamInvite"/"TeamNews" rather than opaque integers.
/// </summary>
public enum NotificationType
{
    /// <summary>A targeted invite to join a team. Carries inline Accept/Decline actions.</summary>
    TeamInvite = 0,

    /// <summary>The recipient's role within a team was changed by an admin. Link-only.</summary>
    TeamRoleChanged = 1,

    /// <summary>A new team news post was published to a team the recipient belongs to. Link-only.</summary>
    TeamNews = 2,

    /// <summary>A party participation request was posted (or re-nudged) to the recipient's team (feature 016). Carries inline I'm-in/Can't-make-it actions.</summary>
    PartyRequest = 3,

    /// <summary>A new party news post was published to a party the recipient is in (feature 016). Link-only.</summary>
    PartyNews = 4,

    /// <summary>A party invited the recipient to join it via the event marketplace (feature 017). Carries inline Accept/Decline actions.</summary>
    MarketInvite = 5,
}

/// <summary>
/// A user-facing notification-preferences group (feature 011). One category is one row in the
/// settings matrix and maps one or more <see cref="NotificationType"/> producers to a single set of
/// channel toggles. Only categories with real producers are exposed. Extensible — new members append
/// and require no migration of existing users' (sparse) preference rows. Serialized as its name.
/// </summary>
public enum NotificationCategory
{
    /// <summary>Team invites and role/roster changes (<see cref="NotificationType.TeamInvite"/>, <see cref="NotificationType.TeamRoleChanged"/>).</summary>
    InvitesAndRoster = 0,

    /// <summary>Team news posts (<see cref="NotificationType.TeamNews"/>) and party news (<see cref="NotificationType.PartyNews"/>).</summary>
    TeamNews = 1,
}

/// <summary>The delivery medium a preference governs (feature 011). Push is out of scope. Serialized as its name.</summary>
public enum NotificationChannel
{
    InApp = 0,
    Email = 1,
}

/// <summary>Maps a producer <see cref="NotificationType"/> to its user-facing <see cref="NotificationCategory"/>.</summary>
public static class NotificationCategories
{
    public static NotificationCategory For(NotificationType type) => type switch
    {
        NotificationType.TeamInvite => NotificationCategory.InvitesAndRoster,
        NotificationType.TeamRoleChanged => NotificationCategory.InvitesAndRoster,
        NotificationType.PartyRequest => NotificationCategory.InvitesAndRoster,
        NotificationType.MarketInvite => NotificationCategory.InvitesAndRoster,
        NotificationType.TeamNews => NotificationCategory.TeamNews,
        NotificationType.PartyNews => NotificationCategory.TeamNews,
        _ => NotificationCategory.TeamNews,
    };
}
