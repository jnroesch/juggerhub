# Phase 1 Data Model: Wizard drafts

**Client-side only.** Nothing here is an entity, a table, or a DTO. No migration exists for this feature and none should. These shapes live in `frontend/apps/web/src/app/core/drafts/wizard-draft.models.ts` and are serialised to `sessionStorage`.

---

## `TrainingDraft`

One unfinished create-training wizard. 16 answers + the step. Mirrors [`TrainingCreateComponent`](../../frontend/apps/web/src/app/features/trainings/training-create/training-create.component.ts#L30-L58) field for field.

| Field | Type | Source field | Notes |
|---|---|---|---|
| `v` | `number` | — | `DRAFT_VERSION`. See [contract](./contracts/wizard-draft.md). |
| `step` | `1 \| 2 \| 3 \| 4 \| 5` | `step` | Restored as the opening step (FR-003). Also changes when `create()` fails and resets to 2. |
| `isRecurring` | `boolean` | `isRecurring` | Default `true` — not blank; see R7. |
| `name` | `string` | `name` | |
| `weekday` | `string` | `weekday` | Default is *today's* weekday, so a pristine draft is not empty. |
| `interval` | `TrainingInterval` | `interval` | `Weekly \| BiWeekly \| Monthly`. |
| `startTime` | `string` | `startTime` | `HH:mm`. Default `19:00`. |
| `endTime` | `string` | `endTime` | `HH:mm`. Default `21:00`. |
| `startDate` | `string` | `startDate` | `YYYY-MM-DD` or empty. |
| `endDate` | `string` | `endDate` | `YYYY-MM-DD` or empty; only meaningful when recurring. |
| `locationKind` | `LocationKind` | `locationKind()` | `InPerson \| Virtual`. |
| `venueName` | `string` | `venueName()` | |
| `street` | `string` | `street()` | |
| `postalCode` | `string` | `postalCode()` | |
| `city` | `CityOption \| null` | `selectedCity()` | **Restored before first render and passed to `[initialCity]`** — see R3. Stored whole because the picker needs the label and the request needs the `externalId`. |
| `virtualLink` | `string` | `virtualLink()` | |
| `description` | `string` | `description` | |
| `visibility` | `TrainingVisibility` | `visibility` | `TeamOnly \| Public`. Default `TeamOnly`. |

**Key**: `jh-draft:training:<slug>` — one per team (FR-006).

---

## `EventDraft`

One unfinished create-event wizard. 21 answers + the step. Mirrors [`EventCreateComponent`](../../frontend/apps/web/src/app/features/events/event-create/event-create.component.ts#L39-L74); note the answers come from **two** places — 7 signals and a 13-control `FormGroup` — and both must be in the draft (plan, risk 3).

| Field | Type | Source | Notes |
|---|---|---|---|
| `v` | `number` | — | `DRAFT_VERSION`. |
| `step` | `Step` | `step()` | `type \| when \| where \| who \| fee \| review`. |
| `type` | `EventType` | signal | Default `Tournament`. |
| `locationKind` | `LocationKind` | signal | |
| `participantMode` | `ParticipantMode` | signal | Default `Teams`. |
| `isPaid` | `boolean` | signal | Default `false`. |
| `venueName` | `string` | signal | |
| `street` | `string` | signal | |
| `postalCode` | `string` | signal | |
| `city` | `CityOption \| null` | `selectedCity()` signal | Same `[initialCity]` requirement as trainings (R3). |
| `name` | `string` | form control | |
| `customLabel` | `string` | form control | Only meaningful when `type === 'Other'`. |
| `description` | `string` | form control | Mandatory in this wizard — the most expensive field to retype. |
| `startsAt` | `string` | form control | |
| `endsAt` | `string` | form control | |
| `virtualLink` | `string` | form control | |
| `participationLimit` | `number` | form control | Default `16`. |
| `rosterCap` | `number` | form control | Default `8`. |
| `feeAmount` | `number \| null` | form control | |
| `feeCurrency` | `string` | form control | Default `EUR`. |
| `feeRecipientName` | `string` | form control | ⚠ see below. |
| `feeIban` | `string` | form control | ⚠ see below. |
| `feePaymentDeadline` | `string` | form control | |

**Key**: `jh-draft:event` — one draft; the wizard is not scoped to a team.

### ⚠ `feeRecipientName` and `feeIban`

These are persisted **by explicit owner decision** (spec, Assumptions). A bank account number is therefore written to browser storage. The decision was taken with the alternative on the table — excluding the two fee fields so they are retyped on restore — and this is where the consequence is recorded, not hidden:

- It is bounded in **time** by FR-010: the draft cannot outlive the browser tab. `sessionStorage` was chosen over `localStorage` for this reason and no other (R1).
- It is bounded by **owner** by FR-011: drafts are cleared on sign-out, so a shared device does not hand it to the next person.
- It is **disclosed** by FR-019: the privacy policy names the draft and says what it may contain.
- It is **not** bounded within a session: with no discard control (FR-009), an abandoned draft returns in the same tab. That residual is stated in the spec's Decision on restore surfacing.

Reversing the decision later is a two-line change — omit both fields on write — plus a `DRAFT_VERSION` bump.

---

## Excluded from both drafts

Transient UI state. Persisting any of it is a defect, not an omission:

| Not persisted | Why |
|---|---|
| `busy` / `submitting` | Restoring `true` disables the submit button permanently with no way out (plan, risk 4). |
| `error` | A stale error message about a request that is no longer in flight. |
| `slug` (training) | Comes from the route, which is the authority. It is part of the *key*, never the value. |
| Anything derived — `whereComplete`, `summaryCount`, `stepIndex`, `canAdvance` | Recomputed from the restored answers. |
| Auth state of any kind | No token, session id, user id or credential enters a draft (constitution IV). |

---

## Lifecycle

```text
construct ─► snapshot pristine ─► read draft ─┬─ absent/incompatible ─► blank wizard
                                              └─ valid ──────────────► restore answers + step
                                                                        (before first render — R3)
   answer changes ─► differs from pristine? ─┬─ no ──► write nothing (FR-013)
                                             └─ yes ─► write draft
   cleared by: create accepted by server (FR-007) · Cancel, trainings (FR-008)
               sign-out (FR-011) · tab closed (FR-010, implicit)
```
