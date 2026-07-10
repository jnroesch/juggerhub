/**
 * Badges & achievements (feature 012) — client contracts mirroring the backend Dtos/Recognition.
 * Public display exposes only name/description/icon/earned-date (+ achievement context); the admin
 * note and granter are never sent to public pages.
 */

/** Which catalogue an earned item belongs to — drives its icon endpoint. */
export type RecognitionKind = 'badge' | 'achievement';

/** One earned badge or achievement as shown on a profile / team page. */
export interface EarnedRecognition {
  definitionId: string;
  name: string;
  description: string;
  hasIcon: boolean;
  /** ISO timestamp. */
  earnedAt: string;
  /** Achievements only. */
  contextYear: number | null;
  /** Achievements only. */
  contextLabel: string | null;
}

/** Builds the public icon URL for a definition, by catalogue. */
export function recognitionIconUrl(kind: RecognitionKind, definitionId: string): string {
  return `/api/v1/${kind === 'badge' ? 'badges' : 'achievements'}/${definitionId}/icon`;
}

// --- Admin (grant/revoke) contracts ---------------------------------------

/** An admin catalogue entry (a badge or achievement definition). */
export interface RecognitionDefinition {
  id: string;
  name: string;
  description: string;
  appliesToPlayers: boolean;
  appliesToTeams: boolean;
  isRetired: boolean;
  hasIcon: boolean;
  /** Number of currently-active awards of this type (feature 014). */
  grantedCount: number;
  /** ISO timestamp the definition was created (feature 014). */
  createdAt: string;
}

/** Create/edit payload for a catalogue type — the kind chooses the endpoint, not this body. */
export interface RecognitionUpsert {
  name: string;
  description: string;
  appliesToPlayers: boolean;
  appliesToTeams: boolean;
}

/** One award in the admin subject view — carries the award id (to revoke), note, and granter. */
export interface AdminAward {
  awardId: string;
  definitionId: string;
  name: string;
  earnedAt: string;
  grantedByName: string;
  note: string | null;
  contextYear: number | null;
  contextLabel: string | null;
}

/** A subject's current awards for the admin grant/revoke UI. */
export interface AdminSubjectAwards {
  subjectRef: string;
  badges: AdminAward[];
  achievements: AdminAward[];
}

export type AdminSubjectType = 'player' | 'team';
