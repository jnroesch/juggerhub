import { Location } from '../core/models/city.models';

/**
 * The single frontend helper for rendering a structured location (feature 030, FR-010). Mirrors the
 * backend "City, Country" label. Peripheral endpoints already return that string directly; this is
 * for the structured {@link Location} shape used on profiles, cards, and detail pages.
 */
export function locationLabel(location: Location | null | undefined): string | null {
  return location?.label ?? null;
}
