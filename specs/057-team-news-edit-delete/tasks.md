---

description: "Task list for 057 — team news posts can be edited and deleted"
---

# Tasks: Team News Posts Can Be Edited and Deleted

**Input**: Design documents from `specs/057-team-news-edit-delete/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/team-news-api.md](./contracts/team-news-api.md), [quickstart.md](./quickstart.md)

**Tests**: Included. The plan's test strategy (research R13) is part of the design, not an
option: the no-re-notify guarantees (FR-005/FR-010) and the Principle III `ModifiedDate`
obligation are only provable by tests.

**Organization**: by user story. US1 = edit (P1, MVP), US2 = delete (P1), US3 = every copy tells
the same story (P2: the Alerts-row refresh and Home's marker).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1 / US2 / US3 (setup, foundational and polish carry none)

---

## Phase 1: Setup

**Purpose**: know the baseline before touching anything, so a later failure is attributed to
the right change (the cross-PR attribution lesson).

- [X] T001 On the untouched branch, run `dotnet build backend/JuggerHub.slnx` and, in `frontend/`, `npx nx test web --watch=false --testPathPatterns="team-detail|team.service|catalog-"`; record any pre-existing failure under **Notes** at the end of `specs/057-team-news-edit-delete/tasks.md`

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: the column, the widened DTOs, the notification engine's three operations, the
test fake, the frontend model and all copy. Every story builds on these.

**⚠️ No user-story work starts before this phase's checkpoint.**

- [X] T002 [P] In `backend/Entities/TeamNewsPost.cs` add `public DateTime? EditedDate { get; set; }` and rewrite the class summary to describe the entity as it is (research R4, R11): admins post (010); any admin may edit or delete any post (057); `EditedDate` = when a person last changed the text, `null` = never edited, deliberately **not** derived from `ModifiedDate` (which moves on any write). Delete the stale "later iteration" and "seeded in Development" sentences.
- [X] T003 Generate the migration **with a build** (056 lesson: `--no-build` emitted an empty migration): `dotnet ef migrations add AddTeamNewsEditedDate --project backend --startup-project backend --output-dir Data/Migrations`. Then read `backend/Data/Migrations/*_AddTeamNewsEditedDate.cs`: exactly one nullable `AddColumn<DateTime>(name: "EditedDate", table: "TeamNewsPosts", type: "timestamp with time zone", nullable: true)` in `Up`, one `DropColumn` in `Down`, no SQL, no default, snapshot updated (depends on T002)
- [X] T004 [P] In `backend/Dtos/Teams/TeamDtos.cs`: `TeamNewsDto` becomes `(Guid Id, string AuthorDisplayName, string? AuthorHandle, TeamRole AuthorRole, DateTime CreatedDate, DateTime? EditedDate, string Body)` (keep the `AuthorHandle` comment; document `Id` and `EditedDate`); add `EditTeamNewsRequest([Required, MinLength(1), MaxLength(1000)] string Body)` beside `PostTeamNewsRequest`, documented as the same rules as posting (FR-002)
- [X] T005 In `backend/Services/Teams/TeamNewsService.cs`: extract `GetFeedAsync`'s `.Select` into one private projection (`IQueryable<TeamNewsDto> Project(IQueryable<TeamNewsPost> posts, string placeholder)`) that also maps `n.Id` and `n.EditedDate`, keeping the placeholder and current-role subquery exactly as they are; `GetFeedAsync` uses it; `PostAsync`'s hand-built DTO passes `post.Id` and `EditedDate: null` (depends on T002, T004)
- [X] T006 [P] In `backend/Services/Notifications/INotificationService.cs` and `NotificationService.cs` add, per [contracts](./contracts/team-news-api.md#internal-contract-inotificationservice-additions): `ReplacePayloadAsync(type, dedupeKeyPrefix, payload, ct)` (serialize with the existing `PayloadJson`; `ExecuteUpdate` `Payload` **and `ModifiedDate`** where `Type == type && DedupeKey != null && DedupeKey.StartsWith(dedupeKeyPrefix + ":")`; returns rows changed; no realtime), `DeleteManyAsync(type, dedupeKeyPrefix, ct)` (select distinct `RecipientUserId` of matching **unread** rows, then `ExecuteDelete` all matching rows; returns those recipients; no realtime) and `RefreshUnreadBadgesAsync(recipientUserIds, ct)` (loop the existing best-effort `PushUnreadCountAsync` over distinct ids). XML docs state which are transaction-safe and that the prefix is the same string given to `CreateManyAsync`. **No new name starts with `Push`** (055)
- [X] T007 [P] Create `backend/tests/JuggerHub.Api.IntegrationTests/Notifications/FakeNotificationRealtime.cs`: a recording `INotificationRealtime` (lists `Created(RecipientUserId, NotificationDto)` and `UnreadCount(RecipientUserId, Count)` behind a lock, mirroring `Chat/FakeChatRealtime.cs`); register it in `backend/tests/JuggerHub.Api.IntegrationTests/JuggerHubApiFactory.cs` (`RemoveAll<INotificationRealtime>()` + `AddSingleton`) and expose it as `public FakeNotificationRealtime NotificationRealtime { get; }` beside `ChatRealtime`
- [X] T008 [P] In `frontend/apps/web/src/app/core/models/team.models.ts` add `id: string` and `editedDate: string | null` (ISO, null = never edited) to `TeamNews`; in `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.html` change the news `@for` from `track $index` to `track n.id` (rows will vanish; index tracking would move an open editor onto the neighbour)
- [X] T009 [P] Add **all** new copy to `frontend/apps/web/public/i18n/en.json`, `de.json` and `es.json` in one change (`catalog-parity` fails otherwise). Under `teams.detail`: `newsManage`, `newsEdit`, `newsDelete`, `newsEdited`, `newsEditLabel`, `newsEditHint`, `newsSaving`, `newsSaveFailed`, `newsGone`, `newsDeleteTitle`, `newsDeleteBody`, `newsDeleteKeep`, `newsDeleteConfirm`, `newsDeleting`, `newsDeleteFailed`; under `home`: `newsEdited`. Terms per research R9 (EN *post*, DE *Beitrag*, ES *novedad* → *editada*; Alerts = *Meldungen* / *Avisos*); errors open "We couldn't" / "Wir konnten" / "No pudimos"; German `–` never `—`; the delete body states both FR-008 facts. Then run `npx nx test web --watch=false --testPathPatterns="catalog-"` in `frontend/`
- [X] T010 Checkpoint: `dotnet build backend/JuggerHub.slnx`, then `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~NotificationTests|FullyQualifiedName~PostErasureReadPath|FullyQualifiedName~HomeTests|FullyQualifiedName~NewsPartyTests|FullyQualifiedName~EmailEncodingTests"` (the migration applies and the widened DTOs break nothing); commit `feat(057): EditedDate column, widened news DTOs, notification row operations (#363)`

**Checkpoint**: foundation ready. US1 and US2 can proceed; US3 needs US1's `EditAsync`.

---

## Phase 3: User Story 1 — An admin corrects a post (Priority: P1) 🎯 MVP

**Goal**: any admin can change a post's text in place on the team page; the post keeps its
place, author and date, is marked *edited*, and nobody is notified.

**Independent test**: post, edit, and check that the team page shows the new text marked
edited in the same position, and that no member received an alert, email, push or live event.

### Tests for User Story 1 (write first, see them fail)

- [X] T011 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamNewsEditDeleteTests.cs` (`[Collection("Teams")]`) with helpers (new verified user, create team, join by link, promote to admin via `PATCH …/members/{id}/role`, post news, list notifications, unread count) and US1 facts: the author edits → 200, body trimmed, `editedDate` set, `id`/`createdDate`/author/feed position unchanged, `ModifiedDate` moved (read via `HomeTestSupport.WithDbAsync`); **another admin edits someone else's post → 200** (FR-013); a plain member → 403; a non-member and an unknown slug → 404 with title "Team not found"; a post id belonging to another team, under this team's slug, by an admin of both → 404 "News post not found"; a demoted author → 403; `""`, whitespace-only and 1,001 characters → 400, post unchanged; the same text with surrounding spaces → 200 and `editedDate` stays `null`; **nothing is sent**: no new `Notification` row for anyone, `Factory.EmailSender.LatestFor(memberEmail)` is null after `Clear()`, `Factory.PushDispatcher` recorded no new dispatch, `Factory.NotificationRealtime` recorded no `Created`; `POST` returns `id` and `editedDate: null`

### Implementation for User Story 1

- [X] T012 [US1] In `backend/Services/Teams/ITeamNewsService.cs` add `TeamNewsEditStatus { Updated, NotFoundOrNotMember, Forbidden, PostNotFound }`, `TeamNewsEditResult(TeamNewsEditStatus Status, TeamNewsDto? Post)` and `EditAsync(string slug, Guid postId, Guid actorUserId, string body, CancellationToken ct = default)`, documented (any admin, any post; unchanged text is a no-op; nothing is sent)
- [X] T013 [US1] Implement `EditAsync` in `backend/Services/Teams/TeamNewsService.cs`: guard → member (else `NotFoundOrNotMember`) → admin (else `Forbidden`) → current body `AsNoTracking` where `Id == postId && TeamId == team` (absent ⇒ `PostNotFound`) → trimmed text equal ⇒ `Updated` with the projected current DTO and **no write** → otherwise `_db.Database.CreateExecutionStrategy().ExecuteAsync` { `BeginTransactionAsync`; `ExecuteUpdate` `Body`, `EditedDate` = now, **`ModifiedDate` = now** where `Id && TeamId`; 0 rows ⇒ `PostNotFound` without commit; *(US3 adds the Alerts refresh here, before commit)*; `CommitAsync` } → return the projected DTO. Comment the transaction's reason (research R3). Log the post id only, never the text (depends on T005, T012)
- [X] T014 [US1] In `backend/Controllers/TeamsController.cs` add `[HttpPatch("{slug}/news/{postId:guid}")] EditNews(string slug, Guid postId, [FromBody] EditTeamNewsRequest request, CancellationToken ct)` mapping `Updated` → 200 + DTO, `Forbidden` → `Forbidden("Only admins can edit team news.")`, `PostNotFound` → a new `NewsPostNotFound()` helper (404, title "News post not found", detail "That post doesn't exist, or was deleted."), otherwise `TeamNotFound()`; doc comment (depends on T013)
- [X] T015 [P] [US1] In `frontend/apps/web/src/app/core/services/team.service.ts` add `editNews(slug: string, postId: string, body: string): Observable<TeamNews>` → `PATCH …/teams/{slug}/news/{postId}` with `{ body }`; add a case to `frontend/apps/web/src/app/core/services/team.service.spec.ts` (method, URL encoding, body)
- [X] T016 [US1] Team page edit flow in `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.ts` + `.html` (all new state as **signals**, since the app is zoneless): `newsMenu`, `editingNewsId`, `newsDraft`, `savingNews`, `newsEditError`, `newsNotice`. Each post becomes `flex items-start gap-sm`: a `min-w-0 flex-1` column (body `whitespace-pre-line break-words text-body-sm text-body`, meta `authorDisplayName · date` + ` · {{ 'teams.detail.newsEdited' | transloco }}` when `n.editedDate`), and **for admins** a `relative` wrapper holding the `ellipsis` trigger (`aria-label` `teams.detail.newsManage`, `aria-haspopup="menu"`, `[attr.aria-expanded]`, the roster trigger's classes) and a `role="menu"` surface with a `role="menuitem"` **Edit** item (focus-visible ring, like `profile-quick-actions`). Opening the editor replaces the body with a textarea (composer classes, `maxlength="1000"`, `aria-label` `newsEditLabel`, value bound to `newsDraft`), the caption hint `newsEditHint`, an inline error line (`role="alert"`), a `ghost` **Cancel** (`common.cancel`) and a **`secondary`** **Save** (`common.save` / `newsSaving`, disabled while empty or saving). The textarea is focused via `afterNextRender`. Unchanged trimmed text closes without a request. Success replaces the post with the response. A **404** removes the post and sets `newsNotice` (`newsGone`, `role="status"`, rendered under the News heading). Any other error keeps the editor and the text and shows `newsSaveFailed`, **never `problemDetail`'s server text** (#179). Escape closes an open menu (extend the existing `document:keydown.escape` handler); a `document:click` outside `[data-news-menu]` closes it. Only one of menu or editor is open at a time (depends on T008, T009, T015)
- [X] T017 [US1] Extend `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.spec.ts` with a news block (mock `getNews`, `editNews`): a plain member sees no post menu; an admin sees one per post; Edit → Save sends the trimmed text and the post shows the new body plus "edited"; Cancel and an unchanged Save send nothing; a failed save keeps the typed text and shows the error; a 404 removes the post and shows the notice (depends on T016)
- [X] T018 [US1] Run `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~TeamNewsEditDelete"` and, in `frontend/`, `npx nx test web --watch=false --testPathPatterns="team-detail|team.service"`; commit `feat(057): admins can edit team news posts (#363)`

**Checkpoint**: US1 works on its own (the Alerts rows still hold the old excerpt until US3).

---

## Phase 4: User Story 2 — An admin removes a post (Priority: P1)

**Goal**: any admin can delete any post behind a confirmation; it disappears from the team
page, Home and every recipient's Alerts inbox, and an unread recipient's badge drops.

**Independent test**: post, delete through the dialog, and check that the post is absent
everywhere and that an unread recipient's count fell by one, live.

### Tests for User Story 2 (write first)

- [X] T019 [P] [US2] Add delete facts to `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamNewsEditDeleteTests.cs`: delete → 204; the post is absent from `GET …/news` and from the member's `GET /api/v1/home/news`; the `TeamNews` rows for the post are gone for a current member **and for a member who left the team afterwards**; the unread member's count fell by exactly one, and `Factory.NotificationRealtime` recorded an unread-count update for them with the lower value, but none for a member who had already read theirs; a second delete → 404 "News post not found"; an edit after the delete → 404; a plain member → 403; a non-member → 404 "Team not found"; **another team's post under this slug (admin of both) → 404, and that team's post and its members' rows are untouched**; nothing is sent (no email, no push dispatch, no `Created`)

### Implementation for User Story 2

- [X] T020 [US2] In `backend/Services/Teams/ITeamNewsService.cs` add `TeamNewsDeleteStatus { Deleted, NotFoundOrNotMember, Forbidden, PostNotFound }` and `DeleteAsync(string slug, Guid postId, Guid actorUserId, CancellationToken ct = default)`, documented (hard delete; removes the post's Alerts rows; emailed copies are out of reach)
- [X] T021 [US2] Implement `DeleteAsync` in `backend/Services/Teams/TeamNewsService.cs`: guard → admin → `CreateExecutionStrategy().ExecuteAsync` { begin; `ExecuteDelete` where `Id == postId && TeamId == team`; 0 rows ⇒ `PostNotFound` without commit; `recipients = DeleteManyAsync(NotificationType.TeamNews, $"news:{postId}")`; commit } → **after commit** `RefreshUnreadBadgesAsync(recipients)`. The notification statement runs only after the team-scoped post delete matched (FR-012, comment why). Log the post id only (depends on T006, T020)
- [X] T022 [US2] In `backend/Controllers/TeamsController.cs` add `[HttpDelete("{slug}/news/{postId:guid}")] DeleteNews` mapping `Deleted` → 204, `Forbidden` → `Forbidden("Only admins can delete team news.")`, `PostNotFound` → `NewsPostNotFound()`, otherwise `TeamNotFound()` (depends on T014, T021)
- [X] T023 [P] [US2] In `frontend/apps/web/src/app/core/services/team.service.ts` add `deleteNews(slug: string, postId: string): Observable<void>` → `DELETE …/teams/{slug}/news/{postId}`, plus a case in `team.service.spec.ts`
- [X] T024 [US2] Team page delete flow in `team-detail.component.ts` + `.html`: signals `deleteNewsTarget`, `deletingNews`, `newsDeleteError`; a **Delete** `role="menuitem"` (danger text, like the roster's *Remove*) opens a dialog copied from the page's join-confirm markup (scrim `bg-surface-inverse/40`, bottom sheet under `sm`, `role="dialog"`, `aria-modal="true"`, `aria-labelledby` its title): title `newsDeleteTitle`, body `newsDeleteBody`, inline error (`role="alert"`), `secondary` **Keep** (`newsDeleteKeep`) that receives focus on open via `afterNextRender`, and `jhButton variant="danger"` **Delete** (`newsDeleteConfirm` / `newsDeleting`, disabled while deleting). Success or **404** removes the post from `news()` (404 also sets `newsNotice`) and closes the dialog. Any other error keeps the dialog open with `newsDeleteFailed`. Escape dismisses it unless a delete is in flight (depends on T016, T023)
- [X] T025 [US2] Extend `team-detail.component.spec.ts`: Delete opens the dialog with focus on Keep; Keep closes it without a request; confirm calls `deleteNews` and removes the post; a 404 removes it and shows the notice; a failure keeps the dialog and shows the error (depends on T024)
- [X] T026 [US2] Run the US2 backend facts and the frontend specs as in T018; commit `feat(057): admins can delete team news posts (#363)`

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 — Every copy tells the same story (Priority: P2)

**Goal**: after an edit, the Alerts rows already delivered show the corrected opening text
(silently), and Home's news module and "See all" mark the post *edited*; event and party items
never carry the marker.

**Independent test**: edit a post, then check a recipient's Alerts row (same place, same read
state, new text, a former member's too) and Home's news items.

### Tests for User Story 3 (write first)

- [X] T027 [P] [US3] Add to `TeamNewsEditDeleteTests.cs`: after an edit, the `TeamNews` row's `payload.excerpt` equals the corrected text's excerpt for an unread member, a read member and a **member who left** before the edit; each row's `isRead` and its position in `GET /api/v1/notifications` are unchanged; unread counts unchanged; the rows' `ModifiedDate` moved (Gate 2, via `WithDbAsync`); `Factory.NotificationRealtime` recorded nothing; the member's `GET /api/v1/home/news` item for the post has the corrected `body` and a non-null `editedDate`, while an event news item (`HomeTestSupport.SeedEventAsync` + `SignupUserAsync` + `AddEventNewsAsync`) has `editedDate: null`, as does a never-edited team post

### Implementation for User Story 3

- [X] T028 [US3] In `EditAsync` (`backend/Services/Teams/TeamNewsService.cs`), inside the same transaction after the post update and before commit, call `_notifications.ReplacePayloadAsync(NotificationType.TeamNews, $"news:{postId}", new TeamNewsPayload(team.Slug, team.Name, postId, Excerpt(trimmed)), ct)`; read the team's slug and name before the strategy (depends on T006, T013)
- [X] T029 [P] [US3] Home backend: `HomeProjections.NewsRaw` gains `DateTime? EditedDate` (`backend/Services/Home/HomeProjections.cs`); `HomeNewsDto` gains `DateTime? EditedDate` with a comment that it is team-only (`backend/Dtos/Home/HomeDtos.cs`); `HomeNewsMerge.Merge` passes it through (`backend/Services/Home/HomeNewsMerge.cs`); `HomeService.LoadNewsAsync` projects `n.EditedDate` for team news and `(DateTime?)null` for event and party news (`backend/Services/Home/HomeService.cs`)
- [X] T030 [P] [US3] Frontend Home: `HomeNews.editedDate: string | null` in `frontend/apps/web/src/app/core/models/home.models.ts`; in `frontend/apps/web/src/app/features/dashboard/modules/news-list.component.html` append ` · {{ 'home.newsEdited' | transloco }}` to the meta line when `item.editedDate`; switch its `track $index` to a stable key only if one exists (`sourceSlugOrId` is not unique per item, so leave `$index`); create `frontend/apps/web/src/app/features/dashboard/modules/news-list.component.spec.ts` (marker shown for an edited team item, absent for a never-edited one and for an event item)
- [X] T031 [US3] Run `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~TeamNewsEditDelete|FullyQualifiedName~HomeTests|FullyQualifiedName~NewsPartyTests"` and, in `frontend/`, `npx nx test web --watch=false --testPathPatterns="news-list|team-detail"`; commit `feat(057): alerts and Home follow an edited post (#363)`

**Checkpoint**: all three stories work.

---

## Phase 6: Polish & cross-cutting

- [ ] T032 Instantiate `specs/057-team-news-edit-delete/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and add feature items under *Feature-specific UI*: the post menu's ARIA wiring and Escape/outside-click; still **one coral CTA** with the editor open (Save is `secondary`); the dialog's initial focus on *Keep*, danger confirm and scrim token; the marker sits in the meta line at `caption`/`text-muted` on the team page **and** Home; no truncation of the hint, buttons or dialog copy in German at 375px; long unbroken text wraps (`break-words`). Verify every item against the diff; answer the layout items from T033's screenshots
- [ ] T033 Browser walk per [quickstart.md](./quickstart.md) → *Manual scenarios*: `docker compose up -d --build backend frontend`; a temporary Playwright script **inside `frontend/`** (register → Mailpit verify → sign in → dismiss onboarding, locale-agnostic waits, **one browser context per actor**) drives scenarios 1–10 at **375px and desktop in German**, saving screenshots of the menu, the open editor, the dialog, the marker on the team page and on Home, and an Alerts row before and after an edit and after a delete. Read the driver's output and **look at every screenshot** (false passes happen). Fix what the walk finds, re-walk, then delete the script
- [ ] T034 Full verification from the repo root: `dotnet build backend/JuggerHub.slnx`, `dotnet test backend/JuggerHub.slnx`; in `frontend/`: `npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`. Record results (and any failure with its output) for the PR
- [ ] T035 [P] File two follow-up issues with `gh issue create --body-file <scratchpad file>` (labels `enhancement`, `backend`, `frontend`): **Event news posts cannot be edited or deleted** and **Party news posts cannot be edited or deleted**. Each points at this feature, research R2 (the three `INotificationService` operations are producer-agnostic; party news already uses the prefix `party-news:{postId}`) and asks the owner to confirm the any-admin rule for that source
- [ ] T036 Commit the remaining changes (checklist, fixes from the walk) referencing #363, push `057-team-news-edit-delete`, and open the PR (`Closes #363`) with a summary, the two owner decisions, the German 375px/desktop screenshots, verification results, the recorded residuals, and a call-out of the one visible change to existing posts (line breaks now render)

---

## Dependencies & execution order

### Phase dependencies

- **Setup (T001)** → **Foundational (T002–T010)** → user stories → **Polish (T032–T036)**.
- **US1 (T011–T018)** and **US2 (T019–T026)** both need only the foundation. US2's controller
  task sits beside US1's in the same file (T022 after T014), and its UI extends the menu US1
  creates (T024 after T016). Done sequentially in that order, neither blocks the other's
  *testing*.
- **US3 (T027–T031)** needs US1's `EditAsync` (T028 edits it). Its Home half (T029, T030) is
  independent of everything but the foundation.

### Within each story

Tests first (and failing) → interface → service → controller → frontend service → component →
component spec → run + commit.

### Parallel opportunities

- Foundational: T002, T004, T006, T007, T008 and T009 touch different files and can run
  together. T003 waits for T002, and T005 for T002 and T004.
- US1: T011 (backend tests) and T015 (frontend service) run in parallel with each other and with
  T012.
- US2: T019 and T023 in parallel.
- US3: T027, T029 and T030 in parallel; T028 is the only task touching `TeamNewsService.cs`.
- Polish: T035 runs any time after the plan; T032 and T033 go together.

## Parallel example: foundation

```text
Task: "T002 EditedDate on TeamNewsPost.cs"
Task: "T004 TeamNewsDto + EditTeamNewsRequest in TeamDtos.cs"
Task: "T006 three INotificationService operations"
Task: "T007 FakeNotificationRealtime + factory registration"
Task: "T008 TeamNews model + track n.id"
Task: "T009 all copy × 3 catalogues"
```

## Implementation strategy

**MVP = Phase 1 + 2 + US1**: an admin can fix a post, and it says *edited*. It ships value on its
own, but the feature is delivered as one PR with all three stories, because US2 (the
wrong-team case) and US3 (stale Alerts text) are what the issue is actually about.

Commit at each checkpoint (T010, T018, T026, T031, T036) so every commit builds and its tests
pass.

## Notes

- Keep every write `ExecuteUpdate`/`ExecuteDelete` inside the strategy, and set
  **`ModifiedDate`** on both update paths. That's the likeliest review failure (Gate 2).
- Never log post text, excerpts, author names or team names (Principle VII).
- Never show `problemDetail(err)` text for these calls; branch on status (#179).
- Baseline (T001, 2026-09-28, `main` @ `6eba124`): `dotnet build backend/JuggerHub.slnx` →
  0 warnings, 0 errors. `npx nx test web --testPathPatterns="team-detail|team.service|catalog-"`
  → 7 suites, 40 tests, all passing. No pre-existing failure in the touched areas.
