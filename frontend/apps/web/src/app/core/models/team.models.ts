/**
 * Team API contracts (mirror of backend Dtos/Teams). Internal reads (roster/news)
 * are members-only; public info (name/type/city/count) and the invite preview are
 * anonymous. Enums are serialized as names by the backend.
 */
import { Location, LocationSelection } from './city.models';
import { Pompfe } from '../../shared/pompfen.catalog';
import { ActivityItem, PagedResult } from './profile.models';
import { EarnedRecognition } from './recognition.models';

export type { Location, LocationSelection };

export type { ActivityItem, PagedResult };

export type TeamType = 'CityTeam' | 'Mixteam';
export type TeamRole = 'Member' | 'Admin';
export type InvitationKind = 'Link' | 'Targeted';
export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked';
export type InviteState = 'Usable' | 'Expired' | 'Invalid';
export type UserRelation = 'Invitable' | 'Invited' | 'Member';

export interface CreateTeamRequest {
  name: string;
  slug: string;
  type: TeamType;
  /** Feature 030 — structured city selection (required for a CityTeam, null for a Mixteam). */
  location: LocationSelection | null;
}

export interface TeamDetail {
  slug: string;
  name: string;
  type: TeamType;
  location: Location | null;
  memberCount: number;
  myRole: TeamRole;
  /** Feature 007 — self-managed recruitment flag surfaced in browse. */
  beginnersWelcome: boolean;
}

export interface TeamPublic {
  slug: string;
  name: string;
  type: TeamType;
  location: Location | null;
  memberCount: number;
}

/** How the viewer relates to a team (feature 009) — drives the sections + join action. */
export type TeamViewerRelation = 'Anonymous' | 'NonMember' | 'Requested' | 'Member' | 'Admin';

/** A public roster row — identity + position only, never contact details. */
export interface PublicMember {
  handle: string;
  displayName: string;
  role: TeamRole;
  hasAvatar: boolean;
  pompfen: Pompfe[];
}

/** The public team page (feature 009). */
export interface TeamPublicDetail {
  id: string;
  slug: string;
  name: string;
  type: TeamType;
  location: Location | null;
  memberCount: number;
  beginnersWelcome: boolean;
  isActive: boolean;
  viewerRelation: TeamViewerRelation;
  roster: PublicMember[];
  recentActivity: ActivityItem[];
  /** Feature 012 — the team's earned badges & achievements. */
  badges: EarnedRecognition[];
  achievements: EarnedRecognition[];
}

/** One pending join request in the admin queue. */
export interface JoinRequest {
  id: string;
  handle: string;
  displayName: string;
  hasAvatar: boolean;
  createdDate: string;
}

export interface TeamMember {
  userId: string;
  handle: string;
  displayName: string;
  role: TeamRole;
  hasAvatar: boolean;
  pompfen: Pompfe[];
}

export interface TeamNews {
  authorDisplayName: string;
  /** Null when the author's profile is gone (banned or deleted) — there's nothing to link to. */
  authorHandle: string | null;
  authorRole: TeamRole;
  /** ISO date-time. */
  createdDate: string;
  body: string;
}

export interface TeamInvitation {
  id: string;
  kind: InvitationKind;
  targetDisplayName: string | null;
  createdDate: string;
  expiresDate: string;
  status: InvitationStatus;
}

export interface InviteLink {
  url: string;
  token: string;
  expiresDate: string;
}

export interface InvitableUser {
  userId: string;
  handle: string;
  displayName: string;
  /** Feature 030 — "City, Country" display label. */
  location: string | null;
  relation: UserRelation;
}

export interface InvitePreview {
  teamName: string;
  teamSlug: string;
  type: TeamType;
  /** Feature 030 — "City, Country" display label (null for a Mixteam). */
  location: string | null;
  memberCount: number;
  inviterDisplayName: string;
  state: InviteState;
}

/** One usable targeted invitation addressed to the caller (feature 023 — the "My team" home).
 * Carries the caller's own token so the UI can accept/decline via the existing token endpoints. */
export interface MyInvitation {
  token: string;
  teamName: string;
  teamSlug: string;
  teamType: TeamType;
  /** Feature 030 — "City, Country" display label (null for a Mixteam). */
  location: string | null;
  memberCount: number;
  inviterDisplayName: string;
  createdDate: string;
  expiresDate: string;
}

export interface AcceptInviteResult {
  teamSlug: string;
}

export interface SlugAvailability {
  slug: string;
  normalized: string;
  available: boolean;
  reason: string | null;
}

// ---- Team-internal "What's happening" (feature 044) -------------------------

/**
 * The kinds the team-internal feed can describe. Deliberately separate from `ActivityKind` in
 * `home.models.ts`: the dashboard switches over its kinds exhaustively and drops unknown ones, so
 * sharing the union would make team-only kinds vanish silently there. Copy the pattern, not the type.
 *
 * Departures and role changes are absent by design — nothing the platform records can reconstruct
 * them. Events the team played belong to the separate "Recent events" card.
 */
export type TeamHappeningKind =
  | 'MemberJoined'
  | 'RecognitionAwarded'
  | 'TrainingSeriesCreated'
  | 'TrainingSessionCancelled';

/**
 * The interpolation values for a happening sentence. Only the fields the entry's `kind` uses are
 * populated. The server sends facts, never prose — it does not know the viewer's language — so the
 * connecting words come from a `teams.detail.happening.*` key chosen client-side.
 */
export interface TeamHappeningParams {
  /**
   * MemberJoined. Null when the player has no display name, is banned, or deleted their account —
   * substitute a *translated* stand-in, never an English placeholder.
   */
  actorName: string | null;
  /** RecognitionAwarded — the badge or achievement name. */
  recognitionName: string | null;
  /** Both training kinds — the training series' name. */
  trainingName: string | null;
  /** TrainingSessionCancelled — which session was called off (ISO date, no time). */
  sessionDate: string | null;
}

/**
 * One thing that happened inside a team (feature 044) — the members-only "What's happening" card.
 * Read-only, carries no actions. At most 10 entries, none older than 30 days.
 */
export interface TeamHappening {
  kind: TeamHappeningKind;
  params: TeamHappeningParams;
  /** Player handle, session id, or null when there is nowhere sensible to navigate. */
  linkTarget: string | null;
  occurredAt: string;
}
