import { CityOption } from '../models/city.models';
import { LocationKind, TrainingInterval, TrainingVisibility } from '../models/trainings.models';
import { EventType, ParticipantMode } from '../models/event.models';

/**
 * The shape of an unfinished create-wizard, as kept in `sessionStorage` (feature 045, GH #182).
 *
 * ⚠ **Bump {@link DRAFT_VERSION} whenever a persisted field is added, removed, renamed, or changes
 * meaning.** A long-lived tab can outlive a deploy — that is why `ChunkLoadErrorHandler` exists —
 * and a draft restored against a changed field set is worse than no draft at all: some answers come
 * back, others silently sit at their defaults, and the user cannot tell which is which. There is no
 * migration path between versions and there should not be one; a draft is worth a few minutes of
 * retyping, migration code would be permanent.
 *
 * Transient UI state is deliberately absent. `busy`/`submitting` must never be persisted (restoring
 * `true` leaves the submit button disabled forever with no way out) and neither must `error` (a
 * stale message about a request that is no longer in flight). The training `slug` is part of the
 * storage *key*, never the value — the route is its authority.
 */
export const DRAFT_VERSION = 1;

/** Which of the five create-training steps the user was on. */
export type TrainingDraftStep = 1 | 2 | 3 | 4 | 5;

/** Which of the six create-event steps the user was on. Shared with `EventCreateComponent`. */
export type EventDraftStep = 'type' | 'when' | 'where' | 'who' | 'fee' | 'review';

/**
 * One unfinished create-training wizard: 16 answers plus the step. Mirrors
 * `TrainingCreateComponent` field for field.
 */
export interface TrainingDraft {
  v: number;
  step: TrainingDraftStep;
  isRecurring: boolean;
  name: string;
  weekday: string;
  interval: TrainingInterval;
  startTime: string;
  endTime: string;
  startDate: string;
  endDate: string;
  locationKind: LocationKind;
  venueName: string;
  street: string;
  postalCode: string;
  /**
   * Stored whole rather than as an id: the request needs `externalId`, and the picker needs the
   * `label` to render its chip on restore. See `WizardDraftStore` and the `[initialCity]` binding.
   */
  city: CityOption | null;
  virtualLink: string;
  description: string;
  visibility: TrainingVisibility;
}

/**
 * One unfinished create-event wizard: 21 answers plus the step.
 *
 * Note the answers come from **two** places in `EventCreateComponent` — seven signals and a
 * thirteen-control `FormGroup` — and both belong here. The signals are the half that is easy to
 * forget, because they sit outside the form.
 *
 * ⚠ `feeRecipientName` and `feeIban` are persisted by explicit owner decision, so a bank account
 * number is written to browser storage. That is bounded rather than omitted: `sessionStorage` means
 * it cannot outlive the tab, `WizardDraftStore.clearAll()` on sign-out means it cannot outlive the
 * session, and the privacy policy names it. See specs/045-wizard-draft-persistence/data-model.md.
 */
export interface EventDraft {
  v: number;
  step: EventDraftStep;
  type: EventType;
  locationKind: LocationKind;
  participantMode: ParticipantMode;
  isPaid: boolean;
  venueName: string;
  street: string;
  postalCode: string;
  city: CityOption | null;
  name: string;
  customLabel: string;
  description: string;
  startsAt: string;
  endsAt: string;
  virtualLink: string;
  participationLimit: number;
  rosterCap: number;
  feeAmount: number | null;
  feeCurrency: string;
  feeRecipientName: string;
  feeIban: string;
  feePaymentDeadline: string;
}
