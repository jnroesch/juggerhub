/**
 * Browse/search API contracts (mirror of backend Dtos/Search — feature 007). All three
 * browse endpoints are anonymous and return public card fields only; players are opt-in.
 * Enums are serialized as names by the backend.
 */
import { Pompfe } from '../../shared/pompfen.catalog';
import { PagedResult } from './profile.models';

export type { PagedResult };

export type EventType = 'Tournament' | 'Workshop' | 'Other';
export type LocationKind = 'InPerson' | 'Virtual';

export type TeamSort = 'NameAsc';
export type EventSort = 'StartsAtAsc';
export type PlayerSort = 'DisplayNameAsc';

// --- Cards ----------------------------------------------------------------------

export interface TeamCard {
  slug: string;
  name: string;
  city: string | null;
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
  city: string | null;
  locationLabel: string;
}

export interface PlayerCard {
  handle: string;
  displayName: string;
  hometown: string | null;
  positions: Pompfe[];
  hasAvatar: boolean;
}

// --- Query params (only defined keys are sent) ----------------------------------

export interface PageParams {
  skip?: number;
  take?: number;
}

export interface TeamBrowseParams extends PageParams {
  q?: string;
  activeOnly?: boolean;
  beginnersWelcome?: boolean;
  city?: string | null;
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
  city?: string | null;
  sort?: EventSort;
}

export interface PlayerBrowseParams extends PageParams {
  q?: string;
  positions?: Pompfe[];
  city?: string | null;
  sort?: PlayerSort;
}

// --- Shared shell view models ---------------------------------------------------

/** A removable active-filter chip shown above the results. */
export interface FilterChip {
  /** Stable key identifying which filter this chip represents. */
  key: string;
  label: string;
}

/** The four list states (+ ready) the shared shell renders. */
export type BrowseState = 'loading' | 'ready' | 'empty' | 'no-results' | 'error';
