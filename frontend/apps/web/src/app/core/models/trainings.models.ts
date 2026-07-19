import { PagedResult } from './profile.models';

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
  location: string | null;
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
  location: string | null;
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
}

export interface WhosComingPerson {
  handle: string;
  displayName: string;
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
  location: string | null;
  virtualLink: string | null;
  seriesLabel: string | null;
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
}

export interface CreateTrainingRequest {
  isRecurring: boolean;
  name: string;
  description: string | null;
  locationKind: LocationKind;
  location: string | null;
  virtualLink: string | null;
  weekday: string | null;
  interval: TrainingInterval | null;
  startTime: string;
  endTime: string;
  startDate: string;
  endDate: string | null;
  visibility: TrainingVisibility;
}

export interface EditSeriesRequest {
  name?: string;
  description?: string | null;
  locationKind?: LocationKind;
  location?: string | null;
  virtualLink?: string | null;
  weekday?: string;
  interval?: TrainingInterval;
  startTime?: string;
  endTime?: string;
  endDate?: string;
  visibility?: TrainingVisibility;
}

export interface EditSessionRequest {
  sessionDate?: string;
  startTime?: string;
  endTime?: string;
  locationKind?: LocationKind;
  location?: string | null;
  virtualLink?: string | null;
}
