/**
 * Canonical Jugger pompfen catalog (+ the Läufer position), shared by the profile
 * owner selector and the public "Plays" section. The enum string values mirror the
 * backend `Pompfe` enum names exactly. Order here is the display order.
 *
 * Labels are NOT held here — they live in the i18n catalogs under `pompfen.*` so a
 * player sees each name in the language they picked (feature 031). Render them with
 * the transloco pipe (`entry.labelKey | transloco`) or `TranslocoService.translate`.
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
  /** Transloco key for the label in the active language. */
  labelKey: string;
}

/** Läufer is a position, not a pompfe, but it lives in the same selector unmarked. */
export const POMPFEN_CATALOG: readonly PompfeCatalogEntry[] = [
  { value: 'Stab', labelKey: 'pompfen.Stab' },
  { value: 'Langpompfe', labelKey: 'pompfen.Langpompfe' },
  { value: 'Schild', labelKey: 'pompfen.Schild' },
  { value: 'QTip', labelKey: 'pompfen.QTip' },
  { value: 'Kette', labelKey: 'pompfen.Kette' },
  { value: 'DoppelKurz', labelKey: 'pompfen.DoppelKurz' },
  { value: 'Laeufer', labelKey: 'pompfen.Laeufer' },
];

/** The i18n key holding a pompfe's label in the active language. */
export function pompfeLabelKey(value: Pompfe): string {
  return `pompfen.${value}`;
}
