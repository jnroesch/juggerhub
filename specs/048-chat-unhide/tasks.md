---
description: "Task list for feature 048 — Hiding a Chat Is Reversible"
---

# Tasks: Hiding a Chat Is Reversible

**Input**: Design documents from `/specs/048-chat-unhide/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/chat-hide-state.md](./contracts/chat-hide-state.md)

**Tests**: Included. The contract names ten obligations (C1–C10) and the codebase's chat
suites are the established way such facts are pinned — three of them (C5 ordering, C7 system
lines, C4 sender) guard behaviours that are invisible in the diff and would rot silently.

**Organization**: grouped by user story so each can be implemented and verified independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable — different file, no dependency on an incomplete task
- **[Story]**: US1 (auto-return, P1) or US2 (explicit toggle, P2)

## ⚠ Bounds — check every task against these

**NO** entity · **NO** column · **NO** migration · **NO** endpoint · **NO** DTO field ·
**NO** realtime event · **NO** dependency · **NO** hidden-chats inbox surface.
If a task below seems to need one, stop and re-read [research.md](./research.md) R1/R2/R5/R12.

---

## Phase 1: Setup

**Purpose**: nothing to install or scaffold — this feature edits files that already exist.

- [X] T001 Confirm the working tree is on branch `048-chat-unhide` and `docker compose up -d` brings up postgres + redis (chat fails closed without redis), then run the baseline suites green before changing anything: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Chat"` and `cd frontend; npx nx test web --watch=false --testPathPattern="chat|catalog-parity"`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the one shared string every later task reads. Nothing else blocks.

- [X] T002 Add `chat.details.unhide` to **all three** catalogues in one change — `frontend/apps/web/public/i18n/en.json`, `de.json`, `es.json` — as a sibling of the existing `chat.details.hide` (en L528 / de L532 / es L532). Wording follows spec FR-015: hide is an *archive*, so the counterpart reads "show in my messages again", never "unhide" or "restore". Suggested: en `"Show in my messages again"`, de `"Wieder in meinen Nachrichten anzeigen"`, es `"Mostrar de nuevo en mis mensajes"`. Leave the existing `hide` strings untouched. `catalog-parity.spec.ts` is red until all three land (research R8)

**Checkpoint**: `npx nx test web --watch=false --testPathPattern="catalog-parity"` green.

---

## Phase 3: User Story 1 — An archived conversation comes back on its own (P1) 🎯 MVP

**Goal**: a member message returns the conversation to the inbox of everyone who archived it,
sender included, with a correct badge — the half that makes a hidden **team** chat recoverable
at all, which is the bug reported in #222.

**Independent test**: hide a team chat as A, send to it as B, confirm it is back in A's inbox
with an accurate unread badge and no refresh. Shipped alone this resolves the issue.

### Tests for User Story 1

- [X] T003 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatHideTests.cs` following the shape of the neighbouring suites (`ChatTestSupport` fixtures, `ChatMessageSeed` helpers, `FakeChatRealtime` for pushes), covering **C4** (a member message returns the conversation to every hidden recipient's inbox **and** the sender's), **C6** (hidden **and** muted → returns to the inbox, still absent from the unread total) and **C8** (one member hiding does not alter another's `isHidden`)
- [X] T004 [P] [US1] Add **C5** to `ChatHideTests.cs`: after a message returns a hidden conversation, the recipient's nav unread total **already includes it** — assert against the total captured from `FakeChatRealtime`'s `PushUnreadCountAsync`, not a later re-query. This is the test that fails if the clear is placed after `PushMessageToOthersAsync` (research R3, spec FR-009)
- [X] T005 [P] [US1] Add **C7** to `ChatHideTests.cs`: a system line leaves a hidden conversation hidden. Drive it through a real roster change on a hidden team/group conversation so the assertion breaks if a future change teaches `WriteSystemMessageAsync` to clear the flag (research R4, spec FR-011)
- [X] T006 [P] [US1] Add the group-leaver fact to `ChatHideTests.cs`: a member who hid a **group** and then left it keeps `IsHidden = true` when a later message arrives — the assertion that pins the `p.LeftDate == null` clause (data-model D2, spec Edge Cases)

### Implementation for User Story 1

- [X] T007 [US1] In `backend/Services/Chat/ChatMessageService.cs` `SendAsync`, insert the auto-clear **between** `await _db.SaveChangesAsync(ct);` (~L131) and the `ProjectOneAsync`/`PushMessageToOthersAsync` calls — the exact statement and its comment are given verbatim in [plan.md](./plan.md) ("Backend — one statement"). All four parts are load-bearing: `&& p.IsHidden` (narrow, matches zero rows normally), `&& p.LeftDate == null` (D2), `SetProperty(p => p.ModifiedDate, DateTime.UtcNow)` (**`ExecuteUpdateAsync` bypasses the change tracker — constitution Principle III, the likeliest gate failure in this diff**), and its **position** before the push (FR-009)
- [X] T008 [US1] Verify by *not* editing: `WriteSystemMessageAsync` (~L538) gains nothing, and the auto-clear is **not** extracted into a helper the two methods share. Keep the comment written in T007 explaining why — it is the only thing standing between a future refactor and a silent FR-011 regression (research R4)

**Checkpoint**: `dotnet test … --filter "FullyQualifiedName~IntegrationTests.Chat"` green; quickstart Scenario 1 (incl. 1a, 1b, 1c) walks correctly in the browser with **no frontend change yet** — the inbox re-seed already handles it (research R5).

---

## Phase 4: User Story 2 — Put a conversation back myself (P2)

**Goal**: the details panel's Hide becomes a two-state toggle, mirroring mute/unmute.

**Independent test**: open a hidden conversation by its `/chat/{id}` link, use the control,
confirm it is in the inbox immediately without a page reload.

### Tests for User Story 2

- [X] T009 [P] [US2] Add **C1**, **C2** and **C3** to `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatHideTests.cs`: `{"isHidden": false}` un-hides and returns `204`; un-hiding something never hidden is a `204` no-op; and patching one flag never disturbs the other across all four hide/mute combinations (spec FR-005, I2). These pin behaviour that already exists (research R1) so the toggle cannot regress it
- [X] T010 [P] [US2] Add un-hide facts to `frontend/apps/web/src/app/core/services/chat.service.spec.ts` next to the existing `setState('c1', { isHidden: true })` case (~L294): `{ isHidden: false }` triggers an inbox re-seed; `{ isHidden: true }` still drops the row; and a patch carrying **both** flags applies both. Written against the current code these fail — that is the point (research R6)
- [X] T011 [P] [US2] Add toggle facts to `frontend/apps/web/src/app/features/chat/chat-details/chat-details.component.spec.ts` (its fixture already sets `isHidden: false`, ~L28): the label key flips with `detail().isHidden`; un-hiding updates the local `detail` signal and does **not** navigate; hiding still navigates to `/chat` (spec FR-004)

### Implementation for User Story 2

- [X] T012 [US2] Fix `setState` in `frontend/apps/web/src/app/core/services/chat.service.ts` (~L203-212): replace the truthiness test with explicit `patch.isHidden === true` / `=== false` and make the two flags **independent `if`s** rather than `if/else if`. On `false`, call `this.loadInbox().subscribe({ error: () => undefined })` — reusing the re-seed rather than inventing a client-side row insert, so the server's inbox projection stays the single place a row's shape is decided (research R6)
- [X] T013 [US2] In `frontend/apps/web/src/app/features/chat/chat-details/chat-details.component.ts`, replace `hide()` (~L92-104) with `toggleHide()` modelled on `toggleMute()` (~L124-140), keeping the one asymmetry FR-004 requires: **hide** navigates to `/chat` as today; **un-hide** updates the local `detail` signal via `this.detail.update(...)` and stays on the conversation
- [X] T014 [US2] In `frontend/apps/web/src/app/features/chat/chat-details/chat-details.component.html` (~L128-137), bind the label to `(d.isHidden ? 'chat.details.unhide' : 'chat.details.hide') | transloco`, point the click at `toggleHide()`, and rename `data-testid="hide-chat"` → `toggle-hide` to match the neighbouring `toggle-mute` (verified safe: the old id appears in no test — research R7). Give the un-hidden state an icon that reads as *show* rather than leaving the crossed-out eye in both states; keep every layout, spacing and focus class identical to the mute row

**Checkpoint**: frontend chat suites green; quickstart Scenario 2 (incl. 2a) walks correctly.

---

## Phase 5: Spec amendments (019)

**Purpose**: the behaviour is specified rather than implied — spec FR-013, FR-014, research R9.

- [X] T015 [P] Add an `> **Amended by feature 048 (2026-09-09) — hiding a conversation is reversible.**` callout to the **Amendments** section of `specs/019-chat/spec.md`, after the 046 callout (~L21), in the same shape the 022 and 046 callouts use: hide is an archive, mute is the leave substitute, a member message returns an archived conversation, system lines do not, option 3 out of scope
- [X] T016 [P] Amend `specs/019-chat/spec.md` **FR-026** (L287) so **mute** is the control offered in place of leave — the owner rejected hide's justification there (leaving the team leaves the chat, FR-025) — and **FR-029** (L293) to state that hiding is reversible both by the player and automatically on a new member message. **Leave FR-018 (L272) untouched**: what "hidden" means to the badge is unchanged (research R9)
- [X] T017 [P] Add a pointer to `specs/019-chat/contracts/chat-api.md` noting that the conversation-state patch is now exercised in both directions and that a member message clears the hidden flag, referencing `specs/048-chat-unhide/contracts/chat-hide-state.md`

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T018 Instantiate the Gate 7 checklist: copy `.specify/templates/ui-review-checklist-template.md` to `specs/048-chat-unhide/checklists/ui-review.md` and complete it against the diff at **375px** and desktop, light and dark. Binding case is the German string **"Wieder in meinen Nachrichten anzeigen"** — no font shrink, no truncation; the toggle must match `toggle-mute` in height, padding, border and focus ring. DESIGN.md wins on any conflict; report conflicts rather than resolving them silently
- [X] T019 Walk quickstart **Scenario 3** (regression): a player who hid nothing sees an identical inbox, order and unread total (SC-006); a hidden conversation still opens by direct link with full history (FR-017, SC-005); a blocked-and-hidden DM does **not** return to the blocker's inbox (**C10**, spec Edge Cases); mute/unmute unchanged in label and behaviour
- [X] T020 Run the full verification set and record the results: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Chat"`, then in `frontend/` `npx nx test web --watch=false`, `npm run lint`, `npm run build`
- [X] T021 Review the diff against the bounds in [research.md](./research.md) R12: no migration, no new entity, no new endpoint or DTO field, no realtime event, no dependency, no hidden-chats surface, and **no resilience wrapping** (Principle VII is not engaged — research R10)
- [X] T022 File the follow-up issue for #222 **option 3** — filed as **#242** (a "Hidden chats" list or inbox filter), referencing spec FR-016 and noting it becomes worth building only if hidden chats are observed to accumulate

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → no dependencies
- **Phase 2 (Foundational)** → T002 blocks T014 (the template reads the key) and T011
- **Phase 3 (US1)** → depends only on Phase 1. **Independently shippable.**
- **Phase 4 (US2)** → depends on T002. **Does not depend on Phase 3** — the two mechanisms
  touch different files and either can land first
- **Phase 5 (Specs)** → documentation only; may run at any point after the plan
- **Phase 6 (Polish)** → after Phases 3 and 4

### User story dependencies

**US1 and US2 are independent.** US1 is backend-only (`ChatMessageService` + one test file);
US2 is frontend-only plus three API-level tests. They share no file. US1 alone is a coherent
release that fixes the reported bug; US2 alone gives an explicit control for anyone who can
reach the conversation.

### Within each user story

Tests (T003–T006, T009–T011) are written first and fail; implementation (T007–T008,
T012–T014) makes them pass.

### Parallel opportunities

- **T003, T004, T005, T006** all add facts to the same new file — write the file once (T003),
  then the rest are sequential edits to it. Marked `[P]` because they are independent *facts*,
  not independent files; do not run them as concurrent writers.
- **T009, T010, T011** are genuinely parallel — three different files.
- **T015, T016, T017** are parallel — two files, and T015/T016 touch different regions.
- **US1 (T003–T008) and US2 (T009–T014)** can proceed in parallel by two people.

---

## Implementation Strategy

### MVP: User Story 1 only

Phases 1 → 3. Backend-only, one statement plus tests. Resolves #222's central complaint — a
hidden team chat becomes recoverable — and needs **no frontend change at all**, because the
client already re-seeds its inbox for a conversation it does not know about (research R5).

### Incremental delivery

1. **T002** (i18n) — harmless alone, unblocks everything else
2. **Phase 3** (US1) — ship, verify Scenario 1
3. **Phase 4** (US2) — ship, verify Scenario 2
4. **Phases 5–6** — amendments, Gate 7, regression sweep

### Suggested commits

One per phase boundary, in the project's small-commit style: `feat(048): …` for T002, T007,
T012–T014; `test(048): …` for the test files; `docs(048): …` for the 019 amendments. Reference
`#222` in each.

---

## Notes

- **The likeliest defect in this diff is a missing `ModifiedDate`** in T007. It is silent —
  everything passes, the audit column just stops being true (constitution Principle III).
- **The second likeliest is ordering**: T007 placed after the push instead of before it. T004
  is the test that catches it; nothing else will.
- **Do not "fix" unread counting for system lines.** Whether a system line contributes to the
  unread total is 019 behaviour, unexamined here and explicitly out of scope (research R4).

---

## Verification status (2026-09-09)

**Automated — all green.**

| Suite | Result |
|-------|--------|
| Backend chat (`~IntegrationTests.Chat`) | **255 passed, 0 failed** (baseline 243, +12 new in `ChatHideTests`) |
| Frontend `web` (full) | **526 passed, 72 suites** (baseline 520, +6 new) |
| `npm run lint` | **0 errors**, 21 warnings — all pre-existing, in `web-e2e` Playwright specs, none in this diff |
| `npm run build` (production) | **clean**, 287.23 kB initial |

**T019 (regression)** is verified by test rather than by hand: FR-017/C9, the blocked-and-hidden
case/C10 and the hide↔mute independence/C3 are assertions in `ChatHideTests`, and "a player who
hid nothing sees an identical inbox" is covered by the 243 pre-existing chat tests still passing.

**Browser walk — done, both viewports.** Driven with Playwright against the docker stack
(rebuilt backend + rebuilt frontend), en-GB at 1280px and de-DE at 375px, covering quickstart
Scenarios 1, 1c, 2, 2a and 3. `checklists/ui-review.md` is complete: CHK034 passed (the German
label wraps to two lines, row grows 44px → 50px, nothing truncates) and **CHK035 was a defect
the walk found** — the wrapped label rendered centred because the panel header centres its text,
which a single-line span had been hiding. Fixed with `text-left` and re-shot.

**Two mistakes in the walk itself, both caught by reading the output rather than trusting it:**

- The first run signed the second player in **on the same browser context**, replacing the first
  player's session cookie — so the "returned" inbox was the sender's, proving nothing. Fixed with
  a separate context.
- The stack was serving a **stale backend image**: only the frontend assets had been replaced, so
  the auto-return could not work and the inbox legitimately stayed empty. `docker compose up -d
  --build backend` fixed it, and the live trace then showed `chatMessageCreated` followed by
  `chatUnreadCountChanged: 1` in the same push cycle — FR-009 observed, not just asserted.
