# Implementation Plan: Event and Party News Posts Can Be Edited and Deleted

**Branch**: `059-event-party-news-edit-delete` | **Date**: 2026-09-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/059-event-party-news-edit-delete/spec.md` (GH #367 + GH #368)

## Summary

Event news and party news are permanent today: `EventNewsService` and `PartyNewsService` read and
post, nothing else. This feature adds **`PATCH` and `DELETE`** for both
(`/events/{id}/news/{postId}`, `/parties/{id}/news/{postId}`) on 057's rules: **any current
admin, any post** (owner decision), a nullable **`EditedDate`** marker on each post's page and on
Home, a silent edit, and a hard delete behind a confirmation. **Event news** creates no alerts
and sends nothing, so both its operations are one statement. **Party news** alerts carry **no
copy of the post**, so an edit touches only the post, and a delete **removes the post's alerts**
for every recipient, former crew included, in one transaction with the post (owner decision),
then refreshes badges. The edit/delete controls become **one shared `jh-news-post`
component**, used by the team page (moved onto it), the event page, the party page and the party
news page (owner decision).

**The load-bearing decisions, all found by reading:**

1. **#368's premise is wrong: party-news alerts quote nothing.** The payload is
   `{partyId, eventId, teamSlug, eventName, teamName}` and the push is a fixed sentence. So an edit
   must **not** rewrite the rows (no-op writes that move `ModifiedDate`, the 056 defect class), and
   the "refresh on edit?" question disappears (research R1).
2. **Party alerts are found by the prefix `party-news:{postId}`**, never by the crew. The prefix
   reaches people who have since left, and it is spelled once in the service for both writer and
   deleter (R2). Party delete = 057's R3 transaction exactly. Event edit/delete and party edit
   are single statements (R3).
3. **Outsiders get the feed's 404.** Party news is crew-private, so edit/delete answer a
   non-crew caller (team members outside the crew included) with the **same** "Party not found"
   as reading, never a 403. Event news is readable by every signed-in player, so a non-admin gets
   403, as posting does (R5). Party news gets its **own** status enums, because `PartyOutcome` is
   shared with the market and both `Fail` switches end in a catch-all 400 (R5).
4. **The shared component owns behaviour, the host owns layout.** The host projects the body and
   meta line. The component takes `save`/`remove` **functions** and emits `saved`/`removed`, so
   it knows no service. The editing state (open editor + typed draft) is a **page-provided**
   `NewsPostEditing`, so a page that rebuilds its list mid-edit (the team page reloads behind a
   spinner) keeps it, as 057 did; the lock counts only a post on screen.
   Every 057 `data-testid` is kept, so **team-detail's existing news tests are the regression net**
   (R8).
5. **Cards clip the menu.** `jh-card` is `overflow: hidden` and the party pages render one card
   per post, so every card hosting a post menu gets `overflowVisible` (the roster idiom). The
   fixed-position dialog is not clipped: no ancestor on the four surfaces has a
   transform/filter/contain (R9).

**Size**: 2 columns + 1 migration, 4 endpoints, 4 service methods, 2 DTO fields, 1 shared
component (+ page-scoped editing service), 3 host pages adopt it and the team page is refactored onto it,
~19 i18n keys × 3 (13 moved, 6 new). **No new entity, no new dependency, no new configuration, no
infrastructure change, no `INotificationService` change.**

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (zoneless, signals) with Nx (frontend)

**Primary Dependencies**: ASP.NET Core, EF Core + Npgsql (execution strategy with
`EnableRetryOnFailure`), SignalR (the existing notifications hub), Transloco (en/de/es). No new
package.

**Storage**: PostgreSQL 18. One nullable column each on `EventNewsPosts` and `PartyNewsPosts`.
`Notifications` read and deleted through existing columns only.

**Testing**: xUnit integration tests against the real API and a Postgres Testcontainer
(`JuggerHubApiFactory` with `TestEmailSender`, `FakePushDispatcher`, `FakeNotificationRealtime`);
Jest 30 (`--testPathPatterns`); the catalogue guards; a real-browser walk (German, 375px +
desktop).

**Target Platform**: Linux containers on AKS (Dev/Prod) and docker compose locally; evergreen
browsers, the installed PWA included.

**Project Type**: Web application (`backend/` + `frontend/apps/web`).

**Performance Goals**: Nothing new at scale. Rare admin actions on one post; a party delete touches
at most that post's recipients (≈ one crew).

**Constraints**: Hard delete, no history, no notification of any kind on edit or delete, no browser
auto-retry of the mutations. German at 375px is the binding layout case. One coral CTA per view.
Team news behaviour must not change (FR-023).

**Scale/Scope**: 2 entities, 2 services, 2 controllers, Home's news loader; 1 shared component,
4 host surfaces (team page, event news feed, party page, party news page).

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1 design. Constitution v1.4.0.*

| Gate | Verdict | Notes |
|------|---------|-------|
| **I. Security-first, never trust the client** | ✅ Pass | All four endpoints decided server-side in a fixed order: resolve event/party + caller role → refuse → post **scoped to the addressed event/party** (`Id ∧ EventId` / `Id ∧ PartyId`). Unknown event and unknown party answer exactly as their feeds do. A **non-crew caller gets the feed's 404, not a 403**, so edit/delete add no existence oracle to a crew-private resource (SC-004). Another event's/party's post id is a 404 even for an admin of both (FR-013). The alerts statement is reachable only after the post statement matched a row of the caller's party. The UI menu is convenience; errors are problem details and the client shows translated copy only, which also fixes both party pages rendering the server's English `detail` (R11). |
| **II. Thin controllers, service-centric** | ✅ Pass | Four controller actions that forward and map statuses (the `EditNews`/`DeleteNews` shape of `TeamsController`). Logic in `EventNewsService`/`PartyNewsService` behind their interfaces. One shared `Project(...)` per service for feed + edit response, explicit `.Select`, no mapper. |
| **III. Disciplined data access** | ⚠️ Pass **with an obligation** | Every write is `ExecuteUpdateAsync`/`ExecuteDeleteAsync`, so **`ModifiedDate` MUST be set explicitly on both edit paths** (event, party). Most likely gate failure; a test asserts each. Party edit deliberately writes **nothing** to `Notifications` (R1). Feeds stay paginated; reads projected + `AsNoTracking`. |
| **IV. Auth & sessions** | ✅ Pass | Untouched; endpoints under the controllers' existing `[Authorize(JwtBearer)]`. |
| **V. Environment parity** | ✅ Pass | One EF migration, applied the same way everywhere. No configuration, no secret. |
| **VI. Conventions & tooling** | ✅ Pass | The new component and every new spec keep `.html`/`.css`/`.ts` separate. No script added. |
| **VII. Resilient by default** | ✅ **Not engaged as an integration** | No outbound call (nothing emailed or pushed on edit/delete). Party delete is a multi-step write and runs through the execution strategy with all mutation inside the delegate, fixed-value statements (replay converges), and the badge refresh after commit (R3). The three single-statement operations rely on the provider strategy's own retry. The browser never retries `PATCH`/`DELETE` (retry interceptor: `GET`/`HEAD` only); "try again" is a press. Adding retry/backoff/breaker is review-rejectable (R12). |
| **Gate 7 — UI/design compliance** | ✅ **Engaged** | New controls on three surfaces, a refactor of a fourth, a marker in four places, ~19 strings × 3 → `checklists/ui-review.md` from the template, verified against screenshots. Binding cases in **German at 375px**: the party post card's menu (clipping, R9), the editor's hint and buttons in the narrow party card, the dialog as a bottom sheet, and each meta line with *· bearbeitet*. |
| **Gate 8 — Resilience review** | ✅ Pass | See VII; the only item it asks of this diff is the party-delete transaction, designed in. |

**Post-Phase-1 re-evaluation**: unchanged. The design kept to two columns and four endpoints and
added no entity, dependency, configuration or change to the notification engine. The Principle
III obligation is written into R3, the data model, the contract and the test plan.

**Complexity Tracking**: one item worth stating, not a violation. The team page is refactored
although neither issue asks for it; this is the owner's choice (spec → Clarifications), guarded by
FR-023/SC-007 and by 057's unchanged team-detail tests.

## Project Structure

### Documentation (this feature)

```text
specs/059-event-party-news-edit-delete/
├── plan.md                  # This file
├── spec.md                  # FR-001…FR-024, SC-001…SC-007, owner clarifications
├── research.md              # R1–R14
├── data-model.md            # EditedDate ×2; alert rows on party delete; widened DTOs
├── quickstart.md            # Automated checks + the German 375px/desktop walk
├── contracts/
│   └── news-api.md          # 4 endpoints, widened DTOs, internal interfaces, jh-news-post contract
├── checklists/
│   ├── requirements.md      # Spec quality (done)
│   └── ui-review.md         # Gate 7, created during implementation
└── tasks.md                 # /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── Entities/EventNewsPost.cs, PartyNewsPost.cs                # + EditedDate; summaries rewritten (R11)
├── Data/Migrations/<ts>_AddEventAndPartyNewsEditedDate.cs (+Designer, snapshot)
├── Dtos/Events/EventDtos.cs                                   # EventNewsDto + EditedDate; EditEventNewsRequest
├── Dtos/Parties/PartyDtos.cs                                  # PartyNewsDto + EditedDate; EditPartyNewsRequest
├── Services/Events/IEventNewsService.cs, EventNewsService.cs  # EditAsync, DeleteAsync, statuses; shared projection
├── Services/Parties/IPartyNewsService.cs, PartyNewsService.cs # EditAsync, DeleteAsync, statuses; prefix helper
├── Services/Home/HomeService.cs                               # event + party project n.EditedDate
├── Controllers/EventsController.cs                            # EditNews (PATCH), DeleteNews (DELETE), NewsPostNotFound()
├── Controllers/PartiesController.cs                           # EditNews (PATCH), DeleteNews (DELETE), NewsPostNotFound()
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Events/EventNewsEditDeleteTests.cs                     # NEW
    ├── Parties/PartyNewsEditDeleteTests.cs                    # NEW
    └── Teams/TeamNewsEditDeleteTests.cs                       # one test renamed (R7)

frontend/apps/web/
├── public/i18n/{en,de,es}.json                                # news.* (moved + new); teams.detail.news* removed
└── src/app/
    ├── shared/news-post/news-post.component.{ts,html,css,spec.ts}   # NEW shared controls
    ├── shared/news-post/news-post-editing.ts                         # NEW page-scoped editing state (provided by each page)
    ├── core/models/event.models.ts, party.models.ts                  # + editedDate
    ├── core/services/event.service.ts (+ NEW .spec.ts)               # editNews, deleteNews
    ├── core/services/party.service.ts (+ NEW .spec.ts)               # editNews, deleteNews
    ├── features/teams/team-detail/team-detail.component.{ts,html}    # onto jh-news-post (spec unchanged)
    ├── features/events/event-detail/event-detail.component.{ts,html} # [(news)] two-way, eventId, canManage
    ├── features/events/event-detail/components/news-feed.component.{ts,html,spec.ts}   # jh-news-post, marker, notice (+ NEW spec)
    ├── features/parties/party-news/party-news.component.{ts,html,spec.ts}              # jh-news-post, marker, notice (+ NEW spec)
    ├── features/parties/party-manage/party-manage.component.{ts,html}                  # same, h2 label
    └── features/dashboard/modules/news-list.component.spec.ts        # header fixed; event/party marker cases
```

**Structure Decision**: the existing web-application layout. The shared component sits beside
`shared/address-fields` and `shared/city-picker`, the other cross-feature composites, not in
`shared/ui`, which holds design-system primitives. Every other change lands in a file that
already owns the concern.

## Implementation approach

### Backend

1. **Entities + migration.** `EditedDate` on both. Summaries rewritten: `EventNewsPost` read by
   signed-in players (not "public", 026), both posted by admins and editable/deletable by any
   current admin (059), `EditedDate` meaning *a person changed the text*, not `ModifiedDate`.
   Generate `AddEventAndPartyNewsEditedDate` **with a build**, then read it: two nullable
   `AddColumn`s, nothing else.
2. **Event news service.** Extract one `Project(IQueryable<EventNewsPost>, placeholder)`
   (author: profile display name or `MemberPlaceholder`; `EditedDate`) used by `GetFeedAsync`,
   `PostAsync` (drops the English `"An organiser"`, R11) and `EditAsync`.
   - `EditAsync`: guard → null ⇒ `EventNotFound`; not admin ⇒ `Forbidden`; read current body
     `WHERE Id ∧ EventId` (`AsNoTracking`) → absent ⇒ `PostNotFound`; trimmed equal ⇒ `Updated`
     with the current DTO, **no write**; else `ExecuteUpdate(Body, EditedDate, ModifiedDate)` →
     0 rows ⇒ `PostNotFound`; project and return.
   - `DeleteAsync`: guard → admin → `ExecuteDelete WHERE Id ∧ EventId` → 0 ⇒ `PostNotFound`.
3. **Party news service.** `NewsDedupePrefix(postId)` used by `NotifyCrewAsync` and delete. Extract
   the feed's projection (author placeholder + current party role + `EditedDate`) for feed and edit;
   `CreateAsync`'s DTO gains `EditedDate: null`.
   - Access: `access is null || !(IsCrew || IsPartyAdmin)` ⇒ `PartyNotFound`; `!IsPartyAdmin` ⇒
     `Forbidden`.
   - `EditAsync`: as event, scoped `Id ∧ PartyId`. **No notification statement** (R1).
   - `DeleteAsync`: strategy → tx → `ExecuteDelete post WHERE Id ∧ PartyId` → 0 ⇒ return
     not-found without touching alerts → `DeleteManyAsync(PartyNews, prefix)` → commit; then
     `RefreshUnreadBadgesAsync`.
   - Log post ids on failure, never text.
4. **Controllers.** `EventsController.EditNews`/`DeleteNews` (`{id:guid}/news/{postId:guid}`),
   mapping to `EventNotFound()`, `Forbidden(...)`, and a new `NewsPostNotFound()`.
   `PartiesController.EditNews`/`DeleteNews`, mapping to `PartyNotFound()` (the feed's),
   `Fail(Forbidden, …)`, and a new `NewsPostNotFound()`. Same titles and details as the team one.
5. **Home.** Event and party `NewsRaw` project `n.EditedDate`.

### Frontend

1. **Models + services**: `editedDate` on `EventNews`/`PartyNews`; `EventService.editNews/deleteNews`,
   `PartyService.editNews/deleteNews`.
2. **`jh-news-post`** (R8): 057's menu, editor and dialog **moved** out of team-detail with their
   markup, classes, testids and comments, generalised by inputs. Behaviour state is all
   **signals** (zoneless). Focus via `afterNextRender` with the component's injector. Escape and
   outside click are handled per instance with the own-wrapper containment check. The editing
   state is `NewsPostEditing`, provided by each page (never root); instances register while on
   screen through `DestroyRef`, and the lock counts only those. The host element is the
   post row (`flex items-start gap-xs`).
3. **Team page**: the post `<li>` keeps its testid and wraps `jh-news-post`; team-detail keeps
   `news`, `newsNotice` and the heading focus, and loses every other news signal, `trapTab`, the
   news branches of `onEscape` and `onDocumentClick`; it provides `NewsPostEditing` and resets it
   on a team switch, as 057 did. The News card gets `overflowVisible`. **The spec file is not
   edited**; it must stay green.
4. **Event page**: `jh-event-news-feed` gains `eventId` and `canManage` inputs, and `news`
   becomes a `model` bound `[(news)]="news"` so the feed updates the list itself. It gets a
   `notice` and a focusable `h2` (`tabindex="-1"`), and the marker after the date. `canManage` is
   `viewer.isAdmin` **without** the cancelled gate (FR-015); the composer keeps its gate. The News
   card gets `overflowVisible`.
5. **Party pages**: both lists wrap each post in `jh-news-post` (`maxLength` 1000, party keys),
   `overflowVisible` on each post card, marker after the date, `notice` above the list, heading
   focus after removal (party news: the `h1`; party page: the label, now an `h2`). The post error
   becomes `parties.news.error` only (R11).
6. **Copy**: `news.*` in all three catalogues **in one commit**; remove the moved
   `teams.detail.news*` keys in the same commit. House punctuation (`–` in German, *Wir
   konnten…*).

### Specs and follow-ups

- Tick nothing in 006, 016 or 057. 057 named #367/#368 as follow-ups; this plan fulfils them.
  Record a one-line "Followed up by 059" note in 057's plan **Follow-ups** section.
- Close GH #367 and #368 from the PR.

## Recorded residuals

- **Party-news email already sent keeps the old or deleted text.** It is out of reach, and the
  dialog says so. Push carries no text; a shown push is not withdrawn.
- **The marker doesn't say who edited** (owner accepted).
- **The prefix lookup is a sequential scan** of `Notifications` (057's residual, unchanged): rare,
  admin-only, fine at today's volume; `varchar_pattern_ops` index if ever needed.
- **Other people's open pages don't update live.** An open Alerts inbox keeps a deleted party
  post's row until reload (opening it lands on the party news page); only the badge moves live.
- **Posting's 403 for party outsiders** (a member of the team outside the crew learns the party
  exists) is pre-existing and untouched. Edit/delete do not widen it.
- **The shared component makes a later change to the controls land on all four surfaces at once.**
  That is the point, and it is also the blast radius. The team-detail tests and the shared spec
  are the guard.
