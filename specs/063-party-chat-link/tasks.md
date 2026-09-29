# Tasks: The Party Page Leads Into the Party Chat

**Input**: Design documents from `specs/063-party-chat-link/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/party-chat-api.md](./contracts/party-chat-api.md), [quickstart.md](./quickstart.md)

**Tests**: included, as in 060. Each behaviour gets a test written before the code that makes it
pass. The backend endpoint serves all three stories, so it is foundational. The stories differ only
in which card shows the button and who sees it.

**Organization**: by user story. US1 and US2 are both P1 and touch different cards of the same
template, so they run one after the other, never alongside.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: US1 / US2 / US3 from spec.md

---

## Phase 1: Setup

- [X] T001 Record the baseline on the branch: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~ChatTeamChatLink|FullyQualifiedName~ChatTeamParty|FullyQualifiedName~Parties"` and, in `frontend/`, `npx nx test web --watch=false --testPathPatterns="party-|chat.service|catalog-"`. Note any pre-existing failure so it is not later mistaken for a regression. *(Done: backend 57/57, frontend 51/51; no pre-existing failure.)*

---

## Phase 2: Foundational (blocking): the endpoint and the client call

**Purpose**: `GET /api/v1/chat/party/{partyId}` exactly as [contracts/party-chat-api.md](./contracts/party-chat-api.md) states, with 060's order held in one shared helper (research R1).

- [X] T002 [P] Add `public sealed record PartyChatRefDto(Guid ConversationId);` with an XML doc (063: the party chat's id, never null; anyone outside the crew gets a 404, not an empty reference; the id and nothing else, FR-006) directly after `TeamChatRefDto` in `backend/Dtos/Chat/ChatDtos.cs`
- [X] T003 [P] Add `export interface PartyChatRef { readonly conversationId: string; }` with a doc comment directly after `TeamChatRef` in `frontend/apps/web/src/app/core/models/chat.models.ts`
- [X] T004 Create `backend/tests/JuggerHub.Api.IntegrationTests/Parties/PartyChatLinkTests.cs` (`[Collection("Parties")]`, `: PartyTestSupport`, a class doc naming GH #382 and 060). Local helpers: `OpenPartyChatAsync(client, partyId)` → `GET /api/v1/chat/party/{partyId}`; `OpenPartyChatIdAsync` (asserts 200, reads `conversationId`); `InboxPartyChatIdAsync(client, partyId)` (reads `GET /api/v1/chat/conversations` items with `kind == "Party"` and matching `partyId`); `PartyChatCountAsync(partyId)` (DB count `Kind == Party && PartyId == partyId`); `SeatGuestAsync(partyId, userId)` (DB insert `PartyMember { PartyId, UserId, Status = In, Role = Member, ViaMarket = true }`); a `Json` = `new(JsonSerializerDefaults.Web)`. Setup per test: admin `A` → `CreateTeamAsync` → `CreateTeamsEventAsync` → `FormPartyAsync`; team members via `AddTeamMemberAsync`, crew via `POST /api/v1/parties/{id}/join`. Facts:
  - crew member → 200, id equals the inbox's party row (FR-002);
  - party admin (the creator) → 200, same id (US2);
  - marketplace guest (not on the team) → 200, same id (US1-3);
  - never-opened → 200, exactly one `Kind == Party` row now exists, 0 messages, **no** new `Notifications` row for the crew, no new `Factory.EmailSender.Sent`, the crew not in `Factory.PushDispatcher.Recipients` (FR-003);
  - team member who has not answered → 404, and `PartyChatCountAsync == 0` afterwards (FR-007);
  - team member who declined (`POST …/decline`) → 404;
  - former crew member (joined then `POST …/leave`) → 404 (FR-011's server side);
  - disbanded (`DELETE /api/v1/parties/{id}` by A, after the chat exists) → 404 for the former crew member;
  - the party's **team id** passed as a party id, with the team chat existing → 404 (another kind, research R2);
  - not on the team, and a random GUID → 404 with `title`/`detail`/`status` equal between the two (FR-005, SC-003);
  - hidden and muted (`PATCH /api/v1/chat/conversations/{id}/state`) → 200, same id, both flags still `true` on the `ConversationParticipant` row (FR-012);
  - the 200 body has exactly one property, `conversationId` (FR-006);
  - three concurrent first calls from crew members → one distinct id, one row (FR-013);
  - no session → 401.

  Build and run it: every fact fails (the route does not exist yet).
- [X] T005 Declare `Task<ChatResult<PartyChatRefDto>> OpenPartyChatAsync(Guid callerId, Guid partyId, CancellationToken ct = default);` after `OpenTeamChatAsync` in `backend/Services/Chat/IChatConversationService.cs`, with an XML doc mirroring `OpenTeamChatAsync`'s (crew member → id, creating like the inbox; anyone else → NotFound, indistinguishable; a disbanded party is NotFound).
- [X] T006 In `backend/Services/Chat/ChatConversationService.cs`: extract `OpenTeamChatAsync`'s body into `private async Task<Guid> OpenAutoChatAsync(Guid callerId, ConversationKind kind, Guid ownerId, CancellationToken ct)` (ensure → find → guard, returning `Guid.Empty` for not found) and **move** the order's `<remarks>` onto it, generalised to "team or party" (no `EnsureForXAsync(requestedId)`: anyone could create anyone's chat and an unknown id rethrows an FK violation; no roster pre-check: ChatGuard is the one membership rule). `OpenTeamChatAsync` becomes a wrapper (`Guid.Empty` → `Fail(NotFound)`, else `Ok(new TeamChatRefDto(id))`), and add `OpenPartyChatAsync` the same way with `ConversationKind.Party` and `PartyChatRefDto`.
- [X] T007 Add `[HttpGet("party/{partyId:guid}")] OpenPartyChat(Guid partyId, CancellationToken ct)` → `ActionResult<PartyChatRefDto>` directly after `OpenTeamChat` in `backend/Controllers/ChatConversationsController.cs`, under a `// --- Party chat (feature 063) ---` divider. Thin, the same shape as `OpenTeamChat`; its doc comment mirrors it.
- [X] T008 `dotnet build backend/JuggerHub.slnx` (**gate on its exit code**, never through a pipe), then `dotnet test … --filter "FullyQualifiedName~PartyChatLink|FullyQualifiedName~ChatTeamChatLink|FullyQualifiedName~ChatTeamParty"` until green. 060's `ChatTeamChatLinkTests` must pass **unedited**.
- [X] T009 [P] Add `openPartyChat(partyId: string): Observable<PartyChatRef>` → `GET ${this.base}/party/${partyId}` directly after `openTeamChat` in `frontend/apps/web/src/app/core/services/chat.service.ts` (doc comment: feature 063, looked up on the press), and a URL/verb test next to `openTeamChat`'s in `frontend/apps/web/src/app/core/services/chat.service.spec.ts`.
- [X] T010 [P] Add `parties.manage.partyChat`, `openingPartyChat`, `partyChatFailed`, `partyChatNotCrew` with research R7's values to `frontend/apps/web/public/i18n/en.json`, `de.json` and `es.json`, all three in the same change.

**Checkpoint**: commit `feat(063): resolve a party's chat for its crew (#382)`.

---

## Phase 3: User Story 1 — A crew member opens the party chat from the party page (P1) 🎯 MVP

**Goal**: in the "You're in this crew" card, **Party chat** opens the party's own chat, creating it if needed (FR-001 to FR-012, FR-014).

**Independent test**: as a crew member (and as a marketplace guest), press Party chat and land in the conversation whose id equals the inbox's party row.

- [X] T011 [US1] Create `frontend/apps/web/src/app/features/parties/party-manage/party-manage.component.spec.ts` (class doc: feature 063, GH #382). A `render(p: Partial<Party>)` helper provides `provideRouter([])`, the transloco testing module and locale providers, `PartyService` stubbed (`getParty` → `of(party)`, `listMembers` → `of(page([]))`, `listNews` → `of(page([]))`), `ChatService` stubbed `{ openPartyChat: jest.fn() }`, and `ActivatedRoute` with snapshot id `party-1`; a `party(overrides)` fixture builds a full `Party`. Tests:
  - crew member (`myState 'In'`, `myRole 'Member'`): `party-chat` is inside `[data-testid="crew-card"]`, before `leave-party`;
  - loading the page does **not** call `openPartyChat` (SC-004);
  - press → `openPartyChat('party-1')`, then the router navigates to `['/chat', id]`;
  - while pending (a `Subject`), the button is disabled and shows *Opening…*; a second press makes no second call;
  - 404 → `party-chat-notice` shows *You're no longer in this crew.* and `getParty` is called a second time;
  - 500 → `party-chat-error` shows inside `crew-card` and the button is enabled again.

  Run it: the placement and press tests fail.
- [X] T012 [US1] In `frontend/apps/web/src/app/features/parties/party-manage/party-manage.component.ts`: inject `ChatService`; add signals `partyChatBusy`, `partyChatError` and `partyChatNotice` (translation keys or null, each with a one-line doc saying where it renders and why); add `openPartyChat()`. It returns early when busy, clears both keys, sets busy, then calls `chat.openPartyChat(this.id)`. On success → busy false, `router.navigate(['/chat', ref.conversationId])`. On `HttpErrorResponse` 404 → busy false, `partyChatNotice.set('parties.manage.partyChatNotCrew')`, `reload()`. On anything else → busy false, `partyChatError.set('parties.manage.partyChatFailed')`. Branch on status, never `detail` (GH #179), and do not route through `fail()`.
- [X] T013 [US1] In `party-manage.component.html`: give the non-admin card `data-testid="crew-card"`. In its `p.myState === 'In'` branch, replace the lone Leave button with a `mt-sm flex flex-wrap gap-sm` row: the Party chat `<button type="button" jhButton variant="secondary" data-testid="party-chat" [disabled]="partyChatBusy()" (click)="openPartyChat()">` (label `openingPartyChat` while busy, else `partyChat`), then Leave (`data-testid="leave-party"`, `size="sm"` **removed** so it matches at 44px, research R6). Under the row: `@if (partyChatError(); as key) { <p class="mt-xs text-body-sm text-danger-fg" role="alert" data-testid="party-chat-error">…</p> }`. Add the page-level `@if (partyChatNotice(); as key) { <jh-alert tone="info" class="mt-sm" data-testid="party-chat-notice">…</jh-alert> }` directly after the existing `error()` alert, **outside** both cards, with a one-line comment saying why. Run T011 until green.

**Checkpoint**: commit `feat(063): crew members open the party chat from the party page (#382)`.

---

## Phase 4: User Story 2 — A party admin opens the party chat from the readiness card (P1)

**Goal**: Party chat sits beside *Apply to event* / *Withdraw* in the admin's readiness card, secondary (FR-015, FR-016).

**Independent test**: as the party's creator, before and after applying, the readiness card holds Party chat and Apply stays the only primary button.

- [X] T014 [US2] Add to `party-manage.component.spec.ts`:
  - admin, not applied (`myRole 'Admin'`, `myState 'Admin'`, `status 'Open'`): `party-chat` is inside `[data-testid="readiness-card"]`, after `apply-to-event`; `apply-to-event` carries the primary variant's class (`bg-brand-strong`) and `party-chat` does not. *(Not "the only one on the page": the admin's news composer already has a primary *Post update*, pre-existing, recorded in the UI review.)*;
  - admin, applied (`status 'Applied'`, `appliedGroup 'Joined'`): `party-chat` is beside `withdraw-from-event`;
  - no `crew-card` is rendered for an admin (the card stays non-admin only);
  - pressing the admin's `party-chat` calls `openPartyChat('party-1')`;
  - 500 → `party-chat-error` shows inside `readiness-card`.

  Run them: they fail.
- [X] T015 [US2] In `party-manage.component.html`: give the admin readiness card `data-testid="readiness-card"`, the Apply button `data-testid="apply-to-event"` and the Withdraw button `data-testid="withdraw-from-event"` (dropping Withdraw's `size="sm"`, research R6). Append the same Party chat button after the `@if (!isApplied()) … @else …` block inside the existing `mt-md flex flex-wrap gap-sm` row, and the same `party-chat-error` line under the row. Keep the button markup identical to the crew card's. Run the spec until green.

**Checkpoint**: commit `feat(063): party admins open the party chat from the readiness card (#382)`.

---

## Phase 5: User Story 3 — Team members outside the crew see the page as before (P2)

**Goal**: no Party chat for `NoResponse` / `Declined`, and nothing else changes (FR-001, FR-017, SC-005).

**Independent test**: render the page for a team member with no answer and for one who declined; no `party-chat` anywhere; *I'm in* then reveals it.

- [ ] T016 [US3] Add to `party-manage.component.spec.ts`:
  - `myState 'NoResponse'` (not full): no `party-chat`; the card still shows *I'm in* and *Can't make it*;
  - `myState 'Declined'`: no `party-chat`;
  - `myState 'NoResponse'` with `isFull: true`: no `party-chat` (the "full, reopens" line only);
  - pressing *I'm in* (`PartyService.join` → `of({})`, then `getParty` returns `myState 'In'`) → `party-chat` appears in `crew-card`.

  No template change is expected (the button lives only in the `In` branch and the admin card). If one fails, fix the template, not the test.

**Checkpoint**: commit `test(063): team members outside the crew get no party chat button (#382)`.

---

## Phase 6: Polish & cross-cutting

- [ ] T017 [P] Add an "**Amended by feature 063 (party chat link).**" paragraph to the amendment list at the top of `specs/019-chat/contracts/chat-api.md`, next to 060's. It covers the new `GET /chat/party/{partyId}`, and says that 060's and 063's resolvers share one ensure → find → guard helper. Link `../../063-party-chat-link/contracts/party-chat-api.md`.
- [ ] T018 Full verification: `dotnet build backend/JuggerHub.slnx` (exit code) → `dotnet test backend/JuggerHub.slnx`; in `frontend/`: `npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`. Report any failure with its output. Never call a skipped check "passed".
- [ ] T019 Browser walk (owner's standing rule): `docker compose up -d --build backend frontend`, then a throwaway Playwright script inside `frontend/` (one context per actor, `locale: 'de-DE'`) that drives quickstart scenarios 1–11 at **375px and desktop**. Screenshot the crew card, the admin readiness card before and after applying, a guest's card, the no-answer and declined request cards, the busy state if catchable, the not-in-crew notice, the forced-failure line, and the landed chat. Scope selectors to the page (`jh-party-manage` / test ids), never a bare `header` (060's walk lesson). Read the driver's output and **look at** each screenshot. Delete the script afterwards.
- [ ] T020 Gate 7: copy `.specify/templates/ui-review-checklist-template.md` to `specs/063-party-chat-link/checklists/ui-review.md` and answer every item against the diff and the T019 screenshots. DESIGN.md wins any conflict. Record the deliberate `sm` → default change of *Leave the party* / *Withdraw from event* and any wrap at 375px. Record conflicts; do not resolve them silently.
- [ ] T021 Commit the walk fixes and the UI review, push `063-party-chat-link`, and open a PR that **`Closes #382`**. The PR summarises the owner decisions (including the page-visibility question and its answer), the shared helper, the size change and the verification run. Update the auto-memory with this feature's decisions and traps.

---

## Dependencies & execution order

- Setup (T001) → Foundational (T002 ∥ T003; T004 → T005 → T006 → T007 → T008; T009 ∥ T010 after T003) → **US1** (T011 → T012 → T013) → **US2** (T014 → T015) → **US3** (T016) → Polish (T017 any time after T007; T018 → T019 → T020 → T021).
- US1, US2 and US3 all edit or test `party-manage.component.*`, so they run in sequence.
- The frontend (T009 onward) needs only T003 to compile. Only the walk (T019) needs the backend.

## Parallel opportunities

- T002 ∥ T003 (backend DTO / frontend model).
- T009 ∥ T010 (service / catalogues), and both alongside the backend T004–T008.
- T017 at any time after T007.

## Implementation strategy

1. **Foundational first**: the endpoint is the whole server side, and the extraction is proven by
   060's unedited suite in the same step.
2. **MVP = US1**: the crew card is where most crew members will press.
3. **US2** adds the admin's route; **US3** pins that nothing else changed for everyone else.
4. Verify with automated suites, then the browser walk, then Gate 7, then the PR.
