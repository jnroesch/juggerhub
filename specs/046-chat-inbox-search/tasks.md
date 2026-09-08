# Tasks: Chat Inbox Search by People and Conversation Names

**Input**: Design documents from `/specs/046-chat-inbox-search/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/chat-inbox-search-api.md](./contracts/chat-inbox-search-api.md),
[quickstart.md](./quickstart.md)

**Tests**: Included. The spec's Independent Tests and SC-002/003/004 demand verification "by
direct request", every chat feature in this repository ships its integration tests, and the
removal in US2 needs a test that proves absence. Backend tests are written **before** the code
they cover and must fail first.

**Organization**: Tasks are grouped by user story. The backend predicate is shared by US1 and
US3 and is therefore foundational; US3's phase proves it for conversation names.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4 from spec.md)
- Every task names its exact file path(s)

## Path Conventions

Web application: `backend/` (.NET) and `frontend/apps/web/` (Nx/Angular). Integration tests
in `backend/tests/JuggerHub.Api.IntegrationTests/Chat/`. All paths below are repository-relative;
this feature lives in the worktree `juggerhub.worktrees/046-chat-inbox-search` on branch
`046-chat-inbox-search`.

> **Reminders that apply to every task**: no entity, no migration, no new endpoint, no new
> dependency (plan). If a task would produce an `Add-Migration`, stop — something is wrong.
> `/chat/search`'s **people** half is shared by `chat-new`, `chat-compose` and
> `profile-quick-actions` — never narrow it (FR-012). Principle VII is **not** engaged: no
> retry, timeout or breaker belongs in this diff.

---

## Phase 1: Setup

**Purpose**: a fresh worktree has no toolchain state; make both halves build before touching code.

- [ ] T001 Install frontend dependencies in the worktree: `cd frontend && npm ci` (the worktree
      hook seeds `.env` but not `node_modules`; a stale or missing tree fakes failures — see the
      Angular 22 lesson in memory). Confirm `npx nx test web --watch=false --testPathPattern=chat-inbox` is green before any change.
- [ ] T002 [P] Confirm the backend builds in the worktree: `dotnet build backend/JuggerHub.Api.csproj`
      (or the solution at the repo root). Confirm the Chat collection is green at baseline:
      `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Chat.ChatSearchTests"`.

---

## Phase 2: Foundational (the predicate and the `q` plumbing)

**Purpose**: the one query change every story reads through. Blocks US1 and US3; US2 and US4
do not depend on it but share files with it (`ChatDtos.cs`, the controller), so it lands first.

**⚠️ CRITICAL**: keep the inbox query byte-identical when `q` is absent or shorter than two
characters (SC-007). The predicate is *appended* to `VisibleConversations(callerId)`; nothing
else in `GetInboxAsync` moves.

- [ ] T003 Add `ChatGuard.MatchesName(AppDbContext db, Guid callerId, string pattern)` in
      `backend/Services/Chat/ChatGuard.cs`, directly below `IsMemberOf`, returning
      `Expression<Func<Conversation, bool>>`. One expression, two halves (EF cannot compose
      separately built lambdas): **members** — per-kind branches mirroring `IsMemberOf` exactly
      (Archived/Direct/Group via `c.Participants` with `LeftDate == null`; Team via
      `db.TeamMemberships` on `c.TeamId`; Party via `db.PartyMembers` on `c.PartyId` with
      `Status == In`; TeamInquiry via `c.RequesterUserId` or `db.TeamMemberships` with
      `Role == Admin`; EventInquiry via `c.RequesterUserId` or `db.EventAdmins`), each member
      `!= callerId` and tested with
      `db.PlayerProfiles.Any(pp => pp.UserId == <id> && (EF.Functions.ILike(AppDbContext.Unaccent(pp.DisplayName), AppDbContext.Unaccent(pattern)) || EF.Functions.ILike(pp.Handle, pattern)))`;
      **names** — `ILike(Unaccent(c.Name))`, `ILike(Unaccent(c.Team!.Name))` for Team and
      TeamInquiry, `ILike(Unaccent(c.Event!.Name))` for EventInquiry. XML-doc it with the
      data-model invariants I3/I4/I6/I7 and the "add a new kind here AND in IsMemberOf" warning.
- [ ] T004 Extend `GetInboxAsync` in `backend/Services/Chat/IChatConversationService.cs` and
      `backend/Services/Chat/ChatConversationService.cs` with an optional `string? query = null`
      parameter (existing internal call sites need no change). Trim it; when its length is
      `>= ChatConstants.MinSearchTermLength`, build `pattern = $"%{trimmed}%"` and apply
      `.Where(ChatGuard.MatchesName(_db, callerId, pattern))` to `VisibleConversations(callerId)`
      **before** the existing `CountAsync` / `OrderByDescending` / `Skip` / `Take` / projection.
      Otherwise leave the query untouched. Document the ≥2 rule with a pointer to research §1.
- [ ] T005 Pass the term through in `backend/Controllers/ChatConversationsController.cs`:
      `Inbox([FromQuery] PaginationRequest pagination, [FromQuery] string? q, CancellationToken ct)`
      → `_conversations.GetInboxAsync(userId, pagination, q, ct)`. No validation beyond the
      service's own rule; a short term is not an error (contract).

**Checkpoint**: `GET /api/v1/chat/conversations?q=xyz` compiles and returns the filtered inbox;
without `q` the response is unchanged.

---

## Phase 3: User Story 1 — Find a conversation by a person's name (Priority: P1) 🎯 MVP

**Goal**: typing part of a member's name narrows the inbox to the conversations that person is in,
rendered as the ordinary inbox rows, in inbox order; clearing restores the inbox.

**Independent Test**: seed Ada with a DM with Lena, a group containing Lena, a team chat whose
roster includes Lena, and conversations without her; `?q=len` lists exactly the three, and the
inbox UI shows them as rows that open on tap.

### Tests for User Story 1 (write first, watch them fail)

- [ ] T006 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatInboxSearchTests.cs`
      (`[Collection("Chat")]`, extends `ChatTestSupport`; add a helper that sets a user's
      `DisplayName` through a scoped `AppDbContext`, following `AddTeamMemberAsync`) with these
      facts against `GET /api/v1/chat/conversations?q=…`:
      `Finds_the_dm_group_and_team_chat_a_member_is_in` (SC-001: Direct via `StartDirectAsync`,
      Group via `POST /chat/conversations` with a name, Team via `CreateTeamAsync` +
      `AddTeamMemberAsync` — the team chat materialises on the first inbox call);
      `A_conversation_without_a_matching_member_is_not_listed`;
      `Matches_on_handle_as_well_as_display_name`;
      `Finds_a_conversation_beyond_the_first_inbox_page` (SC-004: 25 DMs, search the oldest
      partner's name, default page);
      `No_term_returns_the_identical_inbox` (SC-007: compare item ids with and without `q=`);
      `A_one_character_term_returns_the_plain_inbox`;
      `A_hidden_conversation_stays_hidden` (PATCH the state `isHidden: true` first);
      `A_blocked_dm_stays_out` (block, then search the partner's name; unblock after);
      `A_former_group_member_no_longer_matches` (member leaves via `DELETE …/members/me`);
      `Your_own_name_does_not_list_every_conversation`;
      `Matching_is_accent_and_case_insensitive` (display name "Jörg", terms `jorg` and `JÖRG`);
      `A_name_only_in_someone_elses_conversation_returns_nothing_and_no_count` (SC-003).
- [ ] T007 [P] [US1] Add to `frontend/apps/web/src/app/core/services/chat.service.spec.ts`:
      `searchInbox` issues `GET /api/v1/chat/conversations?q=len&skip=0&take=20` and returns the
      page **without changing `conversations()`** (seed the signal via a prior `loadInbox` flush
      and assert it is untouched after the search flush).
- [ ] T008 [P] [US1] Add to `frontend/apps/web/src/app/features/chat/chat-inbox/chat-inbox.component.spec.ts`
      (extend the existing `chat` double with `searchInbox: jest.fn()`): typing a two-character
      term (after the 250 ms debounce — use `jest.useFakeTimers()`) renders the returned
      conversations as `[data-testid="conversation-<id>"]` rows inside
      `[data-testid="conversation-list"]`; an empty page renders `[data-testid="search-empty"]`
      containing the term; clearing the term shows `conversations()` again; a one-character term
      never calls `searchInbox`; while a search is in flight a `[role="status"]` line is present;
      no element contains the old "In your messages" / "People" headings.

### Implementation for User Story 1

- [ ] T009 [US1] Add `searchInbox(term: string, take = 20): Observable<PagedResult<Conversation>>`
      to `frontend/apps/web/src/app/core/services/chat.service.ts` next to `loadInbox`, calling
      `${this.base}/conversations` with `q`, `skip=0`, `take` — **no `tap` into `_conversations`**
      (research §4). Leave `loadInbox` and `search` as they are for now (US2 trims `search`).
- [ ] T010 [US1] Rewire `frontend/apps/web/src/app/features/chat/chat-inbox/chat-inbox.component.ts`:
      `results = signal<Conversation[] | null>(null)`; `displayed = computed(() => this.isSearching() ? (this.results() ?? []) : this.conversations())`;
      `runSearch` calls `chat.searchInbox(value)` and stores `page.items`; drop `chatWith`, the
      `ChatSearchResult` import and the `Router` injection if nothing else uses it; keep the 250 ms
      debounce and the two-character rule; update the class doc comment (it still says "finds both
      messages and people").
- [ ] T011 [US1] Rewrite the results region of
      `frontend/apps/web/src/app/features/chat/chat-inbox/chat-inbox.component.html`: remove the
      "In your messages" and "People" sections and the `chatWith` button; render **one** `@for`
      over `displayed()` using the existing row markup (keep `data-testid="conversation-list"` and
      `conversation-<id>`); while searching show one muted `body-sm` line with `role="status"`
      (DESIGN.md loading rule — no spinner, no skeleton, no layout shift); keep the
      `[data-testid="search-empty"]` state with `chat.inbox.nothingMatched`; the normal empty
      state (`chat-empty`) must not appear while a term is active.
- [ ] T012 [US1] Update the three catalogues together — `frontend/apps/web/public/i18n/en.json`,
      `de.json`, `es.json` — under `chat.inbox`: `searchSr` → "Search your chats by name" / "Deine
      Chats nach Namen durchsuchen" / "Buscar en tus chats por nombre"; `searchPlaceholder` →
      "Find a chat by name…" / "Chat nach Name finden…" / "Buscar un chat por nombre…"; **delete**
      `inYourMessages`, `people` and `chat` from all three (used nowhere else — verified). Run
      `npx nx test web --watch=false --testPathPattern=catalog-parity`.
- [ ] T013 [US1] Run T006–T008 and the Phase 2 code together:
      `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~ChatInboxSearchTests"`
      and `npx nx test web --watch=false --testPathPattern="chat.service|chat-inbox"`. Fix until green.

**Checkpoint**: US1 is demonstrable — quickstart Scenario A passes end to end.

---

## Phase 4: User Story 2 — Message text is never searched (Priority: P1)

**Goal**: message-text search is gone from the interface and from the API; the people half of
`/chat/search` is untouched.

**Independent Test**: a term that appears only in message text returns no conversation from
`?q=`; `GET /chat/search` returns a `people` object and **no `messages` property**.

### Tests for User Story 2 (write first)

- [ ] T014 [P] [US2] Add `Message_text_never_matches` to
      `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatInboxSearchTests.cs` (SC-002: send a
      message containing a unique token to a DM whose partner's name does not contain it; `?q=<token>`
      returns `items: []`, `totalCount: 0`).
- [ ] T015 [P] [US2] Rework `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatSearchTests.cs`:
      **delete** `Finds_a_message_in_your_own_conversation`,
      `A_term_only_in_someone_elses_conversation_returns_nothing`,
      `Leaving_a_group_removes_its_messages_from_your_search`, `A_deleted_message_never_matches`;
      **rewrite** `Search_results_are_paginated` against people (five users whose display names
      share a unique token; `take=2` ⇒ 2 items, `totalCount` 5) and `Search_is_accent_insensitive`
      against a display name containing "Köln" searched as "Koln"; **extend**
      `A_short_or_empty_term_returns_an_empty_result_not_an_error` to assert `people` only;
      **add** `The_response_carries_no_messages_property` (FR-010:
      `Assert.False(results.TryGetProperty("messages", out _))`). Update the class remarks
      (they cite SC-006, which 046 supersedes).

### Implementation for User Story 2

- [ ] T016 [US2] In `backend/Dtos/Chat/ChatDtos.cs` delete `MessageSearchHitDto` and change
      `ChatSearchResultDto` to `(PagedResult<PersonHitDto> People)`; refresh its summary comment.
- [ ] T017 [US2] In `backend/Services/Chat/ChatSearchService.cs` delete `SearchMessagesAsync`
      and the `messages` half of `SearchAsync`/`Empty`; rewrite the class remarks (the scope-predicate
      paragraph is about message results and no longer applies — say what the service is now: people
      to start a chat with, open reach, self and blocks excluded). Update the summary in
      `backend/Services/Chat/IChatSearchService.cs`. Refresh the controller's `Search` doc comment
      in `backend/Controllers/ChatConversationsController.cs` if it mentions messages.
- [ ] T018 [US2] In `frontend/apps/web/src/app/core/models/chat.models.ts` delete
      `MessageSearchHit` and the `messages` member of `ChatSearchResult`; in
      `frontend/apps/web/src/app/core/services/chat.service.spec.ts` rename and trim
      `'searches messages and people'` to people only. `grep -rn "messages.items\|MessageSearchHit" frontend/apps/web/src`
      must return nothing.
- [ ] T019 [US2] Run `dotnet test … --filter "FullyQualifiedName~ChatSearchTests|FullyQualifiedName~ChatInboxSearchTests"`
      and `npx nx test web --watch=false --testPathPattern=chat`. Fix until green.

**Checkpoint**: quickstart Scenario B passes, including the two direct requests.

---

## Phase 5: User Story 3 — Find a conversation by its own name (Priority: P2)

**Goal**: a group's name, a team chat's team name and an admin-contact thread's shown name match
even when no member does. The code is the names half of T003; this phase proves it.

**Independent Test**: seed a group "Tournament trip", a team "Hamburg Jugger" and an inquiry
thread, none with a member whose name contains the term; `?q=trip` and `?q=hamb` list them.

- [ ] T020 [US3] Add to `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatInboxSearchTests.cs`:
      `Finds_a_group_by_its_name`; `Finds_a_team_chat_by_the_teams_name`;
      `Finds_an_admin_contact_thread_by_team_name_and_by_requester_name` (start the thread with
      the feature-027 endpoint — copy the call shape from `ChatInquiryTests.cs`; search as the
      team admin for the team's name and for the requester's name);
      `A_conversation_matching_by_name_and_member_is_listed_once`;
      `Fallback_labels_are_not_names` (a group named "Weekend crew" is not returned for `?q=group`;
      no party seeding needed — assert the literal does not match).
- [ ] T021 [US3] Run the US3 facts; if the inquiry branch or `c.Event!.Name` fails to translate,
      fix the expression in `backend/Services/Chat/ChatGuard.cs` (never post-filter in memory).

**Checkpoint**: quickstart Scenario C passes.

---

## Phase 6: User Story 4 — Starting a chat with someone new is unaffected (Priority: P2)

**Goal**: a regression guard — the people search behind the new-chat picker, compose-by-handle
and the profile Message action still reaches anyone and still excludes blocked players.

**Independent Test**: the existing specs for those three surfaces and `ChatSearchTests`' people
facts pass unchanged.

- [ ] T022 [P] [US4] Run, unchanged, `npx nx test web --watch=false --testPathPattern="chat-new|chat-compose|profile-quick-actions"`
      and confirm each still reads `res.people.items` against the trimmed `ChatSearchResult`
      (compile error = the contract was narrowed too far).
- [ ] T023 [P] [US4] Run `dotnet test … --filter "FullyQualifiedName~ChatSearchTests"` and
      `FullyQualifiedName~ChatLazyDirectTests|ChatBlockTests` — `Finds_people_and_surfaces_an_existing_dm`,
      `People_search_reaches_players_you_share_nothing_with`, `You_never_appear_in_your_own_people_search`
      and the block exclusion must be green.

**Checkpoint**: quickstart Scenario D passes.

---

## Phase 7: Documentation amendments (feature 019)

- [ ] T024 [P] In `specs/019-chat/spec.md` add a second callout under `## Amendments`, after
      022's, in the same style: "**Amended by feature 046 (2026-09-08) — inbox search finds
      conversations by name; message-text search removed.** …" pointing at
      `specs/046-chat-inbox-search/`; append "*(superseded by 046)*" to FR-034, FR-035, FR-036,
      FR-050c and SC-006, and a one-line note under User Story 6's heading.
- [ ] T025 [P] In `specs/019-chat/contracts/chat-api.md`, at the top of the **Search** section,
      add: "> Amended by 046: `messages` removed; `GET /chat/conversations` gained `q` — see
      `specs/046-chat-inbox-search/contracts/chat-inbox-search-api.md`."

---

## Phase 8: Polish & cross-cutting

- [ ] T026 Instantiate `specs/046-chat-inbox-search/checklists/ui-review.md` from
      `.specify/templates/ui-review-checklist-template.md` and verify every item against the
      diff of `chat-inbox.component.html` and the catalogues (Gate 7): sentence case, "you" voice,
      `role="status"` loading line, empty-vs-error distinction, 44px targets unchanged, no colour
      alone, no emoji. Report any DESIGN.md conflict; do not resolve it silently.
- [ ] T027 Full verification: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Chat"`;
      `cd frontend && npx nx test web --watch=false`; `npm run lint`; `npm run build`. Record
      counts and any failure verbatim.
- [ ] T028 Walk `specs/046-chat-inbox-search/quickstart.md` Scenarios A–E in the running app
      (the `run` skill launches it). Record what was verified and what could not be.
- [ ] T029 Comment on GitHub #221 with the outcome (what changed, the FR-002 fallback-label drift,
      #222 for the un-hide gap) and note the branch; do not open a PR unless asked.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → nothing; run first.
- **Foundational (Phase 2)** → Setup. Blocks US1 and US3.
- **US1 (Phase 3)** → Foundational. Its tests (T006–T008) can be written in parallel with Phase 2.
- **US2 (Phase 4)** → Setup only, but it edits `ChatDtos.cs` and `chat.service.spec.ts`, which
  US1 also touches, so run it **after** US1 to avoid same-file conflicts.
- **US3 (Phase 5)** → Foundational (the names half of T003). Tests only.
- **US4 (Phase 6)** → US2 (it proves the trimmed contract).
- **Docs (Phase 7)** → any time after Phase 2; parallel with anything.
- **Polish (Phase 8)** → everything above.

### Within each story

- Tests first, red; then implementation; then the story's checkpoint.
- Backend before frontend inside US1 (the component needs the endpoint's `q`).

### Parallel opportunities

- T001 ‖ T002.
- T006 ‖ T007 ‖ T008 (three test files) while Phase 2 is being written.
- T014 ‖ T015; T016 → T017 → T018 sequential (shared DTO), T019 after.
- T022 ‖ T023; T024 ‖ T025.

## Parallel Example: User Story 1

```text
# While Phase 2 lands, write the three test files together:
Task: "ChatInboxSearchTests.cs — member matches, SC-003/004/007, exclusions, accents"
Task: "chat.service.spec.ts — searchInbox request shape, conversations() untouched"
Task: "chat-inbox.component.spec.ts — rows, empty state, clearing, status line, no old sections"
```

## Implementation Strategy

### MVP first (User Story 1)

1. Phase 1 → Phase 2 → Phase 3. **Stop and validate** with quickstart Scenario A.
2. Ship-able on its own: the inbox already finds conversations by member name; the message
   sections are still rendered from the old endpoint half until US2 lands.

### Incremental delivery

1. US1 → conversations by member name (MVP).
2. US2 → message-text search removed everywhere; contract trimmed.
3. US3 → proven for conversation names.
4. US4 → regression guard on the new-chat surfaces.
5. Docs + Polish → 019 amended, Gate 7 checklist, full verification, quickstart walk.

## Notes

- Commit after each phase (small commits, conventional prefixes: `feat(046)`, `test(046)`,
  `docs(046)`), referencing #221.
- The inbox loads only its first page and has no load-more; that pre-existing limit is **why**
  SC-004 needs a server-side search and is otherwise out of scope.
- Hidden conversations stay out of search by owner decision; the un-hide gap is GitHub #222.
