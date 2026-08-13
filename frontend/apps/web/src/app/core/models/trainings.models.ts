import { PagedResult } from './profile.models';
import { Location, LocationSelection } from './city.models';

export type { PagedResult };

/** Where a training happens (name-serialized on the wire). */
export type LocationKind = 'InPerson' | 'Virtual';

/** How a recurring series repeats. */
export type TrainingInterval = 'Weekly' | 'BiWeekly' | 'Monthly';

/** Who can see and RSVP a training or session. */
export type TrainingVisibility = 'TeamOnly' | 'Public';

/** A single session's lifecycle. */
export type TrainingSessionStatus = 'Scheduled' | 'Cancelled' | 'Skipped';

/** A three-way RSVP answer. */
export type TrainingRsvp = 'Going' | 'Maybe' | 'Cant';

/** A session row in the Trainings tab / public list. `startTime`/`endTime` are "HH:mm:ss"; dates "yyyy-MM-dd". */
export interface TrainingSessionRow {
  sessionId: string;
  trainingId: string;
  name: string;
  isOneOff: boolean;
  sessionDate: string;
  startTime: string;
  endTime: string;
  locationKind: LocationKind;
  /** Server-composed city → venue → legacy label (feature 042); empty for a virtual session. */
  locationLabel: string;
  virtualLink: string | null;
  visibility: TrainingVisibility;
  status: TrainingSessionStatus;
  goingCount: number;
  maybeCount: number;
  cantCount: number;
  myAnswer: TrainingRsvp | null;
  detached: boolean;
}

/** A dashboard agenda row: a session plus its team. */
export interface AgendaSession {
  sessionId: string;
  trainingId: string;
  name: string;
  isOneOff: boolean;
  sessionDate: string;
  startTime: string;
  endTime: string;
  locationKind: LocationKind;
  /** Server-composed city → venue → legacy label (feature 042); empty for a virtual session. */
  locationLabel: string;
  virtualLink: string | null;
  visibility: TrainingVisibility;
  status: TrainingSessionStatus;
  goingCount: number;
  myAnswer: TrainingRsvp | null;
  teamSlug: string;
  teamName: string;
  isPublicGuest: boolean;
}

export interface TrainingSeriesSummary {
  trainingId: string;
  name: string;
  weekday: string | null;
  interval: TrainingInterval | null;
  startTime: string;
  endTime: string;
  endDate: string | null;
  visibility: TrainingVisibility;
  upcomingCount: number;
  nextSessionDate: string | null;
  /** The next upcoming session — the entry point for editing the whole series; null when none remain. */
  nextSessionId: string | null;
}

export interface WhosComingPerson {
  handle: string;
  displayName: string;
  hasAvatar: boolean;
  position: string | null;
  isGuest: boolean;
  isYou: boolean;
}

export interface WhosComingGroup {
  count: number;
  people: WhosComingPerson[];
}

export interface WhosComing {
  going: WhosComingGroup;
  maybe: WhosComingGroup;
  cant: WhosComingGroup;
}

export interface TrainingSessionDetail {
  sessionId: string;
  trainingId: string;
  teamSlug: string;
  teamName: string;
  name: string;
  description: string | null;
  isOneOff: boolean;
  sessionDate: string;
  startTime: string;
  endTime: string;
  locationKind: LocationKind;
  /** Structured address (feature 042). All null for a virtual training. */
  venueName: string | null;
  street: string | null;
  postalCode: string | null;
  /** The resolved city; what `jh-city-picker` reads back as its current selection on an edit. */
  location: Location | null;
  /** Server-composed city → venue → legacy label; empty for a virtual session. */
  locationLabel: string;
  virtualLink: string | null;
  weekday: string | null;
  interval: TrainingInterval | null;
  endDate: string | null;
  visibility: TrainingVisibility;
  status: TrainingSessionStatus;
  isPast: boolean;
  isDetached: boolean;
  viewerIsAdmin: boolean;
  viewerIsGuest: boolean;
  myAnswer: TrainingRsvp | null;
  whosComing: WhosComing;
}

export interface AttendanceEntry {
  userId: string;
  handle: string;
  displayName: string;
  hasAvatar: boolean;
  position: string | null;
  isGuest: boolean;
  isYou: boolean;
  isTeamAdmin: boolean;
  answer: TrainingRsvp;
}

export interface CreatedTraining {
  trainingId: string;
  sessionCount: number;
  firstSessionId: string;
}

export interface SeriesEditResult {
  trainingId: string;
  addedSessions: number;
  removedSessions: number;
  keptSessions: number;
  /**
   * The earliest surviving upcoming session — where to go after saving. A pattern change deletes the
   * session the edit form was opened from, so the entry-point id is not navigable (GH #181).
   */
  nextSessionId: string | null;
}

export interface CreateTrainingRequest {
  isRecurring: boolean;
  name: string;
  description: string | null;
  locationKind: LocationKind;
  /** In-person only; optional. */
  venueName: string | null;
  /** In-person only; required with `postalCode` and `location`. */
  street: string | null;
  /** In-person only; required with `street` and `location`. */
  postalCode: string | null;
  /** The picked city (feature 042). In-person only — the server re-resolves the external id. */
  location: LocationSelection | null;
  virtualLink: string | null;
  weekday: string | null;
  interval: TrainingInterval | null;
  startTime: string;
  endTime: string;
  startDate: string;
  endDate: string | null;
  visibility: TrainingVisibility;
}

/**
 * The address is replaced as a BLOCK: send `location` together with `venueName`/`street`/
 * `postalCode`, or omit all four to leave the address untouched. Never patch a single field —
 * that would allow a street from one address against a city from another (feature 042 FR-007).
 */
export interface EditSeriesRequest {
  name?: string;
  description?: string | null;
  locationKind?: LocationKind;
  venueName?: string | null;
  street?: string | null;
  postalCode?: string | null;
  location?: LocationSelection | null;
  virtualLink?: string | null;
  weekday?: string;
  interval?: TrainingInterval;
  startTime?: string;
  endTime?: string;
  endDate?: string;
  visibility?: TrainingVisibility;
}

/** Same block rule as {@link EditSeriesRequest}; supplying it relocates this session only. */
export interface EditSessionRequest {
  sessionDate?: string;
  startTime?: string;
  endTime?: string;
  locationKind?: LocationKind;
  venueName?: string | null;
  street?: string | null;
  postalCode?: string | null;
  location?: LocationSelection | null;
  virtualLink?: string | null;
}
