using JuggerHub.Entities;

namespace JuggerHub.Dtos.Home;

/// <summary>
/// The composite Home dashboard for the signed-in player (feature 008, reshaped by feature 025
/// around participation + action). Viewer summary + a capped top-N per section for a fast first
/// paint. The four sections read top-to-bottom as a priority order: <see cref="NeedsYou"/>
/// (actionable, hidden when empty) → <see cref="UpNext"/> (unified events + trainings agenda) →
/// <see cref="News"/> (authored team/event/party posts) → <see cref="Activity"/> (quiet passive
/// "what's going on"). New-player viewers (no team) get empty team-scoped sections and a populated
/// <see cref="OpenToEveryone"/>; the client derives the variant from <c>Teams.Count &gt; 0</c>.
/// Read-only — every action reuses the existing per-domain endpoints.
/// </summary>
public sealed record HomeDto(
    ViewerSummaryDto Viewer,
    IReadOnlyList<MyTeamDto> Teams,
    IReadOnlyList<NeedsYouItemDto> NeedsYou,
    IReadOnlyList<AgendaItemDto> UpNext,
    IReadOnlyList<AgendaItemDto> OpenToEveryone,
    IReadOnlyList<HomeNewsDto> News,
    IReadOnlyList<ActivityEntryDto> Activity);

/// <summary>The signed-in player: greeting name + whether an avatar exists.</summary>
public sealed record ViewerSummaryDto(string DisplayName, string Handle, bool HasAvatar);

/// <summary>One of the caller's team memberships. Also the payload of GET /profiles/me/teams.</summary>
public sealed record MyTeamDto(string Slug, string Name, TeamRole Role);

// ---- Needs you (actionable) ------------------------------------------------

/// <summary>The kind of an actionable "Needs you" item — drives the icon, copy, and which existing
/// per-domain endpoint the client calls to resolve it. Serialized by name.</summary>
public enum NeedsYouKind
{
    /// <summary>A pending targeted team invite. Resolve via the team invitation token endpoints.</summary>
    TeamInvite,

    /// <summary>A party participation request to the viewer's team (feature 016). Resolve via the party request endpoints.</summary>
    PartyRequest,

    /// <summary>A pending party co-admin invite (feature 016). Resolve via the party invitation token endpoints.</summary>
    PartyCoAdminInvite,

    /// <summary>A marketplace invite the viewer can accept/decline (feature 017).</summary>
    MarketInvite,

    /// <summary>A marketplace application the viewer sent — shown pending (withdraw only) (feature 017).</summary>
    MarketApplication,
}

/// <summary>
/// One invite or request awaiting the viewer's response, aggregated from its authoritative source
/// domain (never the notification display-cache). Trainings are deliberately excluded — RSVP lives in
/// "Up next". <see cref="Id"/> is the action key the client passes to the kind's resolving endpoint
/// (invitation token or request id); <see cref="LinkTarget"/> is the optional navigation target.
/// </summary>
public sealed record NeedsYouItemDto(
    NeedsYouKind Kind,
    string Id,
    string Title,
    string? Context,
    string? LinkTarget,
    DateTime OccurredAt);

// ---- Up next (unified agenda) ----------------------------------------------

/// <summary>Whether a unified agenda item is an event or a training session. Serialized by name.</summary>
public enum AgendaKind
{
    Event,
    Training,
}

/// <summary>
/// One item in the unified "Up next" participation agenda (feature 025). <see cref="Kind"/> selects
/// which optional block is populated. Events carry the mode/RSVP fields (individuals-mode items the
/// viewer joined carry <see cref="ViewerSignupId"/> + <see cref="ViewerStatus"/> and toggle to
/// withdraw; team-mode items carry <see cref="TeamGoing"/> and are read-only). Trainings carry the
/// session's time and the viewer's answer. Near-window un-answered trainings live in
/// <see cref="NeedsYouItemDto"/> instead and are excluded here (FR-006b).
/// </summary>
public sealed record AgendaItemDto(
    AgendaKind Kind,
    Guid Id,
    string Title,
    DateTime StartsAt,
    DateTime? EndsAt,
    string LocationLabel,

    // Event-only (Kind == Event)
    string? TypeLabel,
    int? SpotsRemaining,
    int? ParticipationLimit,
    ParticipantMode? Mode,
    Guid? ViewerSignupId,
    SignupStatus? ViewerStatus,
    TeamGoingDto? TeamGoing,

    // Training-only (Kind == Training)
    string? TrainingName,
    string? StartTime,
    bool? IsPublicGuest,
    TrainingRsvp? MyAnswer);

/// <summary>Which of the viewer's teams entered a team-mode event.</summary>
public sealed record TeamGoingDto(string Slug, string Name);

// ---- News (authored broadcast) ---------------------------------------------

/// <summary>A news item the viewer is connected to, tagged by source.</summary>
public sealed record HomeNewsDto(
    string Source,          // "team" | "event" | "party"
    string SourceName,
    string SourceSlugOrId,  // team slug, event id, or event id for a party post → link target
    string Body,
    DateTime CreatedDate);

// ---- What's going on (passive activity) ------------------------------------

/// <summary>The kind of a passive "What's going on" activity entry (feature 025). Serialized by name.</summary>
public enum ActivityKind
{
    /// <summary>A teammate signed up for an event.</summary>
    TeammateJoinedEvent,

    /// <summary>A new member joined one of the viewer's teams.</summary>
    NewTeamMember,

    /// <summary>A badge was awarded to the viewer or a teammate.</summary>
    BadgeAwarded,

    /// <summary>A member joined one of the viewer's parties.</summary>
    PartyMemberJoined,

    /// <summary>The viewer's role in a team changed.</summary>
    RoleChanged,

    /// <summary>A training the viewer responded to was rescheduled or cancelled.</summary>
    TrainingChanged,
}

/// <summary>Whether a changed training session was called off or merely edited. Serialized by name.</summary>
public enum TrainingChangeKind
{
    Updated,
    Cancelled,
}

/// <summary>
/// The interpolation values for an <see cref="ActivityEntryDto"/> sentence. Only the fields the
/// entry's <see cref="ActivityEntryDto.Kind"/> uses are populated; the rest stay null.
///
/// This DTO exists so the server never composes the sentence. The server does not know the viewer's
/// language — the catalogue is client-side and switchable at runtime (feature 031) — so a rendered
/// summary would be English on a German dashboard, and no key-parity guard could ever catch it
/// because there would be no key. Names here are user data and stay untranslated; the connecting
/// prose is a <c>home.activity.*</c> key the client picks from <see cref="ActivityEntryDto.Kind"/>
/// plus the discriminators below.
/// </summary>
public sealed record ActivityParamsDto
{
    /// <summary>The player the entry is about. Null when their profile carries no display name —
    /// the client substitutes a translated stand-in rather than an English one.</summary>
    public string? ActorName { get; init; }

    /// <summary>TeammateJoinedEvent / PartyMemberJoined.</summary>
    public string? EventName { get; init; }

    /// <summary>NewTeamMember / RoleChanged. Null on RoleChanged when the notification payload
    /// carried no team name.</summary>
    public string? TeamName { get; init; }

    /// <summary>TrainingChanged. Null when the payload carried no training name.</summary>
    public string? TrainingName { get; init; }

    /// <summary>BadgeAwarded.</summary>
    public string? BadgeName { get; init; }

    /// <summary>BadgeAwarded — the award is the viewer's own. Selects the second-person sentence
    /// ("You earned…"), which is a different verb form in German/Spanish, not a swapped noun.</summary>
    public bool IsMine { get; init; }

    /// <summary>RoleChanged — the viewer's new role. Null when the payload role was unrecognized.</summary>
    public TeamRole? NewRole { get; init; }

    /// <summary>TrainingChanged — cancelled vs. edited.</summary>
    public TrainingChangeKind? ChangeKind { get; init; }
}

/// <summary>
/// A passive, read-only "What's going on" entry (feature 025). Carries no actions. Derived on read
/// from domain rows (event sign-ups, team memberships, badge awards, party members) plus the viewer's
/// own passive notification rows for pure state-changes (role change, training reschedule/cancel).
/// All entries are scoped to the viewer or the viewer's teams/parties server-side.
///
/// <see cref="Kind"/> + <see cref="Params"/> describe *what happened*; the client turns that into a
/// localized sentence. See <see cref="ActivityParamsDto"/> for why the sentence is not built here.
/// </summary>
public sealed record ActivityEntryDto(
    ActivityKind Kind,
    ActivityParamsDto Params,
    string? LinkTarget,
    DateTime OccurredAt);
