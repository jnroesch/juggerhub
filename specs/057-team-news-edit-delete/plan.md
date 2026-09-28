# Implementation Plan: Team News Posts Can Be Edited and Deleted

**Branch**: `057-team-news-edit-delete` | **Date**: 2026-09-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/057-team-news-edit-delete/spec.md` (GH #363)

## Summary

Team news is permanent today: `TeamNewsService` has `GetFeedAsync` and `PostAsync` and nothing
else, and `TeamNewsDto` carries no id a client could address. This feature adds
**`PATCH` and `DELETE /teams/{slug}/news/{postId}`**. **Any current admin may act on any post**
(owner decision). An edit sets a new nullable **`EditedDate`**, which surfaces as an
*edited* marker on the team page **and** on Home's news module. It notifies no one, and it
**rewrites the excerpt in the Alerts rows already delivered** (owner decision). A delete is
hard, behind the page's confirmation dialog, and **removes those Alerts rows** for every
recipient, former members included. After commit it refreshes the badge of anyone who lost
an unread row.

**The load-bearing decisions, all found by reading:**

1. **A post's Alerts rows are found by `DedupeKey` prefix `news:{postId}:`, never by the
   roster.** `CreateManyAsync` writes `{prefix}:{recipient}` for every row, so the prefix
   reaches **former members**, whom a roster join would silently miss, leaving the wrong-team
   post's text in their inbox (research R1).
2. **The post and its Alerts rows change in ONE transaction** through the execution strategy.
   Otherwise a failure between the two statements leaves the post deleted and its text still
   quoted in every inbox, with no way back (a second delete answers "not found"). Every
   statement is `ExecuteUpdate`/`ExecuteDelete` with fixed values, so a replay converges. The
   notification statement runs **only after the post statement matched a row of the caller's
   team**, which is what keeps another team's post id away from this team's rows (FR-012).
   The badge refresh runs **after** commit (R3).
3. **The notification engine owns the row mutations**: `ReplacePayloadAsync`,
   `DeleteManyAsync`, `RefreshUnreadBadgesAsync` on `INotificationService`. The payload's
   camelCase serializer and the SignalR badge channel each keep a single owner, and none of the
   names say `Push…` (the 055 trap: `PushAsync` in that class means SignalR) (R2).
4. **`EditedDate`, not `ModifiedDate`**: the audit field moves on writes nobody would call an
   edit (056 shipped exactly that on `ChatMessages`), and null on every existing row *is*
   FR-017, with no backfill (R4).
5. **Two corrections to the issue**: Home renders its own **`HomeNewsDto`** (shared by team,
   event and party), not `TeamNewsDto`, so the marker is a nullable field there that the event
   and party projections write as `null`. And the Alerts link already survives a missing post
   (it opens `/t/{slug}`); what an edit or delete leaves behind is the **copied excerpt** (R8,
   spec → Context).

**Size**: 1 column + 1 migration, 2 endpoints, 3 `INotificationService` methods, 2
`ITeamNewsService` methods, 2 DTO fields + 1 DTO field, one menu + inline editor + reused
dialog on the team page, one marker on Home, ~17 i18n keys × 3. **No new entity, no new
dependency, no new configuration, no infrastructure change.**

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (zoneless, signals) with Nx (frontend)

**Primary Dependencies**: ASP.NET Core, EF Core + Npgsql (execution strategy with
`EnableRetryOnFailure`), SignalR (the existing notifications hub), Transloco (en/de/es). No new
package.

**Storage**: PostgreSQL 18. One nullable column on `TeamNewsPosts`. The `Notifications` table
is read and written through existing columns only.

**Testing**: xUnit integration tests against the real API and a Postgres Testcontainer
(`JuggerHubApiFactory`, with the `TestEmailSender` / `FakePushDispatcher` fakes plus a new
recording `FakeNotificationRealtime`); Jest (Jest 30: `--testPathPatterns`) for components and
services; the catalogue guards `catalog-parity` / `catalog-punctuation`; a real-browser walk
(German, 375px + desktop).

**Target Platform**: Linux containers on AKS (Dev/Prod) and docker compose locally; evergreen
browsers, the installed PWA included.

**Project Type**: Web application (`backend/` + `frontend/apps/web`).

**Performance Goals**: Nothing new at scale. Edit and delete are rare admin actions touching one
post and at most that post's recipients (≈ one roster).

**Constraints**: Hard delete, no history. No notification of any kind on edit or delete. No
browser auto-retry of `PATCH`/`DELETE` (already true: the interceptor retries only
`GET`/`HEAD`). German at 375px is the binding layout case. One coral CTA per view.

**Scale/Scope**: One team page component, one dashboard list component, one entity, two
DTOs, one service pair, one controller.

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1 design. Constitution v1.4.0.*

| Gate | Verdict | Notes |
|------|---------|-------|
| **I. Security-first, never trust the client** | ✅ Pass | Both endpoints are decided server-side in the existing order: `TeamMembershipGuard` → admin → post **scoped to the resolved team** (`Id AND TeamId`). An unknown team and a non-member get an identical 404 (no oracle, FR-011). A post id from another team is a 404 even for an admin of both (FR-012). The Alerts-row statement is reachable only after the post statement matched a row of the caller's team (R3), so no caller can touch another team's notification rows by guessing an id. The UI's menu is convenience; the server is the boundary (FR-014). Errors are problem details; the client shows translated copy, never the server's `detail`. |
| **II. Thin controllers, service-centric** | ✅ Pass | Two controller actions that forward and map statuses, the same shape as `PostNews`. Logic lives in `TeamNewsService` and `NotificationService` behind their interfaces. DTOs are built by explicit `.Select` projections (one shared projection for feed and edit), with no mapper. |
| **III. Disciplined data access** | ⚠️ Pass **with an obligation** | **Every write is `ExecuteUpdateAsync`/`ExecuteDeleteAsync`, so `ModifiedDate` MUST be set explicitly** on both update paths: the post's (with `EditedDate`) and the notification payload rewrite. This is the most likely gate failure in the diff, and a test asserts it moves on both. The new column derives nothing from `BaseEntity`'s audit fields. No list endpoint is added, the feed stays paginated, and reads are projected + `AsNoTracking`. |
| **IV. Auth & sessions** | ✅ Pass | Untouched. Both endpoints sit under the controller's existing `[Authorize(JwtBearer)]`. |
| **V. Environment parity** | ✅ Pass | One EF migration, applied the same way everywhere. No configuration and no secret. |
| **VI. Conventions & tooling** | ✅ Pass | Frontend edits keep `.html`/`.css`/`.ts` separate. New spec files only. No script added. |
| **VII. Resilient by default** | ✅ **Not engaged as an integration** | No outbound call: email and push are deliberately *not* sent. The one multi-step write **does** fall under the "multi-step transactions run through the execution strategy" rule, and follows it (R3: all mutation inside the delegate, replay-safe, side effects after commit). The browser never auto-retries the new `PATCH`/`DELETE` (retry interceptor: `GET`/`HEAD` only), and the spec's "try again" is a press. Adding retry, backoff or a breaker is review-rejectable (R7). |
| **Gate 7 — UI/design compliance** | ✅ **Engaged** | A new per-post menu, an inline editor, a destructive confirmation, a marker in two places, and ~17 strings × 3 → `checklists/ui-review.md` from the template, verified against screenshots. The binding cases are **German at 375px**: the editor's hint and buttons under a post in the narrowed column, the dialog as a bottom sheet, and the meta line with *· bearbeitet*. |
| **Gate 8 — Resilience review** | ✅ Pass | See VII. The only item it asks of this diff is the execution-strategy transaction, which is designed in. |

**Post-Phase-1 re-evaluation**: unchanged. The design kept the column count at one, added no
entity, endpoint beyond the two, dependency or configuration, and wrote the Principle III
obligation into research R3, the data model, the contract and the test plan.

**Complexity Tracking**: not required. No violation needs justifying (the transaction is
constitution-mandated, not added complexity).

## Project Structure

### Documentation (this feature)

```text
specs/057-team-news-edit-delete/
├── plan.md                  # This file
├── spec.md                  # FR-001…FR-021, SC-001…SC-006, owner clarifications
├── research.md              # R1–R13
├── data-model.md            # EditedDate; the Alerts-row operations; widened DTOs
├── quickstart.md            # Automated checks + the German 375px/desktop walk
├── contracts/
│   └── team-news-api.md     # PATCH / DELETE, widened DTOs, internal interface additions
├── checklists/
│   ├── requirements.md      # Spec quality (done)
│   └── ui-review.md         # Gate 7, created during implementation
└── tasks.md                 # /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── Entities/TeamNewsPost.cs                                   # + EditedDate; summary rewritten (R11)
├── Data/Migrations/<ts>_AddTeamNewsEditedDate.cs (+Designer, snapshot)
├── Dtos/Teams/TeamDtos.cs                                     # TeamNewsDto + Id, EditedDate; EditTeamNewsRequest
├── Dtos/Home/HomeDtos.cs                                      # HomeNewsDto + EditedDate
├── Services/Home/HomeProjections.cs                           # NewsRaw + EditedDate
├── Services/Home/HomeNewsMerge.cs                             # pass EditedDate through
├── Services/Home/HomeService.cs                               # team projects n.EditedDate; event/party null
├── Services/Notifications/INotificationService.cs             # + ReplacePayloadAsync, DeleteManyAsync, RefreshUnreadBadgesAsync
├── Services/Notifications/NotificationService.cs              # their implementations
├── Services/Teams/ITeamNewsService.cs                         # + EditAsync, DeleteAsync, statuses, result
├── Services/Teams/TeamNewsService.cs                          # implementations; shared projection
├── Controllers/TeamsController.cs                             # + EditNews (PATCH), DeleteNews (DELETE)
└── tests/JuggerHub.Api.IntegrationTests/
    ├── JuggerHubApiFactory.cs                                 # register FakeNotificationRealtime
    ├── Notifications/FakeNotificationRealtime.cs              # NEW recording fake
    └── Teams/TeamNewsEditDeleteTests.cs                       # NEW

frontend/apps/web/
├── public/i18n/{en,de,es}.json                                # teams.detail.news* + home.newsEdited
└── src/app/
    ├── core/models/team.models.ts                             # TeamNews + id, editedDate
    ├── core/models/home.models.ts                             # HomeNews + editedDate
    ├── core/services/team.service.ts (+ .spec.ts)             # editNews, deleteNews
    ├── features/teams/team-detail/team-detail.component.{ts,html,spec.ts}   # menu, editor, dialog, marker
    └── features/dashboard/modules/news-list.component.{html,spec.ts}        # marker (+ NEW spec)
```

**Structure Decision**: the existing web-application layout. Every change lands in a file that
already owns the concern. The only new files are the migration, the integration test
suite, the realtime fake and one component spec.

## Implementation approach

### Backend

1. **Entity + migration.** `TeamNewsPost.EditedDate` (`DateTime?`). Rewrite the summary to
   describe the entity as it is: posted by admins (010), editable and deletable by any admin
   (057), `EditedDate` meaning *a person changed the text* and why it is not `ModifiedDate`
   (R4, R11). Generate the migration with a **build** (the 056 lesson: `ef migrations add
   --no-build` emitted an empty migration), then read it: one `AddColumn`, nullable, no default
   and no SQL.
2. **Notification engine** (R2). Three methods. `ReplacePayloadAsync` serializes with the
   existing `PayloadJson` and `ExecuteUpdate`s `Payload` **and `ModifiedDate`**.
   `DeleteManyAsync` reads the distinct unread recipients, then `ExecuteDelete`s.
   `RefreshUnreadBadgesAsync` loops the existing private `PushUnreadCountAsync`, which is
   already best-effort. The XML docs say which methods push and which are
   transaction-safe.
3. **Team news service.** Extract the feed's `.Select` into one private projection used by
   `GetFeedAsync` and the edit response (the author placeholder and current-role subquery stay
   exactly as they are). `PostAsync`'s hand-built DTO gains `post.Id` and `EditedDate: null`.
   - `EditAsync`: guard → admin → read the post's current body scoped to the team
     (`AsNoTracking`). Absent ⇒ `PostNotFound`. Trimmed text equal ⇒ `Updated` with the current
     DTO and **no write**. Otherwise, inside `strategy.ExecuteAsync`: begin →
     `ExecuteUpdate(Body, EditedDate, ModifiedDate) WHERE Id AND TeamId` → 0 rows ⇒
     `PostNotFound` → `ReplacePayloadAsync(TeamNews, $"news:{postId}", new TeamNewsPayload(slug,
     name, postId, Excerpt(text)))` → commit. Project the DTO after commit.
   - `DeleteAsync`: guard → admin → inside the strategy: begin → `ExecuteDelete WHERE Id AND
     TeamId` → 0 rows ⇒ `PostNotFound` → `DeleteManyAsync(TeamNews, $"news:{postId}")` →
     commit. Then `RefreshUnreadBadgesAsync(recipients)`.
   - Both log the post id on failure, **never the text** (Principle VII: no personal content
     in logs).
4. **Controller.** `EditNews` (`[HttpPatch("{slug}/news/{postId:guid}")]`, 200/403/404/404) and
   `DeleteNews` (`[HttpDelete(...)]`, 204/403/404/404), with a `NewsPostNotFound()` helper beside
   `TeamNotFound()`.
5. **Home.** `NewsRaw` + `HomeNewsDto` gain `EditedDate`. The team projection writes
   `n.EditedDate`, and event and party write `(DateTime?)null`.

### Frontend

1. **Models + service**: `TeamNews.id/editedDate`, `HomeNews.editedDate`;
   `TeamService.editNews(slug, id, body)` → `PATCH`, `deleteNews(slug, id)` → `DELETE`.
2. **Team page** (all new state as **signals**, since the app is zoneless):
   `newsMenu`, `editingNewsId`, `newsDraft`, `savingNews`, `newsEditError`, `deleteNewsTarget`,
   `deletingNews`, `newsDeleteError`, `newsNotice`. The feed tracks `n.id`. Each post becomes a
   flex row: body/meta (or the editor) plus, for admins, the `ellipsis` menu. Focus goes to the
   textarea on open and to *Keep post* when the dialog opens (`afterNextRender`). Escape closes
   the menu and the dialog (extending the page's existing listener), and an outside click
   closes the menu. A 404 removes the post and sets `newsNotice`. Other failures show a
   translated *We couldn't …* line and keep the text or the dialog. The body gains
   `whitespace-pre-line break-words` (R9).
3. **Home news list**: append *· edited* when `item.editedDate`.
4. **Copy**: every key in **all three catalogues in one commit** (`catalog-parity` fails
   otherwise). German `–` never `—`; errors open with *We couldn't* / *Wir konnten* /
   *No pudimos*.

### Specs and follow-ups

- Tick nothing in 005 or 010. 010 explicitly deferred edit/delete to "a later feature", so this
  **fulfils** it rather than amending it. Record the pointer in 010's spec as a one-line
  "Followed up by 057" note.
- File two follow-up issues: **event news** and **party news** edit/delete, pointing at this
  plan's R2 methods (already producer-agnostic) and the owner's any-admin rule as the default
  to confirm there.

## Recorded residuals

- **Email already sent keeps the old text, or the deleted post's text.** It is out of our reach,
  and the delete dialog says so (FR-008). Push carries no post text.
- **A shown push notification is not withdrawn** on delete (056's residual, unchanged).
- **The marker doesn't say who edited.** Another admin's edit reads as the author's (owner
  accepted, spec → Clarifications).
- **The prefix lookup is a sequential scan** of `Notifications`, which has no retention sweep and
  only grows. It is fine at today's volume and runs only on an admin's edit or delete. The fix,
  if ever needed, is a `varchar_pattern_ops` index (R1).
- **Other people's open pages don't update live.** An open Alerts inbox keeps showing a deleted
  post's row until reload (opening it still lands on the team page); only the badge updates
  live.
- **Posts with line breaks now render them** on the team page (`whitespace-pre-line`,
  matching party news). This is a visible change for existing posts that contained them.

## Follow-ups

- Event news: edit and delete (same pattern; R2 reusable).
- Party news: edit and delete (same pattern; prefix `party-news:{postId}` already fits R2).
