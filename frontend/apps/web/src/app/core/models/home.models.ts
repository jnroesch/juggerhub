/**
 * Home dashboard API contracts (feature 008) — mirror of backend Dtos/Home. Read-only;
 * RSVP reuses the event endpoints. Enums arrive as their names.
 */

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  skip: number;
  take: number;
}

export type TeamRole = 'Member' | 'Admin';
export type ParticipantMode = 'Individuals' | 'Teams';
export type SignupStatus = 'Joined' | 'AwaitingApproval' | 'Waitlisted';
export type NewsSource = 'team' | 'event';

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

/**
 * An upcoming agenda item. `viewerSignupId` set ⇒ individuals-mode, viewer is going
 * (toggle to withdraw); all-null individuals item ⇒ RSVP prompt (Open to everyone);
 * `teamGoing` set ⇒ team-mode, read-only "your team is going".
 */
export interface UpNextItem {
  eventId: string;
  title: string;
  typeLabel: string;
  startsAt: string;
  endsAt: string;
  locationLabel: string;
  spotsRemaining: number;
  participationLimit: number;
  mode: ParticipantMode;
  viewerSignupId: string | null;
  viewerStatus: SignupStatus | null;
  teamGoing: TeamGoing | null;
}

export interface TeamActivity {
  teamSlug: string;
  teamName: string;
  summary: string;
  occurredAt: string;
}

export interface HomeNews {
  source: NewsSource;
  sourceName: string;
  sourceSlugOrId: string;
  body: string;
  createdDate: string;
}

export interface TournamentCard {
  eventId: string;
  name: string;
  locationLabel: string;
  startsAt: string;
  spotsRemaining: number;
}

export interface NextFixture {
  eventId: string;
  name: string;
  startsAt: string;
}

export interface TeamSnapshot {
  slug: string;
  name: string;
  nextFixture: NextFixture | null;
}

export interface Home {
  viewer: ViewerSummary;
  teams: MyTeam[];
  upNext: UpNextItem[];
  openToEveryone: UpNextItem[];
  teamsActivity: TeamActivity[];
  news: HomeNews[];
  tournaments: TournamentCard[];
  snapshots: TeamSnapshot[];
}
