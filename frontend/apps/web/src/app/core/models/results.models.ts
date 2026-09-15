/**
 * Tournament results API contracts (feature 050) — mirror of backend Dtos/Results and
 * Dtos/Admin/AdminResultDtos. Enums arrive as their names.
 * Shapes: specs/050-tournament-results/contracts/results-api.md.
 */

import { ParticipantMode } from './event.models';

export type ResultSource = 'None' | 'Manual' | 'TugenyImport';
export type MatchWinner = 'First' | 'Second' | 'Draw';

// --- Reads (any signed-in user) -------------------------------------------------------------

export interface TournamentResult {
  source: ResultSource;
  importedAt: string | null;
  editedSinceImport: boolean;
  resultsChangedAt: string | null;
  tugeny: TugenyLink | null;
  placements: Placement[];
  rankedCount: number;
  matchCount: number;
  viewer: { canEdit: boolean };
}

export interface TugenyLink {
  tournamentId: number;
  slug: string;
  name: string;
  startDate: string | null;
  liveUrl: string;
  tournamentUrl: string;
}

export interface Placement {
  id: string;
  position: number;
  name: string;
  teamSlug: string | null;
}

export interface MatchSide {
  name: string;
  teamSlug: string | null;
}

export interface TournamentMatch {
  id: string;
  stage: string | null;
  name: string;
  first: MatchSide;
  second: MatchSide;
  firstScores: number[];
  secondScores: number[];
  winner: MatchWinner;
}

export interface TeamPlacement {
  eventId: string;
  eventName: string;
  date: string;
  position: number;
  rankedCount: number;
}

// --- The results page (event admins) --------------------------------------------------------

/** A team with a confirmed JuggerHub sign-up for the event — the only teams an event admin can connect. */
export interface SignedUpTeam {
  teamId: string;
  teamSlug: string;
  teamName: string;
}

export interface EditorPlacement {
  id: string;
  position: number;
  name: string;
  sourceName: string;
  teamId: string | null;
  teamSlug: string | null;
  connectedBy: string | null;
  connectedAt: string | null;
}

export interface ResultEditor {
  eventId: string;
  eventName: string;
  participantMode: ParticipantMode;
  isTournament: boolean;
  isCancelled: boolean;
  hasStarted: boolean;
  hasEnded: boolean;
  canRecord: boolean;
  source: ResultSource;
  importedAt: string | null;
  editedSinceImport: boolean;
  resultsChangedAt: string | null;
  tugeny: TugenyLink | null;
  linkedElsewhere: boolean;
  placements: EditorPlacement[];
  signedUpTeams: SignedUpTeam[];
}

/** One row sent to `PUT …/results/ranking`. `id` keeps an existing placement (and its connection). */
export interface RankingRow {
  id: string | null;
  position: number;
  name: string;
  teamId: string | null;
}

// --- Tugeny ---------------------------------------------------------------------------------

export interface TugenyLinked {
  tournamentId: number;
  slug: string;
  name: string;
  startDate: string | null;
  linkedElsewhere: boolean;
}

export interface ImportPlacement {
  position: number;
  name: string;
  tugenyTeamId: number;
}

export interface TugenyImportPreview {
  tournamentName: string;
  placements: ImportPlacement[];
  matchCount: number;
  signedUpTeams: SignedUpTeam[];
  replacesExisting: boolean;
}

export interface ImportConnection {
  tugenyTeamId: number;
  teamId: string;
}

// --- Platform admin -------------------------------------------------------------------------

export interface AdminPlacement {
  id: string;
  eventId: string;
  eventName: string;
  eventDate: string;
  position: number;
  rankedCount: number;
  sourceName: string;
  name: string;
  team: { slug: string; name: string } | null;
  connectedBy: string | null;
  connectedAt: string | null;
  fromTugeny: boolean;
}
