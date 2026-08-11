namespace JuggerHub.Dtos.Teams;

/// <summary>
/// The kind of a team-internal "What's happening" entry (feature 044). Serialized by name.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is deliberately NOT <see cref="JuggerHub.Dtos.Home.ActivityKind"/>, and the two must not
/// be unified.</b> The dashboard's <c>jh-activity-list</c> switches over its kinds exhaustively and
/// ends in <c>default: return ''</c> — "an unrecognized kind (a newer server) yields no sentence".
/// Adding team-only members to that enum would therefore make the dashboard silently discard rows it
/// can never receive, and every future team kind would widen the home params DTO with fields the
/// dashboard never populates. The two feeds also differ semantically: home entries are scoped to the
/// <em>viewer</em> ("a teammate", "you earned"), these are scoped to the <em>team</em> and have no
/// viewer-relative form. Copy the pattern; share no type. (Feature 044 research R1.)
/// </para>
/// <para>
/// Departures, removals, and role changes are absent by design (feature 044 decision D1): a
/// membership row is deleted outright when someone leaves and a role is overwritten in place, so no
/// history survives to derive them from. Events the team played are absent too — they belong to the
/// separate, signed-in-visible "Recent events" card (decision D5).
/// </para>
/// </remarks>
public enum TeamHappeningKind
{
    /// <summary>A player joined the team.</summary>
    MemberJoined,

    /// <summary>A badge or achievement was awarded to the team.</summary>
    RecognitionAwarded,

    /// <summary>A training series was added to the team's schedule.</summary>
    TrainingSeriesCreated,

    /// <summary>A dated session of one of the team's trainings was called off.</summary>
    TrainingSessionCancelled,
}

/// <summary>
/// The interpolation values for a <see cref="TeamHappeningDto"/> sentence. Only the fields the
/// entry's <see cref="TeamHappeningDto.Kind"/> uses are populated; the rest stay null.
///
/// The server never composes the sentence. It does not know the viewer's language — the catalogue is
/// client-side and switchable at runtime (feature 031) — so a rendered summary would be English on a
/// German team page, and no key-parity guard could catch it because there would be no key. Names here
/// are user data and stay untranslated; the connecting prose is a <c>teams.detail.happening.*</c> key
/// the client picks from the kind.
/// </summary>
public sealed record TeamHappeningParamsDto
{
    /// <summary>
    /// <see cref="TeamHappeningKind.MemberJoined"/> — the player who joined. Null when their profile
    /// carries no display name, is suppressed because the account is banned, or was neutralized by
    /// account deletion (feature 037). The client substitutes a <em>translated</em> stand-in; this is
    /// never filled with an English placeholder server-side.
    /// </summary>
    public string? ActorName { get; init; }

    /// <summary><see cref="TeamHappeningKind.RecognitionAwarded"/> — the badge or achievement name.</summary>
    public string? RecognitionName { get; init; }

    /// <summary>Both training kinds — the training series' name.</summary>
    public string? TrainingName { get; init; }

    /// <summary>
    /// <see cref="TeamHappeningKind.TrainingSessionCancelled"/> — <em>which</em> session was called
    /// off. The reader needs the session's date, not just the series' name.
    /// </summary>
    public DateOnly? SessionDate { get; init; }
}

/// <summary>
/// One thing that happened inside a team (feature 044) — the members-only "What's happening" card on
/// the team page. Read-only, carries no actions.
///
/// <para>
/// Every entry is <b>derived on read</b> from rows that already exist; nothing is written when a
/// happening occurs and there is no activity table. That is load-bearing, not incidental: a departed
/// member's join, a revoked award, and a banned player's name all correct themselves for free,
/// because the entry is recomputed from live state rather than replayed from a snapshot.
/// </para>
///
/// <see cref="Kind"/> + <see cref="Params"/> describe <em>what happened</em>; the client turns that
/// into a localized sentence. See <see cref="TeamHappeningParamsDto"/> for why the sentence is not
/// built here.
/// </summary>
/// <param name="Kind">Which happening this is.</param>
/// <param name="Params">The untranslated values the client interpolates into the sentence.</param>
/// <param name="LinkTarget">
/// Kind-dependent navigation key — a player handle, a session id, or null when there is nowhere
/// sensible to go. The client maps it to a route; an entry with no target renders as plain text.
/// </param>
/// <param name="OccurredAt">
/// When the happening occurred (UTC). This is the <em>domain</em> moment and differs per kind
/// (joined / earned / created / cancelled) — it is not the row's insertion timestamp.
/// </param>
public sealed record TeamHappeningDto(
    TeamHappeningKind Kind,
    TeamHappeningParamsDto Params,
    string? LinkTarget,
    DateTime OccurredAt);
