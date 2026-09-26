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

    /// <summary>A team scheduled a new training series or one-off the recipient belongs to (feature 018). Link-only.</summary>
    TrainingScheduled = 6,

    /// <summary>An upcoming training session the recipient responded to was edited (series) or cancelled (feature 018). Link-only.</summary>
    TrainingUpdated = 7,

    /// <summary>An event the recipient signed up for was cancelled by its organiser (feature 039). Link-only.</summary>
    EventCancelled = 8,
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

    /// <summary>Training heads-up and change notices (<see cref="NotificationType.TrainingScheduled"/>, <see cref="NotificationType.TrainingUpdated"/>) — feature 018.</summary>
    Trainings = 2,

    /// <summary>
    /// Changes to events the recipient signed up for (<see cref="NotificationType.EventCancelled"/>)
    /// — feature 039. Named for the domain rather than the single current producer so later event
    /// notices can join it without a rename.
    /// </summary>
    Events = 3,

    /// <summary>
    /// New chat messages (feature 056 / GH #309). <b>Push only</b>, and unlike the four above it
    /// has no <see cref="NotificationType"/> mapped to it and never will.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chat writes no notification rows and raises nothing in the Alerts inbox — feature 019's
    /// FR-051, which this deliberately leaves standing. Chat reaches a device through
    /// <c>IPushDispatcher</c> directly, below the notification store, so this category governs
    /// delivery without a producer type existing anywhere.
    /// </para>
    /// <para>
    /// It exists so a member can say "not chat, on my phone" in the same place they say it about
    /// everything else. That required amending the last clause of 019's FR-051a ("no new
    /// notification-preference category"); the clauses that matter — no notification type, no
    /// Alerts row — are untouched. See <c>specs/056-chat-push/research.md</c> R11.
    /// </para>
    /// <para>
    /// <b>Appended, never inserted.</b> These values are stored as integers in
    /// <c>NotificationPreferences</c>, so renumbering an existing member would silently re-point
    /// every preference row a member has ever set.
    /// </para>
    /// </remarks>
    Chat = 4,
}

/// <summary>
/// The delivery medium a preference governs (feature 011). Serialized as its name.
/// <para>
/// The three channels are INDEPENDENT. Turning one off for a category must never change or
/// suppress delivery on another, in either direction — see the restructure in
/// <c>NotificationService.CreateAsync</c>, where the in-app check used to be an early return that
/// would have silently swallowed push.
/// </para>
/// </summary>
public enum NotificationChannel
{
    InApp = 0,
    Email = 1,

    /// <summary>
    /// Web push to the member's enabled devices (feature 055). Added here with no migration: the
    /// preference table is sparse, so a third channel adds possible cells without touching a single
    /// stored row.
    /// </summary>
    Push = 2,
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
        NotificationType.TrainingScheduled => NotificationCategory.Trainings,
        NotificationType.TrainingUpdated => NotificationCategory.Trainings,
        NotificationType.EventCancelled => NotificationCategory.Events,
        // NOTE: this default arm makes a missing case silent — an unmapped type is filed under the
        // recipient's *Team news* preference and compiles without complaint. Every new
        // NotificationType needs a case above, and a test asserting it (feature 039).
        _ => NotificationCategory.TeamNews,
    };

    /// <summary>
    /// Whether a category can be delivered on a channel at all (feature 056).
    /// <see cref="NotificationCategory.Chat"/> is the only one that is not deliverable everywhere:
    /// it is Push-only, because chat has no Alerts row by design (feature 019, FR-051) and no email
    /// producer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One home for the rule, three readers: <c>NotificationPreferenceService.GetMatrixAsync</c>
    /// tells the client which cells exist, <c>NotificationPreferencesController.Set</c> refuses to
    /// store a cell that does not, and the category's own description copy explains the empty ones
    /// to the member looking at them.
    /// </para>
    /// <para>
    /// The refusal is not pedantry. A stored <c>(Chat, Email)</c> row would mean nothing, and
    /// something reading it back later could not tell that from a member's considered choice —
    /// which is the never-trust-the-client rule (constitution I) applied to a cell the interface
    /// never draws.
    /// </para>
    /// <para>
    /// Written permissively — everything is supported unless stated — so that adding a category
    /// defaults to the visible behaviour rather than to a toggle that silently goes missing.
    /// </para>
    /// </remarks>
    public static bool Supports(NotificationCategory category, NotificationChannel channel) =>
        category != NotificationCategory.Chat || channel == NotificationChannel.Push;

    /// <summary>The channels a category can be delivered on, in display order.</summary>
    public static IReadOnlyList<NotificationChannel> ChannelsFor(NotificationCategory category) =>
        Enum.GetValues<NotificationChannel>().Where(c => Supports(category, c)).ToList();
}
