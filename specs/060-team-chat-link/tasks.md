# Tasks: The Team Page Leads Into the Team Chat

**Input**: Design documents from `specs/060-team-chat-link/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/team-chat-api.md](./contracts/team-chat-api.md), [quickstart.md](./quickstart.md)

**Tests**: included. Each behaviour gets a test written before the code that makes it pass, and the
defect's test must be seen to **fail** before the fix.

**Organization**: by user story. **US2 runs before US1** even though both are P1: US1's endpoint is
only correct once the team chat is found by kind (research R1), so the defect fix is its
prerequisite.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: US1 / US2 / US3 from spec.md

---

## Phase 1: Setup

- [X] T001 Record the baseline on the fresh branch: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~Chat"` and, in `frontend/`, `npx nx test web --watch=false --testPathPatterns="team-detail|chat.service|catalog-"`. Note any pre-existing failure so it is not later mistaken for a regression. *(Done: 347/347 chat integration tests, 77/77 frontend; no pre-existing failure.)*

---

## Phase 2: Foundational (blocking)

- [X] T002 [P] Add `public sealed record TeamChatRefDto(Guid ConversationId);` with an XML doc (060: the team chat's id, never null; a missing chat is a 404) next to `InquiryThreadRefDto` in `backend/Dtos/Chat/ChatDtos.cs`
- [X] T003 [P] Add `export interface TeamChatRef { readonly conversationId: string; }` with a doc comment next to `InquiryThreadRef` in `frontend/apps/web/src/app/core/models/chat.models.ts`, and export it wherever `InquiryThreadRef` is re-exported

**Checkpoint**: both sides compile.

---

## Phase 3: User Story 2 — A team's chat exists even when someone contacted its admins first (P1) 🎯 prerequisite of US1

**Goal**: a Contact-admins thread can never pass for the team chat (FR-013, FR-014, FR-015).

**Independent test**: a non-member contacts a team's admins before any member has opened Chat. A
member's inbox then lists a `kind = "Team"` row for the team, and the Contact-admins thread is
untouched.

- [X] T004 [US2] Create `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatTeamChatLinkTests.cs` (`[Collection("Chat")]`, derive from `ChatTestSupport`, mirror `ChatTeamPartyTests`' helpers). Add: **(a)** `A_contact_admins_thread_does_not_stop_the_team_chat_being_created`: J POSTs `/api/v1/chat/contact/team/{teamId}/messages` before any member has loaded the inbox, then the member's inbox contains a row with `kind == "Team"` and that `teamId`, and J's inquiry still exists with the same id and message; **(b)** `A_team_still_has_exactly_one_team_chat_when_an_inquiry_exists`: with an inquiry present, three concurrent inbox loads leave exactly one `Kind == Team` conversation for the team (count by `Kind`, not by `TeamId`). Run it and **confirm (a) fails** on the unfixed code.
- [X] T005 [US2] In `backend/Services/Chat/ChatConversationService.cs`, scope the team half of `EnsureAutoChatsForAsync` (`!_db.Conversations.Any(c => c.TeamId == id)` → `… c.Kind == ConversationKind.Team && c.TeamId == id`), and give the party half `c.Kind == ConversationKind.Party` for symmetry. Scope `FindAutoAsync`'s predicate by kind (`kind == Team ? c.Kind == Team && c.TeamId == ownerId : c.Kind == Party && c.PartyId == ownerId`). Add one short comment on `FindAutoAsync` saying why (027's `TeamInquiry` shares `TeamId`, and the index at `AppDbContext.cs:1041` is already kind-scoped). Run T004 (now green), then `--filter "FullyQualifiedName~ChatTeamParty|FullyQualifiedName~ChatInquiry|FullyQualifiedName~ChatArchive"`.

**Checkpoint**: commit `fix(060): a contact-admins thread no longer passes for the team chat (#362)`.

---

## Phase 4: User Story 1 — A member opens the team chat from the team page (P1) 🎯 MVP

**Goal**: one press on **Team chat** opens the team's own chat, creating it if needed (FR-001 to
FR-012).

**Independent test**: as a member, press Team chat and land in the conversation whose id equals the
inbox's team-chat row. The same holds for an admin of a team with a Contact-admins thread, and for
a never-opened chat.

### Backend

- [X] T006 [US1] Add endpoint tests to `ChatTeamChatLinkTests.cs`, all against `GET /api/v1/chat/team/{teamId}`:
  - member → 200, `conversationId` equals the inbox row with `kind == "Team"` for the team;
  - never-opened chat → 200, and exactly one `Kind == Team` row now exists, with **no** `Notifications` row, no `TestEmailSender` mail and no `FakePushDispatcher` dispatch (FR-003);
  - admin of a team with J's inquiry → the returned conversation's detail has `kind == "Team"` and is **not** J's inquiry id (FR-002, SC-002);
  - signed-in non-member → 404, and **no** `Kind == Team` row exists for that team afterwards (FR-007);
  - player with a pending join request → 404;
  - random GUID → 404, with a body whose `title`/`detail`/`status` equal the non-member's (FR-005, SC-003);
  - member who hid and muted the chat → 200, and their `ConversationParticipant` still has `IsHidden == true` and `IsMuted == true` (FR-012);
  - the 200 body's JSON object has exactly one property, `conversationId` (FR-006);
  - two concurrent calls from different members on a never-opened chat → the same id, one row (FR-015);
  - no auth → 401.
- [X] T007 [US1] Declare `Task<ChatResult<TeamChatRefDto>> OpenTeamChatAsync(Guid callerId, Guid teamId, CancellationToken ct = default);` in `backend/Services/Chat/IChatConversationService.cs`, with an XML doc stating the contract (member → id, creating like the inbox; anyone else → NotFound, indistinguishable).
- [X] T008 [US1] Implement `OpenTeamChatAsync` in `backend/Services/Chat/ChatConversationService.cs`, beside the auto-chat section, in exactly this order (research R2): `await EnsureAutoChatsForAsync(callerId, ct)` → `var id = await FindAutoAsync(ConversationKind.Team, teamId, ct)` → `if (id == Guid.Empty || await _guard.ResolveAsync(id, callerId, ct) is null) return ChatResult<TeamChatRefDto>.Fail(ChatOutcome.NotFound)` → `Ok(new TeamChatRefDto(id))`. Comment why: no `EnsureForTeamAsync(teamId)` (anyone could create any team's chat; an unknown id rethrows an FK violation), and no roster pre-check (ChatGuard is the one membership rule, FR-004).
- [X] T009 [US1] Add `[HttpGet("team/{teamId:guid}")] OpenTeamChat(Guid teamId, CancellationToken ct)` → `ActionResult<TeamChatRefDto>` to `backend/Controllers/ChatConversationsController.cs`, directly after the Contact-admins section, under a `// --- Team chat (feature 060) ---` divider. Thin: `TryGetUserId` → service → `Ok(result.Value)` or `Fail(result.Outcome, result.Error)`. Doc comment mirrors `FindTeamInquiry`'s.
- [X] T010 [US1] `dotnet build backend/JuggerHub.slnx` (**gate on its exit code**, never through a pipe), then run `--filter "FullyQualifiedName~ChatTeamChatLink"` until green.

### Frontend

- [X] T011 [P] [US1] Add `openTeamChat(teamId: string): Observable<TeamChatRef>` → `GET ${this.base}/team/${teamId}` next to `findTeamInquiry` in `frontend/apps/web/src/app/core/services/chat.service.ts`, and a URL/verb test in `frontend/apps/web/src/app/core/services/chat.service.spec.ts` in that file's existing style.
- [X] T012 [P] [US1] Add `teams.detail.teamChat`, `openingTeamChat`, `teamChatFailed`, `teamChatNotMember` with research R7's values to `frontend/apps/web/public/i18n/en.json`, `de.json` and `es.json`, all three in the same change.
- [X] T013 [US1] In `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.ts`: inject `ChatService`; add signals `teamChatBusy`, `teamChatError` and `teamChatNotice` (both translation keys or null); add `openTeamChat()`. It returns early when busy or there is no team, clears both keys, sets busy, then calls `chat.openTeamChat(team.id)`. On success → `router.navigate(['/chat', ref.conversationId])`. On 404 → busy false, `teamChatNotice.set('teams.detail.teamChatNotMember')`, `load()`. On any other error → busy false, `teamChatError.set('teams.detail.teamChatFailed')`. Branch on `HttpErrorResponse.status`, never `detail`. Reset `teamChatNotice`/`teamChatError` in the `paramMap` subscription beside `joinNotice`.
- [X] T014 [US1] In `team-detail.component.html`, add the Team chat button as the **first** item of the `team-tools` card's list: `<button type="button" jhButton variant="secondary" size="sm" data-testid="team-chat" [disabled]="teamChatBusy()" (click)="openTeamChat()">`, with its label switching to `openingTeamChat` while busy (the `postNews` idiom). Below the list, add `@if (teamChatError(); as key) { <p class="mt-xs text-body-sm text-danger-fg" role="alert" data-testid="team-chat-error">…</p> }`, matching the card's scale. Add the page-level notice `@if (teamChatNotice(); as key) { <jh-alert tone="info" class="mt-sm" data-testid="team-chat-notice">…</jh-alert> }` next to `requestError`, **outside** every member-only block.
- [X] T015 [US1] Add tests to `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.spec.ts` (stub `ChatService.openTeamChat`; use the spec's existing fixtures and helpers):
  - admin and plain member each see `team-chat` inside `team-tools`; a non-member, a `Requested` viewer and an anonymous viewer do not;
  - loading the page does **not** call `openTeamChat` (SC-004);
  - press → `openTeamChat` is called with `team.id`, and the router navigates to `['/chat', id]`;
  - while the request is pending, the button is disabled and shows the opening label;
  - 404 → `team-chat-notice` is shown and `getPublicDetail` is called again;
  - 500 → `team-chat-error` is shown in the card and the button is enabled again.

**Checkpoint**: commit `feat(060): members open the team chat from the team page (#362)`.

---

## Phase 5: User Story 3 — A member's actions sit together in one card (P2)

**Goal**: members see no header actions; everything a member can do is in the `team-tools` card
(FR-016 to FR-019). Non-members are unchanged.

**Independent test**: view the page as an admin, a plain member, a non-member, a pending requester and
a signed-out visitor, and check where each action renders.

- [X] T016 [US3] Add placement tests to `team-detail.component.spec.ts`:
  - plain member: `contact-admins` is **inside** `team-tools` (after `team-chat`), and the header has no button or link;
  - admin: no `contact-admins` anywhere, and the header has no action;
  - non-member: `contact-admins` and `request-to-join` are in the header, and there is no `team-tools`;
  - `Requested`: `requested` and `cancel-request` are in the header;
  - pressing the card's `contact-admins` navigates to `['/chat','contact','team', team.id]` with `state: { name }`, as before (FR-018).

  Run them and confirm the plain-member case fails.
- [X] T017 [US3] In `team-detail.component.html`, wrap the header's action block (`<div class="flex w-full shrink-0 flex-col gap-sm …">`) in `@if (!isMember())`. Keep a one-line comment saying why: members' actions live in the card (owner decision, 060), and this also drops the empty row an admin's header used to get. In the `team-tools` card, add `@if (canContactAdmins()) { <button type="button" jhButton variant="secondary" size="sm" data-testid="contact-admins" (click)="contactAdmins()">… }` right after Team chat; inside the member block that condition means plain members only. Update the card's comment to list its four actions and who sees each. Run the whole team-detail spec: the 057/058/059 tests pass **unedited**.

**Checkpoint**: commit `feat(060): a member's team actions sit together in one card (#362)`.

---

## Phase 6: Polish & cross-cutting

- [X] T018 [P] Add an "**Amended by feature 060 (team chat link).**" paragraph to the amendment list at the top of `specs/019-chat/contracts/chat-api.md`. It covers the new `GET /chat/team/{teamId}`, and the inbox now creating a team's chat even when a Contact-admins thread exists. Link `../../060-team-chat-link/contracts/team-chat-api.md`.
- [X] T019 Gate 7: copy `.specify/templates/ui-review-checklist-template.md` to `specs/060-team-chat-link/checklists/ui-review.md` and answer every item against the diff and the walk screenshots (T021). DESIGN.md wins any conflict. Record conflicts; do not silently resolve them.
- [X] T020 Full verification: `dotnet build backend/JuggerHub.slnx` (exit code) → `dotnet test backend/JuggerHub.slnx`; in `frontend/`: `npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`. Report any failure with its output. Never call a skipped check "passed". *(Done: build 0 warnings; backend 1281/1281; frontend 993/993, lint clean, build clean.)*
- [X] T021 Browser walk (owner's standing rule): `docker compose up -d --build backend frontend`, then a throwaway Playwright script inside `frontend/` (one context per actor, `locale: 'de-DE'`) that drives quickstart scenarios 1–9 at **375px and desktop**. Screenshot the member card (admin and plain member), the non-member header, the busy state if catchable, the not-a-member notice, and the landed chat. Read the driver's output and **look at** each screenshot, then delete the script. *(Done: 25/25 scripted checks plus the forced-500 failure state, with 15 screenshots read. The first run showed 4 FAILs that were a **harness** bug: a bare `header` selector also matched the app shell's top bar. The selector was scoped to `[data-testid="team-detail"] header` and re-run clean. Both scripts deleted.)*
- [X] T022 Commit the walk fixes and the UI review, push `060-team-chat-link`, and open a PR that **`Closes #362`**, summarising the defect fix, the owner decisions and the verification run. Update the auto-memory with this feature's decisions and traps.

---

## Dependencies & execution order

- Setup (T001) → Foundational (T002, T003) → **US2** (T004 → T005) → **US1** backend (T006 → T007 → T008 → T009 → T010) and US1 frontend (T011 ∥ T012 → T013 → T014 → T015) → **US3** (T016 → T017) → Polish (T018 ∥ T019 prep; T020 → T021 → T019 finish → T022).
- US1's frontend can start once T003 is done. Only its end-to-end correctness waits on US2.
- US3 touches the same template as US1's T014, so it runs **after** T014, never alongside it.

## Parallel opportunities

- T002 ∥ T003 (backend DTO / frontend model).
- T011 ∥ T012 (service / catalogues), and both alongside the backend T006–T010.
- T018 at any time after T009.

## Implementation strategy

1. **US2 first**: it is small, it is a live defect on its own, and US1 depends on it.
2. **MVP = US1**: the button, in the card members already have (the Manage card exists today).
3. **US3** then applies the owner's placement rule to the rest of the page.
4. Verify with automated suites, then the browser walk, then Gate 7, then the PR.
