---

description: "Task list for 059 — event and party news posts can be edited and deleted"
---

# Tasks: Event and Party News Posts Can Be Edited and Deleted

**Input**: Design documents from `specs/059-event-party-news-edit-delete/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/news-api.md](./contracts/news-api.md), [quickstart.md](./quickstart.md)

**Tests**: Included. The plan's test strategy (research R14) is part of the design: the
silent-edit guarantees (FR-005/FR-006/FR-010), the outsider 404 (SC-004), the Principle III
`ModifiedDate` obligation and the unchanged team page (FR-023) are only provable by tests.

**Organization**: by user story. US1 = event news (P1), US2 = party news (P1), US3 = Home marks
edited event and party posts (P2). The shared `jh-news-post` component, and the team page moved
onto it, are **foundational**: both P1 stories render it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1 / US2 / US3 (setup, foundational and polish carry none)

---

## Phase 1: Setup

**Purpose**: know the baseline, so a later failure is attributed to the right change.

- [X] T001 On the untouched branch, run `dotnet build backend/JuggerHub.slnx` (gate on its exit code, never `| grep`) and, in `frontend/`, `npx nx test web --watch=false --testPathPatterns="team-detail|news-list|catalog-"`; record any pre-existing failure under **Notes** at the end of `specs/059-event-party-news-edit-delete/tasks.md`

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: the columns, the widened DTOs, the frontend models, the shared copy, and the shared
component with the team page moved onto it. Every story builds on these.

**⚠️ No user-story work starts before this phase's checkpoint.**

### Data

- [X] T002 [P] Add `public DateTime? EditedDate { get; set; }` to `backend/Entities/EventNewsPost.cs` and rewrite its summary: read by signed-in players on the event page (not "public", 026), posted by event admins, editable and deletable by any current event admin (059); `EditedDate` = when a person last changed the text, null = never, deliberately not `ModifiedDate` (research R4, R11)
- [X] T003 [P] Add `EditedDate` the same way to `backend/Entities/PartyNewsPost.cs`, summary extended with "editable and deletable by any current party admin (059)"
- [X] T004 Generate the migration **with a build**: `dotnet ef migrations add AddEventAndPartyNewsEditedDate --project backend` (NOT `--no-build`, the 056 empty-migration lesson); read `backend/Data/Migrations/*_AddEventAndPartyNewsEditedDate.cs` and confirm exactly two nullable `AddColumn<DateTime>` (`EventNewsPosts`, `PartyNewsPosts`), no default, no SQL, and a `Down` that drops both
- [X] T005 [P] In `backend/Dtos/Events/EventDtos.cs`: append `DateTime? EditedDate` to `EventNewsDto` after `CreatedDate`, and add `public sealed record EditEventNewsRequest([Required, MaxLength(2000)] string Body);` beside `CreateNewsRequest`
- [X] T006 [P] In `backend/Dtos/Parties/PartyDtos.cs`: append `DateTime? EditedDate` to `PartyNewsDto` after `CreatedDate`, and add `public sealed record EditPartyNewsRequest([Required, MaxLength(1000)] string Body);` beside `CreatePartyNewsRequest`
- [X] T007 Make the backend compile with the widened DTOs: `EventNewsService` feed projection and `PostAsync` pass `EditedDate` (feed: `n.EditedDate`; post: `null`) in `backend/Services/Events/EventNewsService.cs`; `PartyNewsService.ListAsync` passes `n.EditedDate` and `CreateAsync` passes `null` in `backend/Services/Parties/PartyNewsService.cs`; then `dotnet build backend/JuggerHub.slnx` succeeds
- [X] T008 [P] Frontend models: add `editedDate: string | null` to `EventNews` in `frontend/apps/web/src/app/core/models/event.models.ts` and to `PartyNews` in `frontend/apps/web/src/app/core/models/party.models.ts`; fix every compile error this surfaces in existing specs/fixtures by adding `editedDate: null`

### Copy (all three catalogues in ONE commit — `catalog-parity` fails otherwise)

- [X] T009 In `frontend/apps/web/public/i18n/{en,de,es}.json` add a top-level `news` namespace holding 057's shared strings with their values **unchanged**, moved from `teams.detail`: `manage`←`newsManage`, `edit`←`newsEdit`, `delete`←`newsDelete`, `edited`←`newsEdited`, `editLabel`←`newsEditLabel`, `saving`←`newsSaving`, `saveFailed`←`newsSaveFailed`, `gone`←`newsGone`, `deleteTitle`←`newsDeleteTitle`, `deleteKeep`←`newsDeleteKeep`, `deleteConfirm`←`newsDeleteConfirm`, `deleting`←`newsDeleting`, `deleteFailed`←`newsDeleteFailed`; plus `editHint.{team,event,party}` and `deleteBody.{team,event,party}` with `team` = 057's `newsEditHint`/`newsDeleteBody` and `event`/`party` = the wording in research R10's table. Delete the moved `teams.detail.news*` keys (keep `news`, `newsPlaceholder`, `postNews`, `postingNews`, `noNews`). German `–` never `—`

### Shared component (research R8, R9; contract → "UI contract")

- [X] T010 [P] Create `frontend/apps/web/src/app/shared/news-post/news-post-editing.ts`: `@Injectable() export class NewsPostEditing` holding `editingId` and `draft`, **provided by each page component** (never root), with an on-screen registry so `locked` counts only a post that is displayed; doc comment says why (the team page rebuilds its list behind a spinner on reload, and 057 kept the editor and its draft across that)
- [X] T011 Create `frontend/apps/web/src/app/shared/news-post/news-post.component.{ts,html,css}` (`selector: 'jh-news-post'`, generic in `T`) by **moving** 057's menu, inline editor and delete dialog out of `features/teams/team-detail/team-detail.component.{ts,html}`, keeping markup, classes, comments and **every `data-testid`/`data-news-menu-trigger`/`data-news-menu` attribute verbatim**, generalised by the contract's inputs (`postId`, `body`, `canManage`, `maxLength`, `editHint`, `deleteBody`, `save`, `remove`) and outputs (`saved: T`, `removed: { gone: boolean }`). Specifically: host class `flex items-start gap-xs`; projected display wrapped in `<div [hidden]="editing()">` (never `ng-content` inside `@if`); copy keys switch to `news.*` and the two inputs; lock via `NewsPostEditing` (set on edit, cleared on cancel/save/gone and in `DestroyRef.onDestroy` when this instance holds it; trigger `[disabled]` while any id is set); `@HostListener('document:click')` closes the menu only when the click is outside **this instance's** `[data-news-menu]` wrapper; `@HostListener('document:keydown.escape')` closes own menu (focus → own trigger) or dismisses own dialog; `trapTab` moved; focus via `afterNextRender(..., { injector })` scoped to the component's host element; `removed` is emitted **before** anything else once the dialog closes (the host then destroys this instance); errors branch on `status === 404` only, never on `detail`
- [X] T012 Move the team page onto it in `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.{ts,html}`: each `<li data-testid="news-post">` wraps `<jh-news-post … [maxLength]="1000" editHint="news.editHint.team" deleteBody="news.deleteBody.team">` projecting the existing body + meta (meta marker key → `news.edited`); `save`/`remove` are arrow-function properties calling `teams.editNews`/`deleteNews` with `this.slug()`; `(saved)` replaces the item; `(removed)` drops it, sets `newsNotice` to `news.gone` when `gone`, and focuses `#team-news-heading` via `afterNextRender`. Delete from team-detail: `newsMenu`, `editingNewsId`, `newsDraft`, `savingNews`, `newsEditError`, `deleteNewsTarget`, `deletingNews`, `newsDeleteError`, `startEdit`, `cancelEdit`, `saveEdit`, `askDeleteNews`, `dismissDeleteNews`, `confirmDeleteNews`, `trapTab`, `dropNews`, the news branches of `onEscape`/`onDocumentClick` (drop `onDocumentClick` if empty), the dialog markup, the four news resets in the `paramMap` subscription (keep `newsNotice.set(null)`), and `isGone` if unused. Add `overflowVisible` to the News `<jh-card>` (research R9)
- [X] T013 Run `npx nx test web --watch=false --testPathPatterns="team-detail"` **without editing `team-detail.component.spec.ts`**; all of 057's news tests must pass (FR-023, SC-007). A failure means the move changed behaviour: fix the component, never the spec
- [X] T014 [P] Write `frontend/apps/web/src/app/shared/news-post/news-post.component.spec.ts` against a small host component rendering two posts: no menu when `canManage` is false; edit → save calls `save(id, trimmed)` once and emits `saved`; unchanged text and cancel send nothing and return focus to the trigger; a 503 keeps the editor, the typed text and shows `news.saveFailed`; a 404 emits `removed({ gone: true })`; delete dialog: initial focus on *Keep post*, keep sends nothing, confirm calls `remove` and emits `removed({ gone: false })`, 503 keeps the dialog with `news.deleteFailed`, 404 emits `gone`; the lock (editing post 1 disables post 2's trigger, released after cancel and after the editing instance is destroyed); opening post 2's menu closes post 1's; Escape closes the open menu; `maxlength` follows the input; the edit hint and delete body render the keys given

**Checkpoint**: backend builds with the columns; the shared component exists; the team page runs on
it with 057's tests green and unedited.

---

## Phase 3: User Story 1 — An event admin corrects or removes an event update (Priority: P1) 🎯 MVP

**Goal**: any current event admin edits or deletes any event news post from the event page;
silent; marked edited; hard delete behind the confirmation.

**Independent Test**: as an event admin, edit a post (in place, marked edited, nothing sent),
then delete it (gone from the event page and Home).

### Tests for User Story 1

- [X] T015 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/Events/EventNewsEditDeleteTests.cs` (`[Collection("Events")]`, the 057 test file's shape and helpers, co-admin seeded via an `EventAdmins` row or the invite flow): a co-admin edits the creator's post in place (position, author, `createdDate` kept; `editedDate` set; `ModifiedDate` moved — Gate 2); unchanged text → 200 and `editedDate` still null; `""`/`"   "`/2001 chars → 400 and unchanged; non-admin → 403; unknown event → 404 `"Event not found"`; another event's post → 404 `"News post not found"` even for an admin of both; a co-admin removed from the event → 403; delete → 204, gone from the feed and from a connected player's `/home/news`; second delete → 404; a **cancelled** event's post can still be edited and deleted (FR-015); nothing sent on edit or delete (`TestEmailSender`, `FakePushDispatcher`, `FakeNotificationRealtime` counts unchanged, no `Notifications` rows). Run it and watch it fail
- [X] T016 [P] [US1] Write `frontend/apps/web/src/app/core/services/event.service.spec.ts` (`HttpTestingController`): `editNews(id, postId, body)` → `PATCH /api/v1/events/{id}/news/{postId}` with `{ body }`; `deleteNews(id, postId)` → `DELETE` same URL

### Implementation for User Story 1

- [X] T017 [US1] In `backend/Services/Events/IEventNewsService.cs` add `EventNewsEditStatus`, `EventNewsDeleteStatus`, `EventNewsEditResult` and the two methods, with XML docs (any current admin, any post; silent; hard delete; nothing to recall), per the contract
- [X] T018 [US1] In `backend/Services/Events/EventNewsService.cs`: extract `Project(IQueryable<EventNewsPost>, string placeholder)` + `AuthorPlaceholder()` used by `GetFeedAsync`, `PostAsync` (replacing the English `"An organiser"` fallback, research R11) and `EditAsync`; implement `EditAsync` (guard → admin → current body scoped `Id ∧ EventId` → trimmed equal ⇒ no write → `ExecuteUpdate(Body, EditedDate, ModifiedDate)` → 0 rows ⇒ `PostNotFound` → project) and `DeleteAsync` (guard → admin → `ExecuteDelete WHERE Id ∧ EventId` → 0 ⇒ `PostNotFound`). Comments say why no transaction (single statement, research R3) and that `ModifiedDate` is set because `ExecuteUpdate` skips the interceptor
- [X] T019 [US1] In `backend/Controllers/EventsController.cs` add `[HttpPatch("{id:guid}/news/{postId:guid}")] EditNews` (200 / 403 `"Only an event admin can edit news."` / `EventNotFound()` / `NewsPostNotFound()`) and `[HttpDelete(...)] DeleteNews` (204 / 403 `"Only an event admin can delete news."` / 404 / 404), plus `private ObjectResult NewsPostNotFound()` with `TeamsController`'s title and detail; run T015 until green
- [X] T020 [P] [US1] In `frontend/apps/web/src/app/core/services/event.service.ts` add `editNews(id, postId, body): Observable<EventNews>` and `deleteNews(id, postId): Observable<void>`; T016 green
- [X] T021 [US1] In `frontend/apps/web/src/app/features/events/event-detail/components/news-feed.component.{ts,html}`: `news` becomes `model.required<EventNews[]>()`; new inputs `eventId` and `canManage`; inject `EventService` for arrow-function `save`/`remove`; each post `<li>` wraps `<jh-news-post [maxLength]="2000" editHint="news.editHint.event" deleteBody="news.deleteBody.event">` projecting the body (add `whitespace-pre-line break-words`) and the meta line with `· {{ 'news.edited' | transloco }}` after the date inside a `whitespace-nowrap` span when `editedDate`; `(saved)` replaces, `(removed)` drops, sets a `notice` signal to `news.gone` when `gone`, and focuses the heading; the `h2` gets `id="event-news-heading" tabindex="-1"`; the notice renders as `<p role="status" data-testid="news-notice">` above the list
- [X] T022 [US1] In `frontend/apps/web/src/app/features/events/event-detail/event-detail.component.html` bind `[(news)]="news" [eventId]="d.id" [canManage]="d.viewer.isAdmin"` (no cancelled gate, FR-015; `canCompose` keeps it) and add `overflowVisible` to the News `jhCard` wrapper (research R9); keep `event-detail.component.ts`'s `postNews` working with the two-way signal
- [X] T023 [P] [US1] Write `frontend/apps/web/src/app/features/events/event-detail/components/news-feed.component.spec.ts`: no menu without `canManage`; menus with it; the marker only on edited posts; a save replaces the item in the bound list; a delete drops it; a 404 drops it and shows `news.gone`

**Checkpoint**: event news is editable and deletable end to end; the team page is unchanged.

---

## Phase 4: User Story 2 — A party admin corrects or removes a party update (Priority: P1)

**Goal**: any current party admin edits or deletes any party news post from either party page;
an edit leaves Alerts untouched; a delete takes the post's Alerts rows with it, former crew
included, and lowers unread badges.

**Independent Test**: as a party admin, edit a post (both pages show it edited; alert rows
byte-identical; nothing sent), then delete it (gone from both pages, Home, and every recipient's
Alerts; an unread recipient's count drops).

### Tests for User Story 2

- [ ] T024 [P] [US2] Write `backend/tests/JuggerHub.Api.IntegrationTests/Parties/PartyNewsEditDeleteTests.cs` (`[Collection("Parties")]`, deriving from `PartyTestSupport`): a party co-admin edits the creator's post (in place, `editedDate` set, `ModifiedDate` moved); the edit leaves every `PartyNews` row for the post **byte-identical** (`Payload`, `IsRead`, `ModifiedDate`, `CreatedDate`) and sends nothing (email, push, realtime counts unchanged); unchanged text is a no-op; `""`/`"   "`/1001 chars → 400; crew non-admin → 403; a member of the party's team who is **not in the crew** and a stranger → 404 `"Party not found"`, the same as `GET /parties/{id}/news` answers them; a random party id → the same 404; another party's post → 404 `"News post not found"`; delete → 204, post gone from both feeds and `/home/news`; every `PartyNews` row under `party-news:{postId}:` gone — including a crew member who **declined after** the post; another post's rows untouched; the unread recipient's count dropped and `FakeNotificationRealtime` recorded the lower count for them only (not for someone who had read it); second delete → 404. Run it and watch it fail
- [ ] T025 [P] [US2] Write `frontend/apps/web/src/app/core/services/party.service.spec.ts`: `editNews` → `PATCH /api/v1/parties/{id}/news/{postId}` with `{ body }`; `deleteNews` → `DELETE`

### Implementation for User Story 2

- [ ] T026 [US2] In `backend/Services/Parties/IPartyNewsService.cs` add `PartyNewsEditStatus`, `PartyNewsDeleteStatus`, `PartyNewsEditResult` and the two methods, with XML docs (any current party admin; an edit leaves the alerts alone because they quote nothing; a delete removes them); update the interface summary
- [ ] T027 [US2] In `backend/Services/Parties/PartyNewsService.cs`: add `private static string NewsDedupePrefix(Guid postId) => $"party-news:{postId}";` and use it in `NotifyCrewAsync`; extract the feed's projection for feed + edit; access check `access is null || !(IsCrew || IsPartyAdmin)` ⇒ `PartyNotFound`, `!IsPartyAdmin` ⇒ `Forbidden`; `EditAsync` as the event one, scoped `Id ∧ PartyId`, **with no notification statement** (comment: the rows quote no text, research R1); `DeleteAsync` through `CreateExecutionStrategy` + transaction: `ExecuteDelete post WHERE Id ∧ PartyId` → 0 ⇒ return before touching alerts (FR-013) → `DeleteManyAsync(NotificationType.PartyNews, NewsDedupePrefix(postId))` → commit; then `RefreshUnreadBadgesAsync` after commit. Log post ids only, never text
- [ ] T028 [US2] In `backend/Controllers/PartiesController.cs` add `EditNews` (`[HttpPatch("{id:guid}/news/{postId:guid}")]`: 200 / 403 `"Only a party admin can edit news."` / `PartyNotFound()` / `NewsPostNotFound()`) and `DeleteNews` (204 / 403 `"Only a party admin can delete news."` / 404 / 404), plus `NewsPostNotFound()`; run T024 until green
- [ ] T029 [P] [US2] In `frontend/apps/web/src/app/core/services/party.service.ts` add `editNews(id, postId, body): Observable<PartyNews>` and `deleteNews(id, postId): Observable<void>`; T025 green
- [ ] T030 [US2] In `frontend/apps/web/src/app/features/parties/party-news/party-news.component.{ts,html}`: each `<li jhCard padding="dense" overflowVisible>` wraps `<jh-news-post [canManage]="isAdmin()" [maxLength]="1000" editHint="news.editHint.party" deleteBody="news.deleteBody.party">` projecting the existing author/role/date line (marker `· {{ 'news.edited' | transloco }}` after the date when `editedDate`) and body (add `break-words`); arrow-function `save`/`remove`; `(saved)` replaces in `posts`, `(removed)` drops, `notice` → `news.gone` when `gone`, focus the `h1` (give it `id` + `tabindex="-1"`); notice `<p role="status" data-testid="news-notice">` above the list; the post error becomes `this.transloco.translate('parties.news.error')` only (research R11 — no server `detail`)
- [ ] T031 [US2] In `frontend/apps/web/src/app/features/parties/party-manage/party-manage.component.{ts,html}`: the same wiring for the inline news list; the `partyNewsLabel` `<p>` becomes an `<h2>` with the same classes plus `id` and `tabindex="-1"` (focus target, research R11); a `notice` above the list; `postNews`'s error handling unchanged unless it shows a server `detail`
- [ ] T032 [P] [US2] Write `frontend/apps/web/src/app/features/parties/party-news/party-news.component.spec.ts`: menus only for a party admin; the marker only on edited posts; save replaces; delete drops; 404 shows `news.gone`; a failed post shows the translated `parties.news.error`, never the server's `detail`

**Checkpoint**: party news is editable and deletable on both party pages; alerts follow the
delete; event news and the team page are unchanged.

---

## Phase 5: User Story 3 — Every copy tells the same story (Priority: P2)

**Goal**: Home's News module and "See all" mark edited event and party posts.

**Independent Test**: edit an event post and a party post; Home and `/home/news` mark both;
never-edited posts carry no marker.

- [ ] T033 [US3] In `backend/Services/Home/HomeService.cs` `LoadNewsAsync`: the event and party `NewsRaw` project `n.EditedDate` instead of `(DateTime?)null`; update the method's summary (all three sources carry the marker now)
- [ ] T034 [P] [US3] Add Home assertions: in `EventNewsEditDeleteTests.cs` an edited event post has `editedDate` on `/home/news` and in `/home`'s `news`; in `PartyNewsEditDeleteTests.cs` the same for a party post; rename 057's `Home_marks_an_edited_team_post_and_never_event_news` in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamNewsEditDeleteTests.cs` to `Home_marks_an_edited_team_post_and_nothing_never_edited` and correct its comment (research R7)
- [ ] T035 [P] [US3] In `frontend/apps/web/src/app/features/dashboard/modules/news-list.component.spec.ts` correct the header comment (every source can be edited now) and add a case: event and party items with an `editedDate` show the marker

**Checkpoint**: all three stories done.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T036 Copy `.specify/templates/ui-review-checklist-template.md` to `specs/059-event-party-news-edit-delete/checklists/ui-review.md` and answer each item against the diff and the walk's screenshots (DESIGN.md wins on conflict)
- [ ] T037 Browser walk per `quickstart.md` scenarios 1–11 (German, 375px + desktop, one Playwright context per actor, rebuilt images): screenshots of the menu on the last one-line post of all four surfaces (R9), the editor and dialog on the event page, party page and party news page, the marker on each and on Home, and the team page unchanged; read the driver's output and look at every screenshot; fix what it finds
- [ ] T038 Run the full suites: `dotnet test backend/JuggerHub.slnx`; in `frontend/`: `npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`; record results under **Notes**
- [ ] T039 [P] Add a one-line "Followed up by 059 (GH #367/#368)" note to the **Follow-ups** section of `specs/057-team-news-edit-delete/plan.md`
- [ ] T040 Record spec drift (if any) under **Notes**, commit in small phases referencing `#367 #368`, open the PR with `Closes #367` and `Closes #368`

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002–T014)** → US1 / US2 / US3 → **Polish**.
- Inside Foundational: T002/T003 → T004; T005/T006 → T007; T009 before T011/T012 (keys);
  T010 → T011 → T012 → T013; T014 after T011.
- **US1 and US2 are independent** of each other after Foundational (different services,
  controllers, pages). US3's backend (T033) is independent too; T034 extends the US1/US2 test
  files, so it runs after T015/T024 exist.
- Tests (T015, T024) are written first and seen failing before their implementation.

### Parallel opportunities

- T002 ∥ T003 ∥ T005 ∥ T006 ∥ T008 ∥ T010 (different files).
- US1 ∥ US2 once Foundational is done: T015 ∥ T024 ∥ T016 ∥ T025; T020 ∥ T029.
- T023 ∥ T032 ∥ T035 (separate spec files).

## Implementation Strategy

- **MVP**: Foundational + US1. Event news gets the controls, the team page runs on the shared
  component, and nothing about party news changes yet.
- Then US2 (party news, the one with alerts), then US3 (Home), then Polish with the walk.
- Commit per phase; the catalogue move (T009) and the team-page move (T011–T012) land in the same
  commit, so no commit ships a team page reading keys that no longer exist.

## Notes

- **T001 baseline** (2026-09-28, main at b3872bc): `dotnet build backend/JuggerHub.slnx` 0 warnings / 0 errors; `nx test web --testPathPatterns="team-detail|news-list|catalog-"` 7 suites / 55 tests green. No pre-existing failure.
- **Design refined in T010/T011 (spec drift: none; plan drift: recorded in research R8)**: `NewsPostEditing` is provided by each page and holds the open editor *and* its draft, not a root lock. The team page rebuilds its whole content behind a spinner on every reload (approving a join request is one), and 057 deliberately kept the editor and its text across that. A per-instance state would have lost the typed text; a root service would leak an open edit onto the next page. The lock counts only a post that is on screen.
- **T013**: `team-detail.component.spec.ts` unedited, 33/33 green after the move.
- **T014**: 14 tests; mutation-checked the one-menu-open case (the generic `closest("[data-news-menu]")` check turns it red).
