# Contract: the wizard draft store

There is **no network contract in this feature** — no endpoint is added, changed, or called. The contract below is the client-side one: between the two wizards and the browser storage they now depend on, and between one release of the app and the next.

## Surface

`WizardDraftStore`, provided in root, in `frontend/apps/web/src/app/core/drafts/`.

| Operation | Behaviour |
|---|---|
| `readTraining(slug)` | Returns a valid `TrainingDraft`, or `null`. Never throws. A draft that fails the compatibility rule is removed as a side effect. |
| `writeTraining(slug, draft)` | Serialises and stores. Never throws. |
| `clearTraining(slug)` | Removes that team's draft. Never throws. |
| `readEvent()` / `writeEvent(draft)` / `clearEvent()` | As above, unscoped. |
| `clearAll()` | Removes every `jh-draft:` entry. Called by `AuthService` on sign-out and on session loss (R6). |

**"Never throws" is the contract, not a courtesy** (FR-015). Every access is wrapped, following the two existing storage call sites in this app. Private browsing, a full quota, storage disabled by policy, and a `JSON.parse` failure on hand-edited data all degrade to "no draft" — the wizard behaves exactly as it does today.

## Keys

| Wizard | Key |
|---|---|
| create-training | `jh-draft:training:<slug>` |
| create-event | `jh-draft:event` |

`jh-` matches the app's existing keys (`jh.lang`, `jh-chunk-reloaded`). `clearAll()` depends on the shared `jh-draft:` prefix, so any future draft must adopt it.

## Storage medium

`window.sessionStorage`. This is part of the contract, not an implementation detail — FR-010 requires that a draft not survive the tab closing, and the choice is what bounds the exposure of the persisted fee fields (R1, data-model). Switching to `localStorage` would silently break FR-010 and invalidate the privacy-policy text written under FR-019.

## Compatibility rule

Every stored value is a JSON object carrying `v: DRAFT_VERSION`.

On read, a draft is **discarded and removed** — and the wizard opens blank — when any of these holds:

1. `JSON.parse` throws.
2. The parsed value is not a non-null object.
3. `v` is absent or `!== DRAFT_VERSION`.

**`DRAFT_VERSION` MUST be incremented whenever a persisted field is added, removed, renamed, or changes meaning.** Adding a field without bumping is the exact failure this rule exists to prevent: an older draft restores the wizard with the new field silently at its default, mixed in with restored values, and the user cannot tell which of their answers survived.

The rule is version equality, not a range — there is no migration path between draft versions and there should not be one. A draft is worth at most a few minutes of retyping; migration code for it would be permanent.

## Lifecycle guarantees

- A draft is written only once an answer differs from the wizard's pristine state (FR-013). Note the pristine state is not blank — see R7.
- A draft is cleared **after the server accepts** the create, before navigating away (FR-007). Never optimistically: a rejected create must leave the answers intact (US1 scenario 6).
- The create-training wizard's existing Cancel clears its draft (FR-008).
- Sign-out and session loss clear all drafts (FR-011), via `AuthService` (R6).
- Nothing else clears a draft. In particular, no timer expires one and no discard control exists (FR-009).

## Non-goals

- No draft ever crosses the network boundary (FR-014). The store has no `HttpClient` dependency and must not acquire one.
- No draft holds a token, session identifier, or credential (constitution IV).
- No retry, timeout, or circuit breaker belongs anywhere near this — there is no network call (constitution VII is not engaged; see the plan's Constitution Check).
