---

description: "Task list for feature 037 — self-service account deletion"
---

# Tasks: Self-Service Account Deletion

**Input**: Design documents from `/specs/037-account-deletion/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: **Included, and not optional here.** The spec demands them by name — SC-013 requires ban-bars and deletion-permits be "verified as two tests of the same registration path rather than one test and an assumption", and [quickstart.md](quickstart.md) makes "all seven `!= Banned` predicates have an explicit test" a done-criterion. The seven-predicate audit (research R3) cannot be verified by inspection: the predicates *compile and pass* while failing open.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Exact file paths in every description

## Path Conventions

Web app per [plan.md](plan.md): `backend/` (.NET 10) and `frontend/apps/web/src/app/` (Angular 21, zoneless). Backend tests in `backend/tests/JuggerHub.Api.IntegrationTests/`; frontend specs sit beside their component.

---

## Phase 1: Setup

**Purpose**: No new scaffolding — this feature adds no project, package, or container. Only the review surface.

- [X] T001 Copy `.specify/templates/ui-review-checklist-template.md` to `specs/037-account-deletion/checklists/ui-review.md` (constitution Quality Gate 7)
- [X] T002 [P] Create the test folder `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/` for this feature's suites
- [X] T003 [P] Read [DESIGN.md](../../DESIGN.md) destructive-action guidance and confirm the `danger-fg`/`danger-bg`/`danger-border` tokens and `jh-alert tone="danger"` cover a danger-zone section; record any conflict in `checklists/ui-review.md` rather than inventing a style

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Make `AccountStatus.Deleted` safe to exist. **Nothing user-visible ships in this phase.**

**⚠️ CRITICAL**: No user story work may begin until T012 passes. A new enum value silently satisfies every existing `!= Banned` predicate (research R3); three of them fail open. Shipping the erasure before this is a security defect, not a sequencing preference.

- [X] T004 Add `Deleted = 3` to `AccountStatus` in `backend/Entities/AccountEnums.cs`, with an XML comment stating it is **terminal** and distinct from the reversible `Banned` soft-delete
- [X] T005 Generate the EF migration in `backend/Data/Migrations/` — the column is already `int`, so this must produce **no schema change**; verify the generated `Up()` is empty and delete the migration if EF emits nothing
- [X] T006 Replace the fail-open predicate at `backend/Services/Chat/ChatConversationService.cs:53` with an explicit positive test (`Status == Active || Status == Suspended`), not another `!=` exclusion
- [X] T007 Replace the same predicate at `backend/Services/Chat/ChatConversationService.cs:161` ("can I DM this person?")
- [X] T008 Replace the same predicate at `backend/Services/Chat/ChatConversationService.cs:822` (participant name resolution)
- [X] T009 Audit the four query filters in `backend/Data/AppDbContext.cs` (lines 137, 179, 201, 345) and add a comment at each recording **why** it is safe unchanged — the filtered row cascades away with `PlayerProfile`. Do not change them; the comment is the deliverable so the next enum value gets a second look
- [X] T010 Ensure `AdminUserService.ReinstateAsync`/`UnbanAsync` in `backend/Services/Admin/AdminUserService.cs` refuse `Deleted` by **not** adding it to their `from:` sets, and add an explicit guard returning a distinct outcome rather than falling through silently
- [X] T011 [P] Verify admin user list and overview projections in `backend/Services/Admin/AdminUserService.cs` and `AdminOverviewService.cs` do not null-ref on an account whose `PlayerProfile` is gone; project defensively
- [X] T012 Write `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/DeletedAccountVisibilityTests.cs` — **one test per predicate site (7)**, each asserting a `Deleted` account is excluded. This is the phase gate; it must be red before T006–T009 and green after

### Placeholder promotion (plan Complexity Tracking)

- [X] T013 Promote `PlaceholderName` from `internal const` on `backend/Services/Chat/ChatConversationService.cs:1154` to a shared constant reachable outside the Chat namespace, keeping the existing value as the English text
- [X] T014 Localise the placeholder through the existing service in `backend/Services/Localization/`, with `en`/`de`/`es` entries; English keeps the current wording so chat output is unchanged for existing readers
- [X] T015 [P] Update the chat call sites that referenced the old constant (`ChatMessageService.cs:400,408`, `ChatBlockService.cs:40`, `ChatConversationService.cs:470,698,780,1188,1207`) and confirm existing chat tests still pass unchanged

### Vocabulary (FR-030)

- [X] T064 Decide and record the member-facing vocabulary for erasure-vs-ban in `en`/`de`/`es` **before any catalogue text is written** (blocks T032, T041, T054): audit existing member-facing strings for "delete"/"löschen"/"eliminar" already applied to ban or to content removal, choose a distinct term for account erasure in each language, and record the three chosen pairs in `specs/037-account-deletion/checklists/ui-review.md`. German is the highest risk — *löschen* reads naturally for both meanings, so the collision is likelier there than in English

**Checkpoint**: `Deleted` exists, fails closed everywhere, the placeholder is shared and localised, and the vocabulary is settled. Nothing is deletable yet.

---

## Phase 3: User Story 1 — A member deletes their own account (Priority: P1) 🎯 MVP

**Goal**: A signed-in member erases their own account from `/account` and is gone.

**Independent Test**: Sign in as a member with profile, team membership, notifications and chat history. Delete via the UI. Verify signed out, cannot sign in, and name/handle/email/photo appear nowhere.

### Tests for User Story 1

- [X] T016 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/AccountErasureTests.cs` covering the full disposition inventory in [data-model.md §2](data-model.md) — assert each cascaded, explicitly-deleted, and retained table reaches its stated state
- [X] T017 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/ErasureAtomicityTests.cs` — force a fault mid-transaction and assert the account is **fully** intact (profile, memberships, chat), plus the repeat/concurrent case (FR-038, FR-039)
- [X] T018 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/ReRegistrationTests.cs` — **both directions** (SC-013): a deleted address registers successfully **and the account exists and can sign in**; a banned address does not. Assert the created row, never just the HTTP status — registration returns a neutral acceptance either way (research R4)
- [X] T019 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/ArchivedConversationTests.cs` — archive a conversation (delete a team), then erase a participant, and assert the snapshot exposes no personal name: `Conversation.Name` holds the team name, the frozen participant rows resolve to the placeholder, and history still reads (FR-028). A **regression guard** proving the freeze stays impersonal, not a fix — see corrected [data-model.md §3](data-model.md)
- [X] T066 [P] [US1] Add an enumeration-oracle test to `ReRegistrationTests.cs` (SC-008): sign-in with erased credentials MUST return the identical response — status, body, and error shape — as sign-in with an address that never existed. A `Deleted` status check is exactly how such an oracle gets introduced, so assert the equivalence rather than assuming it (constitution Principle I)

### Implementation for User Story 1

- [X] T020 [US1] Create `backend/Services/Account/IAccountDeletionService.cs` with the preview and erase operations and their result types, per [contracts/account-deletion.md](contracts/account-deletion.md)
- [X] T021 [US1] Implement identity neutralisation in `backend/Services/Account/AccountDeletionService.cs` per [data-model.md §1](data-model.md): null `Email`/`NormalizedEmail`, **randomise `UserName`/`NormalizedUserName`** (FR-034 — load-bearing, see risk 6), clear credentials and contact fields, set `Status = Deleted` and `StatusChangedAt`
- [X] T022 [US1] Implement the owned-data deletion in the same service: delete `PlayerProfile` (cascading pompfen, avatar, participations, awards received) and explicitly delete `RefreshToken` rows — **delete, not revoke**, since `CreatedByIp` must go (FR-016)
- [X] T023 [US1] Implement deletion of participation records, notifications, preferences, `UserBlock` rows (both directions), `ConversationParticipant`, and pending invitations, using `ExecuteDeleteAsync` and setting `ModifiedDate` where the change tracker is bypassed (constitution Gate 2)
- [X] T024 [US1] ~~Neutralise the member's display name inside archived `Conversation.Name`~~ — **verified unnecessary**, see corrected [data-model.md §3](data-model.md). The archival freeze writes team/event names and literals only; a group's name is user-typed content retained under FR-024. Deliverable is the verification recorded in T019, not a scrubbing pass
- [X] T025 [US1] Wrap the whole operation in `IExecutionStrategy.ExecuteAsync` with a single transaction and **all** mutation inside the delegate (constitution Principle VII); stage nothing outside it
- [X] T026 [US1] Reclaim the profile photo's blob object via `IMediaStore.DeleteAsync` (FR-015). **Feature 035 is merged** — `ProfileAvatar` is a descriptor (`ObjectKey` + `SizeBytes`), so the cascade deletes the pointer and leaves the image. Read `ObjectKey` **before** the transaction removes the row, delete the object **after** commit (a blob delete cannot be rolled back), and do not report success if the reclaim failed. 035's `MediaReconciliationService` sweep is a backstop, not the mechanism — see corrected research R8
- [X] T027 [US1] Add the account-deleted email template to `backend/EmailTemplates/` extending the existing base header/footer, HTML with inline CSS (constitution), in `en`/`de`/`es`
- [X] T028 [US1] Send the confirmation email **before** the address is nulled, inside the operation; write the judgement at the call site that a send failure MUST NOT roll back the erasure (Principle VII requires the reasoning be recorded where it is made)
- [X] T029 [US1] Add `POST /api/v1/account/deletion` to `backend/Controllers/AccountController.cs` per the contract — thin, no account id accepted, mapping service status to 204/400/401/403/409/500
- [X] T030 [US1] Enforce the server-side guards in the service: caller is the auth principal only (FR-002), `Suspended`/`Banned` refused independently of the sign-in gate (FR-005), password re-verified via `CheckPasswordSignInAsync(..., lockoutOnFailure: true)` (FR-003)
- [X] T031 [US1] Add the danger-zone section and confirmation dialog to `frontend/apps/web/src/app/features/account/` as separate `.ts`/`.html`/`.css` files (constitution Principle VI), using the DESIGN.md danger tokens; clear auth state and navigate to a public route on 204
- [X] T032 [US1] Add the deletion call to the frontend API client and **exclude this POST from any retry** (Principle VII — a retried mutation may have already erased the account); add the `en`/`de`/`es` catalogue entries for the dialog and the confirmation word

**Checkpoint**: A member can erase their own account and it is genuinely gone. Observer-side rendering on non-chat surfaces is not yet hardened — that is US3, and this checkpoint is not deployable without it.

---

## Phase 4: User Story 2 — Knowing what will happen, and being stopped (Priority: P2)

**Goal**: The disclosure is accurate, and a member holding a sole-admin obligation is refused with every blocker named at once.

**Independent Test**: As sole admin of two teams, open the flow. Both teams named in one message with a remedy. Resolve one, retry, the other is still named. Resolve both, the flow proceeds.

### Tests for User Story 2

- [X] T033 [P] [US2] Write `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/DeletionBlockerTests.cs` — sole admin of two teams yields **both** in one response (FR-011); sole event admin and sole party admin each block; a member with none is clear
- [X] T034 [P] [US2] Add a race test to the same file: preview clean → become sole admin in a second session → confirm → assert **409 and the account intact** (FR-013, precondition re-checked inside the transaction)
- [X] T065 [P] [US2] Write `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/ModerationRefusalTests.cs` (FR-005) — call **both** endpoints directly with a still-valid token for a **suspended** and for a **banned** account, asserting 403 and an entirely unchanged account each time. Must exercise the server-side status check, **not** the sign-in gate: obtain the token before the status changes, so the test would still fail if the guard were removed and only sign-in blocked access. This guard is what makes FR-031 and FR-033 safe

### Implementation for User Story 2

- [X] T035 [US2] Implement the precondition query in `backend/Services/Account/AccountDeletionService.cs` gathering **all** blockers in one pass — new code; the existing guard at `backend/Services/Teams/TeamService.cs:396` is per-team and cannot be reused directly
- [X] T036 [US2] Define and implement the sole-event-admin and sole-party-admin blockers, which have no equivalent guard today (FR-010, plan Complexity Tracking); mirror the team rule rather than inventing different semantics
- [X] T037 [US2] Re-run the precondition query **inside** the transaction in the erase path, returning the 409 shape from the contract (FR-013)
- [X] T038 [US2] Add `GET /api/v1/account/deletion-preview` to `backend/Controllers/AccountController.cs`, returning `canDelete`, the complete `blockers` array, and `retained`/`erased` as **enum keys not prose** (the client owns the three-language catalogue)
- [X] T039 [US2] Build the pre-confirmation disclosure in the account dialog stating what is erased, what is retained, and that it cannot be undone (FR-006)
- [X] T040 [US2] State plainly in the disclosure that **messages and posts remain, attributed to no one** (FR-025) and that identifying text a member typed themselves survives with them (FR-027) — this is a correctness surface, not copy (plan risk 3)
- [X] T041 [US2] Render blockers from `kind` + `subjectName` with a localised remedy per blocker, in `en`/`de`/`es` (FR-012)
- [X] T042 [US2] Ensure abandoning or cancelling the dialog changes nothing, and that reopening re-fetches the preview rather than serving a cached refusal (FR-013)

**Checkpoint**: US1 and US2 both work. The flow is honest and refuses safely.

---

## Phase 5: User Story 3 — The platform stays coherent for everyone else (Priority: P3)

**Goal**: Every surface that showed the departed member renders without error, showing the neutral placeholder.

**Independent Test**: From a second account, open a shared conversation, team roster, event participant list, news feed and notification list. All render; all show the placeholder; none expose name, handle, email or photo.

### Tests for User Story 3

- [X] T043 [P] [US3] Write `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/PostErasureReadPathTests.cs` — after erasure, assert chat history, team roster, event participant list, party roster, news posts and notifications all return successfully and carry the placeholder
- [X] T044 [P] [US3] Add a non-re-attribution test to the same file (SC-005): no retained message or post exposes any field from which the author can be recovered, including via admin tooling

### Implementation for User Story 3

- [X] T045 [US3] Verify the chat read path in `backend/Services/Chat/ChatMessageService.cs:390-413` needs **no change** — it already falls back to the placeholder when `Sender.Profile.DisplayName` projects null (research R2) — and record the confirmation rather than editing working code
- [X] T046 [P] [US3] Apply the shared placeholder to news post author projections in `backend/Services/Teams/`, `backend/Services/Events/` and `backend/Services/Parties/` where an author's profile may now be absent
- [X] T047 [P] [US3] Apply the same to notification actor projections in `backend/Services/Notifications/`, which already `SetNull` the actor FK (FR-023 pattern)
- [X] T048 [P] [US3] Audit roster and participant-list projections across teams, events and parties for null-profile handling now that a profile can be genuinely absent rather than merely filtered
- [X] T049 [US3] Verify the frontend renders the placeholder without layout breakage where an avatar is absent, in `frontend/apps/web/src/app/` shared member-display components
- [X] T050 [US3] Complete `specs/037-account-deletion/checklists/ui-review.md` against the diff; DESIGN.md wins on any conflict (constitution Gate 7)

**Checkpoint**: All three functional stories work. The feature is deployable.

---

## Phase 6: User Story 4 — The privacy policy stops describing a manual route (Priority: P4)

**Goal**: The rights section points at the control instead of the contact address.

**Independent Test**: Read the rights section in all three languages; each describes the in-product control, keeps the manual route as a fallback, and the three agree on substance.

**⚠️ Do not start before US1–US3 are live.** Shipping this early makes the policy describe a control that does not exist — the exact failure feature 036 refused to commit.

- [X] T051 [US4] Update the rights section in the **German** catalogue under `frontend/apps/web/public/i18n/legal/de.json` — German is authoritative (feature 036)
- [X] T052 [P] [US4] Update `frontend/apps/web/public/i18n/legal/en.json` to match in substance
- [X] T053 [P] [US4] Update `frontend/apps/web/public/i18n/legal/es.json` to match in substance
- [X] T054 [US4] State in all three that erasure is immediate, that authored messages and posts are retained under a neutral author, and that a former address may be used to register again (FR-044) — phrased as durable commitments organised by category of data, not as feature-shaped negatives. Use the vocabulary settled in T064 so the policy does not reintroduce the erasure/ban word collision FR-030 forbids
- [X] T067 [US4] Carry FR-022's retention statement into the policy in all three languages: the moderation log is retained on legitimate interest, principally as a record of **administrator** conduct, for as long as the platform operates a moderation function. State the **criterion**, not an invented fixed period — nothing in the platform enforces one, and 036's rule is that the policy must never describe a behaviour the product does not have
- [X] T055 [US4] Keep the manual contact route as a documented fallback rather than removing it (FR-043)
- [X] T056 [US4] Confirm feature 036's identical-key-set Jest test still passes across `en`/`de`/`es` — a missing `de` key would render English inside the legally binding German document

**Checkpoint**: The policy describes what the product actually does.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T057 [P] Add frontend specs for the danger-zone component and confirmation dialog beside them in `frontend/apps/web/src/app/features/account/`
- [X] T058 [P] Add a Playwright e2e covering the full deletion journey in `frontend/apps/web-e2e/`
- [X] T059 Verify resilience logs around the erasure carry the operation but **no** personal data, message content or request bodies (Principle VII)
- [X] T060 Confirm no endpoint anywhere accepts an account identifier for deletion, and that the 401/403/409 bodies disclose nothing (Principle I)
- [X] T061 Run the full [quickstart.md](quickstart.md) — all seven scenarios including 6a/6b
- [X] T062 Run backend, frontend and e2e suites; confirm existing chat, admin and auth suites are unaffected by T006–T015
- [X] T063 Update GitHub issue [#105](https://github.com/jnroesch/juggerhub/issues/105) — close the deletion half, confirm export (Art. 15/20) remains tracked

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: blocks **all** user stories. T012 is the gate; **T064 blocks every task that writes member-facing text** (T032, T041, T054)
- **US1 (Phase 3)**: after Phase 2
- **US2 (Phase 4)**: after Phase 2; shares the deletion service with US1, so in practice follows it
- **US3 (Phase 5)**: after Phase 2; independent of US2
- **US4 (Phase 6)**: after US1–US3 are **live**, not merely merged
- **Polish (Phase 7)**: after all desired stories

> **Note on task IDs.** T064–T066 were added by `/speckit-analyze` remediation and are placed in their correct execution phase, so IDs are not strictly ascending in file order. Execute by phase position, not by number.

### Within User Story 1

T016–T019 (tests, parallel) → T020 (interface) → T021–T026 (service, sequential, same file) → T027–T028 (email) → T029–T030 (endpoint) → T031–T032 (frontend)

### Parallel Opportunities

- T002, T003 in Setup
- T011, T015 in Foundational
- T016–T019 and T066: all five US1 test files
- T033, T034, T065 in US2
- T043, T044 then T046, T047, T048 in US3
- T052, T053 in US4 (after the authoritative German T051)
- T057, T058 in Polish
- **US2 and US3 can run in parallel** once US1's service exists

---

## Parallel Example: User Story 1 tests

```bash
Task: "AccountErasureTests.cs — disposition inventory"
Task: "ErasureAtomicityTests.cs — rollback and repeat-safety"
Task: "ReRegistrationTests.cs — ban bars, deletion permits"
Task: "ArchivedConversationTests.cs — frozen Conversation.Name"
Task: "ReRegistrationTests.cs — enumeration-oracle equivalence (T066)"
```

---

## Implementation Strategy

### MVP scope

**Phase 2 + Phase 3 (US1)** is the smallest thing that delivers the feature's point. But note the honest caveat at the US1 checkpoint: without US3, observer-side rendering on non-chat surfaces is unverified. **US1 is the MVP; US1+US2+US3 is the shippable set.**

### Incremental delivery

1. Phase 1 + Phase 2 → `Deleted` exists and fails closed (nothing user-visible)
2. + US1 → members can erase their accounts (MVP)
3. + US2 → the flow is honest and refuses safely
4. + US3 → everyone else's screens are verified → **deployable**
5. + US4 → the policy matches reality

### Sequencing risk

Phase 2 looks like refactoring and will be tempting to skip or fold into US1. Do not. Three of the seven predicates fail open, and they fail open *silently* — the code compiles, the tests pass, and a deleted account stays contactable.

---

## Implementation status — 2026-08-01

**67 of 67 complete.**

```
642 backend integration tests   0 failed
336 frontend tests              0 failed
 78 e2e tests (desktop+mobile)  0 failed
```

27 backend + 6 frontend + 6 e2e tests added.

### The e2e found a real bug the other suites structurally could not

`POST /account/deletion` returns **401 for a wrong password** (it re-authenticates). The auth
interceptor globally treats 401 as *session expired* — so it silently refreshed a perfectly valid
session, retried the delete with the same wrong password, and eventually signed the member out.
**Mistyping your password while deleting your account logged you out instead of telling you.**

Backend tests could not see it (the server was correct), and the component spec could not see it
(the interceptor is not in the unit TestBed). It only exists where a real 401 meets a real
interceptor. Fixed in `auth.interceptor.ts` by matching on **method + path**, not path alone —
`GET /account/deletion-preview` shares the prefix and its 401 really does mean the session expired.

### Export half

Tracked in **#110** (Art. 15 / Art. 20), with the one decision it inherits written down: chat is
shared data, and 037's erasure answer does not automatically give the export answer. #105 has a
comment pointing at both.

### Corrections made to the design documents during implementation

Three planning-phase claims turned out to be wrong and are corrected in place rather than left to
mislead the next reader:

1. **Feature 035 was recorded as unmerged.** It is merged (`b801df9`). `ProfileAvatar` is a
   descriptor, so the cascade orphans the blob — FR-015 needed a real `IMediaStore.DeleteAsync`
   call, not the no-op seam the plan assumed. (research R8, plan risk 5, T026)
2. **The archived `Conversation.Name` "trap" does not exist.** The freeze writes team/event names
   and literals, never a person's name. T024 became a verification instead of a scrubbing pass.
   (data-model §3, T019, T024)
3. **`Notification.Actor`'s `SetNull` never fires.** It fires on row *delete*, and erasure never
   deletes the account row. The reference survives — correctly, exactly like `ChatMessage.SenderId`
   — but the plan described it as self-nulling. (data-model §2)

## Notes

- [P] = different files, no dependencies
- Verify tests fail before implementing (T012 especially — it must be red first, or it is proving nothing)
- Commit after each task or logical group; reference `#105` in messages
- The three owner decisions (immediate, retain-verbatim, email-freed) are recorded in [spec.md](spec.md) *Clarifications* — do not re-litigate them mid-implementation
