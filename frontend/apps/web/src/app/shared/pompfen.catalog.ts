/**
 * Canonical Jugger pompfen catalog (+ the Läufer position), shared by the profile
 * owner selector and the public "Plays" section. The enum string values mirror the
 * backend `Pompfe` enum names exactly. Order here is the display order.
 */

export type Pompfe =
  | 'Stab'
  | 'Langpompfe'
  | 'Schild'
  | 'QTip'
  | 'Kette'
  | 'DoppelKurz'
  | 'Laeufer';

export interface PompfeCatalogEntry {
  /** Matches the backend enum name / API value. */
  value: Pompfe;
  /** German label. */
  de: string;
  /** English label. */
  en: string;
  /** Läufer is a position, not a pompfe, but lives in the same selector. */
  isPosition: boolean;
}

export const POMPFEN_CATALOG: readonly PompfeCatalogEntry[] = [
  { value: 'Stab', de: 'Stab', en: 'Staff', isPosition: false },
  { value: 'Langpompfe', de: 'Langpompfe', en: 'Long', isPosition: false },
  { value: 'Schild', de: 'Schild', en: 'Shield', isPosition: false },
  { value: 'QTip', de: 'Q-Tip', en: 'Q-Tip', isPosition: false },
  { value: 'Kette', de: 'Kette', en: 'Chain', isPosition: false },
  { value: 'DoppelKurz', de: 'Doppel-Kurz', en: 'Double-Short', isPosition: false },
  { value: 'Laeufer', de: 'Läufer', en: 'Runner', isPosition: true },
];

/** Lookup a catalog entry by its API value. */
export function pompfeLabel(value: Pompfe): PompfeCatalogEntry | undefined {
  return POMPFEN_CATALOG.find((p) => p.value === value);
}
