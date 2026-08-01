/**
 * Self-service account deletion (feature 037). Mirrors the wire contract in
 * `specs/037-account-deletion/contracts/account-deletion.md`.
 */

/** Why deletion can't proceed yet. A key, not prose — this text ships in three languages. */
export type DeletionBlockerKind = 'SoleTeamAdmin' | 'SoleEventAdmin' | 'SolePartyAdmin';

/** What the member can do about it. Also a key, for the same reason. */
export type DeletionBlockerRemedy = 'MakeAnotherAdmin' | 'MakeAnotherAdminOrDisband';

/**
 * One outstanding obligation. `subjectName` names the team, event or party — never a person, so a
 * refusal can say which without disclosing anyone.
 */
export interface DeletionBlocker {
  kind: DeletionBlockerKind;
  subjectId: string;
  subjectName: string;
  remedy: DeletionBlockerRemedy;
}

/**
 * The answer to "what happens if I do this, and may I?". Advisory only — the server re-checks
 * everything here at confirmation, so a blocker acquired in between is caught there.
 */
export interface AccountDeletionPreview {
  canDelete: boolean;
  blockers: DeletionBlocker[];
  /** Category keys the client renders from its own catalogue. */
  retained: string[];
  erased: string[];
}
