/**
 * Structured location contracts (mirror of backend Dtos/Cities — feature 030). Cities are
 * resolved from the self-hosted geocoder via the backend proxy; the browser never calls the
 * geocoder directly.
 */

/**
 * A country offered by the browse country filter's type-ahead (feature 030). Sourced from the
 * canonical cities, so only countries with a located team/event/player are suggested.
 */
export interface Country {
  /** ISO country code (may be null for a few reference rows). */
  code: string | null;
  /** Display name, e.g. "Germany". */
  name: string;
}

/** A transient city search result offered by the picker. Latitude/longitude are display hints. */
export interface CityOption {
  externalId: string;
  name: string;
  region: string | null;
  countryName: string;
  countryCode: string | null;
  /** "City, Region, Country" — enough to tell same-named cities apart. */
  label: string;
  latitude: number;
  longitude: number;
}

/** The read shape shown wherever a profile/team/event location appears. Null ⇒ no location set. */
export interface Location {
  /** Provider place id — lets an edit form resend the current city without re-picking. */
  externalId: string;
  name: string;
  region: string | null;
  countryName: string;
  countryCode: string | null;
  /** "City, Country" — the display string (FR-010). */
  label: string;
}

/**
 * The write fragment sent to set/clear a location on a profile/team/event update.
 * `cityExternalId: null` clears; a value selects that city. `name` is only a re-resolution hint.
 */
export interface LocationSelection {
  cityExternalId: string | null;
  name: string | null;
}

/** Build a {@link LocationSelection} from a picked option, or the clear payload from `null`. */
export function toSelection(option: CityOption | null): LocationSelection {
  return option
    ? { cityExternalId: option.externalId, name: option.name }
    : { cityExternalId: null, name: null };
}
