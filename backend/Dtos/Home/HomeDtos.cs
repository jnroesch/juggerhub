using JuggerHub.Entities;

namespace JuggerHub.Dtos.Home;

/// <summary>
/// The composite Home dashboard for the signed-in player (feature 008). Viewer summary +
/// a capped top-N per module for a fast first paint. New-player viewers (no team) get
/// empty team-scoped lists and a populated <see cref="OpenToEveryone"/>; the client
/// derives the variant from <c>Teams.Count &gt; 0</c>. Read-only — RSVP reuses the
/// existing event sign-up endpoints.
/// </summary>
public sealed record HomeDto(
    ViewerSummaryDto Viewer,
    IReadOnlyList<MyTeamDto> Teams,
    IReadOnlyList<UpNextItemDto> UpNext,
    IReadOnlyList<UpNextItemDto> OpenToEveryone,
    IReadOnlyList<TeamActivityDto> TeamsActivity,
    IReadOnlyList<HomeNewsDto> News,
    IReadOnlyList<TournamentCardDto> Tournaments,
    IReadOnlyList<TeamSnapshotDto> Snapshots);

/// <summary>The signed-in player: greeting name + whether an avatar exists.</summary>
public sealed record ViewerSummaryDto(string DisplayName, string Handle, bool HasAvatar);

/// <summary>One of the caller's team memberships. Also the payload of GET /profiles/me/teams.</summary>
public sealed record MyTeamDto(string Slug, string Name, TeamRole Role);

/// <summary>
/// An upcoming event on the player's agenda. Individuals-mode items the viewer is signed up
/// to carry <see cref="ViewerSignupId"/> + <see cref="ViewerStatus"/> (toggle to withdraw);
/// open individuals-mode items (Open to everyone) carry neither (RSVP button); team-mode
/// items carry <see cref="TeamGoing"/> and are read-only ("your team is going").
/// </summary>
public sealed record UpNextItemDto(
    Guid EventId,
    string Title,
    string TypeLabel,
    DateTime StartsAt,
    DateTime EndsAt,
    string LocationLabel,
    int SpotsRemaining,
    int ParticipationLimit,
    ParticipantMode Mode,
    Guid? ViewerSignupId,
    SignupStatus? ViewerStatus,
    TeamGoingDto? TeamGoing);

/// <summary>Which of the viewer's teams entered a team-mode event.</summary>
public sealed record TeamGoingDto(string Slug, string Name);

/// <summary>Recent activity in one of the viewer's teams (sourced from team news today).</summary>
public sealed record TeamActivityDto(string TeamSlug, string TeamName, string Summary, DateTime OccurredAt);

/// <summary>A news item the viewer is connected to, tagged by source.</summary>
public sealed record HomeNewsDto(
    string Source,          // "team" | "event" (a future "league" adds a value; contract unchanged)
    string SourceName,
    string SourceSlugOrId,  // team slug or event id → link target
    string Body,
    DateTime CreatedDate);

/// <summary>A promoted upcoming tournament.</summary>
public sealed record TournamentCardDto(
    Guid EventId, string Name, string LocationLabel, DateTime StartsAt, int SpotsRemaining);

/// <summary>Desktop right-rail snapshot for one team (name + next fixture; no win/loss record).</summary>
public sealed record TeamSnapshotDto(string Slug, string Name, NextFixtureDto? NextFixture);

/// <summary>The soonest upcoming event a team is signed up to.</summary>
public sealed record NextFixtureDto(Guid EventId, string Name, DateTime StartsAt);
