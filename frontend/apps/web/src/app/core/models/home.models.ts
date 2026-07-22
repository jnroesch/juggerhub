/**
 * Home dashboard API contracts (feature 008, reshaped by feature 025) — mirror of backend Dtos/Home.
 * Read-only; every action reuses the existing per-domain endpoints. Enums arrive as their names.
 * The composite reads top-to-bottom: Needs you → Up next → News → What's going on.
 */

import { TrainingRsvp } from './trainings.models';

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  skip: number;
  take: number;
}

export type TeamRole = 'Member' | 'Admin';
export type ParticipantMode = 'Individuals' | 'Teams';
export type SignupStatus = 'Joined' | 'AwaitingApproval' | 'Waitlisted';
export type NewsSource = 'team' | 'event' | 'party';

export interface ViewerSummary {
  displayName: string;
  handle: string;
  hasAvatar: boolean;
}

export interface MyTeam {
  slug: string;
  name: string;
  role: TeamRole;
}

export interface TeamGoing {
  slug: string;
  name: string;
}

// ---- Needs you (actionable) ------------------------------------------------

export type NeedsYouKind =
  | 'TeamInvite'
  | 'PartyRequest'
  | 'PartyCoAdminInvite'
  | 'MarketInvite'
  | 'MarketApplication';

/**
 * One item awaiting the viewer's response, aggregated from its authoritative source domain.
 * `id` is the action key passed to the kind's resolving endpoint (invitation token, request id, or
 * session id); `linkTarget` is the optional navigation target.
 */
export interface NeedsYouItem {
  kind: NeedsYouKind;
  id: string;
  title: string;
  context: string | null;
  linkTarget: string | null;
  occurredAt: string;
}

// ---- Up next (unified agenda) ----------------------------------------------

export type AgendaKind = 'Event' | 'Training';

/**
 * A unified agenda item. `kind` selects which optional block applies. Event items: individuals-mode
 * the viewer joined carry `viewerSignupId`+`viewerStatus` (toggle to withdraw); an all-null
 * individuals item is an RSVP prompt; `teamGoing` set ⇒ team-mode, read-only. Training items carry
 * `startTime`/`myAnswer`. Near-window un-answered trainings live in Needs you, not here.
 */
export interface AgendaItem {
  kind: AgendaKind;
  id: string;
  title: string;
  startsAt: string;
  endsAt: string | null;
  locationLabel: string;

  // Event-only
  typeLabel: string | null;
  spotsRemaining: number | null;
  participationLimit: number | null;
  mode: ParticipantMode | null;
  viewerSignupId: string | null;
  viewerStatus: SignupStatus | null;
  teamGoing: TeamGoing | null;

  // Training-only
  trainingName: string | null;
  startTime: string | null;
  isPublicGuest: boolean | null;
  myAnswer: TrainingRsvp | null;
}

// ---- News (authored broadcast) ---------------------------------------------

export interface HomeNews {
  source: NewsSource;
  sourceName: string;
  sourceSlugOrId: string;
  body: string;
  createdDate: string;
}

// ---- What's going on (passive activity) ------------------------------------

export type ActivityKind =
  | 'TeammateJoinedEvent'
  | 'NewTeamMember'
  | 'BadgeAwarded'
  | 'PartyMemberJoined'
  | 'RoleChanged'
  | 'TrainingChanged';

/** A passive, read-only "What's going on" entry. No actions. */
export interface ActivityEntry {
  kind: ActivityKind;
  summary: string;
  linkTarget: string | null;
  occurredAt: string;
}

// ---- Composite -------------------------------------------------------------

export interface Home {
  viewer: ViewerSummary;
  teams: MyTeam[];
  needsYou: NeedsYouItem[];
  upNext: AgendaItem[];
  openToEveryone: AgendaItem[];
  news: HomeNews[];
  activity: ActivityEntry[];
}
