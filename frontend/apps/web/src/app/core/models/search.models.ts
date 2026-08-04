/**
 * Browse/search API contracts (mirror of backend Dtos/Search — feature 007). All three
 * browse endpoints are anonymous and return public card fields only; players are opt-in.
 * Enums are serialized as names by the backend.
 */
import { Pompfe } from '../../shared/pompfen.catalog';
import { Location } from './city.models';
import { PagedResult } from './profile.models';

export type { PagedResult };
export type { Location };

export type EventType = 'Tournament' | 'Workshop' | 'Other';
export type LocationKind = 'InPerson' | 'Virtual';

// Feature 030 — 'Proximity' is the opt-in nearest-first sort (requires a home city).
export type TeamSort = 'NameAsc' | 'Proximity';
export type EventSort = 'StartsAtAsc' | 'Proximity';
export type PlayerSort = 'DisplayNameAsc';
// Feature 043 — public-training browse.
export type TrainingSort = 'SessionDateAsc' | 'Proximity';

// --- Cards ----------------------------------------------------------------------

export interface TeamCard {
  slug: string;
  name: string;
  location: Location | null;
  playerCount: number;
  beginnersWelcome: boolean;
  logoInitial: string;
}

export interface EventCard {
  id: string;
  name: string;
  type: EventType;
  customTypeLabel: string | null;
  /** ISO date-time (UTC). */
  startsAt: string;
  /** ISO date-time (UTC). */
  endsAt: string;
  locationKind: LocationKind;
  location: Location | null;
  locationLabel: string;
}

export interface PlayerCard {
  handle: string;
  displayName: string;
  location: Location | null;
  positions: Pompfe[];
  hasAvatar: boolean;
}

// --- Query params (only defined keys are sent) ----------------------------------

export interface PageParams {
  skip?: number;
  take?: number;
}

/**
 * A public training session as a discovery card (feature 043). One card = one dated session.
 *
 * `locationLabel` is composed server-side in the same "City, Country" form the events browse
 * uses, so the two tabs read identically; it is empty for a virtual training, where the client
 * renders the "Online" wording from `locationKind` in the viewer's own language.
 */
export interface TrainingCard {
  sessionId: string;
  trainingId: string;
  name: string;
  teamSlug: string;
  teamName: string;
  isOneOff: boolean;
  /** ISO date (yyyy-MM-dd) — a session is a date, not an instant. */
  sessionDate: string;
  /** Time of day (HH:mm:ss). */
  startTime: string;
  /** Time of day (HH:mm:ss). */
  endTime: string;
  locationKind: LocationKind;
  location: Location | null;
  locationLabel: string;
}

export interface TeamBrowseParams extends PageParams {
  q?: string;
  activeOnly?: boolean;
  beginnersWelcome?: boolean;
  /** Feature 030 — filter to a single country (ISO code or name). */
  country?: string | null;
  sort?: TeamSort;
}

export interface EventBrowseParams extends PageParams {
  q?: string;
  hidePast?: boolean;
  /** ISO date (yyyy-MM-dd). */
  from?: string | null;
  /** ISO date (yyyy-MM-dd). */
  to?: string | null;
  type?: EventType | null;
  /** Feature 030 — filter to a single country (ISO code or name). */
  country?: string | null;
  sort?: EventSort;
}

export interface TrainingBrowseParams extends PageParams {
  q?: string;
  hidePast?: boolean;
  /** ISO date (yyyy-MM-dd). */
  from?: string | null;
  /** ISO date (yyyy-MM-dd). */
  to?: string | null;
  /**
   * Feature 043 — filter to a single canonical city by name. The first city filter in the
   * product: teams and events accept one server-side but have never sent it.
   */
  city?: string | null;
  /** Filter to a single country (ISO code or name). */
  country?: string | null;
  sort?: TrainingSort;
}

export interface PlayerBrowseParams extends PageParams {
  q?: string;
  positions?: Pompfe[];
  /** Feature 030 — filter to a single country (ISO code or name). */
  country?: string | null;
  sort?: PlayerSort;
}

// --- Shared shell view models ---------------------------------------------------

/** A removable active-filter chip shown above the results. */
export interface FilterChip {
  /** Stable key identifying which filter this chip represents. */
  key: string;
  label: string;
}

/** One choice in the browse Sort menu (feature 030 unified the sort control). */
export interface SortOption {
  /** The `sort` value sent to the API, e.g. "NameAsc" | "Proximity". */
  value: string;
  /** Human label shown in the menu and on the button, e.g. "Nearest first". */
  label: string;
}

/** The four list states (+ ready) the shared shell renders. */
export type BrowseState = 'loading' | 'ready' | 'empty' | 'no-results' | 'error';
