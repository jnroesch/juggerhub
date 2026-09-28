# Implementation Plan: Team Polls — a Team Can Ask Its Members a Question

**Branch**: `062-team-polls` | **Date**: 2026-09-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/062-team-polls/spec.md` (GH #365)

## Summary

Team admins can put a question with 2–10 fixed options to their team. Every current member can
answer, change or withdraw that answer until the poll closes, and see the result. Polls live in a
new members-only **Polls** card on the team page, placed before News. An open poll the member
has not answered appears in Home's *Needs you*. Opening a poll notifies every other member
through the existing *Team news* setting: an Alerts row, an email in the recipient's language,
and a device notice **without the question**. Nine owner decisions are recorded in the spec.

**Per poll**, the admin chooses:
- one answer or several;
- **named or anonymous**, fixed forever;
- results **always visible** or **hidden until you answer**;
- an optional close time.

**Admin controls**:
- any current admin can edit the content until the first answer;
- the close time can move while the poll is open;
- close early, final;
- delete, which removes the poll together with its alerts.

In named polls, admins also see who has not answered.

**The technical core** is three tables (`TeamPoll`, `TeamPollOption`, `TeamPollVote`, one vote
row per chosen option) behind a new `TeamPollsController`/`TeamPollService`. The rules it rests
on:

1. **Open is derived** from `ClosedAt`/`ClosesAt`, with no sweep. One predicate,
   `TeamPollOpen.At(now)`, is shared by the list, the guards, the cap and Home.
2. **Who counts is derived** from current, non-banned membership at read time. Nothing is
   written when someone leaves.
3. **Anonymity and hidden results are enforced in the one DTO builder**. Voter identities enter
   it behind exactly two conditions, and tests inspect the raw JSON for every role.
4. **Answers, edits and closes are serialised on the poll row**; creation is serialised on the
   team row for the 10-open cap. Everything runs as execution-strategy units.
5. The notice is `NotificationType.TeamPoll = 11` on the existing engine:
   - an edit rewrites delivered rows silently, and a delete removes them (057's machinery);
   - a team rename is covered free, because 061 rewrites rows by `teamSlug`, never by type.

Account deletion removes the member's answers. An erased admin's polls stay, shown with the
former-player placeholder. Because of that, the deletion preview gains "Polls". Two legal
sentences that enumerate what outlasts a deletion are rewritten by category, which **bumps the
Terms version**.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 22 (zoneless) + Nx + Tailwind (frontend)

**Primary Dependencies**: ASP.NET Core, EF Core 10 + Npgsql, Transloco. Nothing new.

**Storage**: PostgreSQL. +3 tables (`TeamPolls`, `TeamPollOptions`, `TeamPollVotes`) and one
migration, `AddTeamPolls`. It must be generated **with** a build; check that it is not empty
(056).

**Testing**:
- xUnit integration tests (Testcontainers Postgres, `JuggerHubApiFactory`, `TestEmailSender`,
  `FakePushDispatcher`, `FakeNotificationRealtime`);
- pure unit tests for `TeamPollRules`;
- Jest (`npx nx test web`);
- e2e helpers that open a team page;
- the owner-mandated browser walk (Playwright, German, 375px + desktop).

**Target Platform**: Linux containers (AKS), local Docker Compose

**Project Type**: web application (`backend/` + `frontend/apps/web`)

**Performance Goals**:
- The team page for non-members makes no extra request (SC-009). Members make two (open +
  closed).
- Each list read is three queries: polls with options, the vote rows for those polls, and the
  current members with names. Everything else is computed in memory. Team-scale data only.

**Constraints**:
- Anonymity is enforced server-side in every response (FR-018, SC-003).
- A member with hidden results receives no counts (FR-012a, SC-003a).
- Alerts are rewritten silently on edit (FR-030).
- A delete is all-or-nothing with its alerts (FR-024).
- The client renders error `code`s, never `detail` (#179).
- Browser mutations are never auto-retried.

**Scale/Scope**:
- Backend: 7 endpoints, 3 entities, 1 policy class, 1 predicate, 1 notification type,
  1 Needs-you kind, 1 email template ×3.
- Frontend: 3 components (card, item, editor), a model, a service, a util; edits to the Alerts
  row and the Needs-you card.
- Copy: ~70 i18n keys ×3; the settings description ×3; the legal sentences ×3; the Terms
  version bump.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Verdict |
|---|---|
| **I. Security-first / never trust the client** | PASS. Every rule is decided in `TeamPollService` from `TeamMembershipGuard` on each request: member-only reads, admin-only mutations, the one-answer rule, open/closed, the content lock, anonymity and hidden results. Non-members get the same 404 as an unknown team (no oracle, FR-032). The anonymity invariant is structural (R6) and tested on raw JSON for author, admin and member. Question/options are interpolation-only, never `innerHTML`, never linkified. No person's name is stored in the alert payload (037 FR-023). Logs carry ids only, never question text. |
| **II. Thin controllers, services, DTO projections** | PASS. The new thin `TeamPollsController` maps status → ProblemDetails with `code`/`option` extensions. Logic sits in `TeamPollService`; rules are in the pure `TeamPollRules`; alert-row mutations stay in `NotificationService` (057: the engine owns rows). Reads use explicit `.Select` projections, then an in-memory DTO builder over already-projected aggregates. |
| **III. Data access** | PASS. The three entities derive from `BaseEntity` (UUIDv7). Reads are `AsNoTracking` projections. **`ModifiedDate` is set on every `ExecuteUpdate`**: close, edit, and the engine's payload rewrite. The list endpoint is paged (`PagedResult`); the open list is bounded by the cap. **Deviation recorded** below: options, voters and the not-answered list are embedded, bounded by ≤10 and by current membership. |
| **IV. Auth/session** | N/A. There is no auth change. The Terms version bump uses 041's existing mechanism. |
| **V. Environment parity** | PASS. One migration; `appsettings.json` gets the Terms version value. There is no new config section or secret. |
| **VI. Conventions** | PASS. `.html`/`.css`/`.ts` are kept separate per component; there are no scripts. |
| **VII. Resilience** | **Not engaged as an integration**: no outbound call. Email and push reuse existing senders and the dispatcher. What VII requires is designed in (R3, R16): every multi-step write is ONE execution-strategy unit that clears the tracker, creates its entities inside, and uses fixed-value statements, so a replay converges; side effects run after commit and are best-effort; browser mutations are never auto-retried (the interceptor retries GET/HEAD only). |
| **Gate 7 (UI/DESIGN.md)** | **ENGAGED** → `checklists/ui-review.md`. Binding case, German at 375px: a named poll with 10 × 80-character options showing results and the admin's not-answered line; the editor with 10 option rows; the anonymous notice; the close/delete dialogs as bottom sheets. There is also the Alerts row, the *Braucht dich* item, and the settings description. DESIGN.md has **no radio-group spec**; the editor's three choices are recorded as a gap, not styled freely. |
| **Gate 8 (Resilience)** | PASS (see VII). |

**Post-design re-check (after Phase 1)**: unchanged. The design adds:
- no dependency;
- no outbound call;
- no anonymous surface;
- no unpaged list.

The one schema-level trust decision, one-answer enforced under a lock rather than by an index,
is argued in R2/R3 and covered by a concurrency test.

## Project Structure

### Documentation (this feature)

```text
specs/062-team-polls/
├── spec.md
├── plan.md                        # this file
├── research.md                    # R1–R17
├── data-model.md
├── quickstart.md
├── contracts/team-polls-api.md
├── checklists/requirements.md
├── checklists/ui-review.md        # created during implementation (Gate 7)
└── tasks.md                       # /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── Entities/TeamPoll.cs, TeamPollOption.cs, TeamPollVote.cs      # NEW
├── Entities/Team.cs                                              # + Polls navigation
├── Entities/NotificationEnums.cs                                 # + TeamPoll = 11; For(): explicit TeamPoll => TeamNews
├── Data/AppDbContext.cs                                          # 3 DbSets + config (R2, data-model indexes/FKs)
├── Data/Migrations/*_AddTeamPolls.cs                             # NEW (generated WITH a build; not empty)
├── Dtos/Teams/TeamPollDtos.cs                                    # NEW: TeamPollDto, option/person DTOs, requests
├── Dtos/Notifications/NotificationDtos.cs                        # + TeamPollPayload
├── Dtos/Home/HomeDtos.cs                                         # + NeedsYouKind.TeamPoll; NeedsYouParamsDto.Question
├── Services/Teams/TeamPollRules.cs                               # NEW: pure limits + codes (R8)
├── Services/Teams/TeamPollOpen.cs                                # NEW: the one open predicate (R4)
├── Services/Teams/ITeamPollService.cs, TeamPollService.cs        # NEW: list, create, edit, answer, withdraw, close, delete (R3, R5, R6)
├── Controllers/TeamPollsController.cs                            # NEW: 7 endpoints, coded ProblemDetails
├── Services/Home/HomeService.cs                                  # + TeamPoll Needs-you query (R11)
├── Services/Notifications/Push/PushContentComposer.cs + PushLocalizer.cs  # + TeamPoll title/body/url arms (R9)
├── Services/Notifications/NotificationPreferenceService.cs       # TeamNews description ×3 (R12)
├── Services/Email/TeamEmailService.cs, EmailLocalizer.cs         # + SendTeamPollEmailAsync; keys ×3 (R10)
├── Services/EmailTemplateService/*                               # + GenerateTeamPollEmailAsync
├── EmailTemplates/{en,de,es}/team-poll.html                      # NEW
├── Services/Account/AccountDeletionService.cs                    # + erase votes; RetainedCategories + "Polls" (R13)
├── Common/TermsOptions.cs, appsettings.json                      # Terms version bump (R13)
├── Program.cs                                                    # + AddScoped<ITeamPollService, TeamPollService>
└── tests/JuggerHub.Api.IntegrationTests/
    ├── Teams/TeamPollRulesTests.cs                               # NEW: pure rules
    ├── Teams/TeamPollTests.cs                                    # NEW: create/answer/edit/close/delete/access/cap/concurrency
    ├── Teams/TeamPollPrivacyTests.cs                             # NEW: anonymity + hidden results on raw JSON, every role
    ├── Teams/TeamPollNotificationTests.cs                        # NEW: recipients, channels, silent edit, delete removal, email language
    ├── Teams/TeamRenameRewriteTests.cs                           # + TeamPoll as the tenth kind
    ├── Home/…                                                    # + Needs-you TeamPoll
    ├── Push/PushComposerTests.cs, Notifications/NotificationCategoryMappingTests.cs  # + TeamPoll
    └── AccountDeletion/AccountErasureTests.cs                    # + votes erased, polls retained, preview lists "Polls"

frontend/apps/web/src/app/
├── core/models/poll.models.ts                                    # NEW
├── core/services/poll.service.ts                                 # NEW
├── core/utils/poll-close-time.ts (+ .spec.ts)                    # NEW: datetime-local ↔ UTC instant
├── core/models/notification.models.ts                            # + TeamPoll type guard/payload
├── core/models/home.models.ts                                    # + 'TeamPoll' kind, params.question
├── features/teams/team-detail/polls/team-polls.component.*       # NEW: the card
├── features/teams/team-detail/polls/poll-item.component.*        # NEW: one poll
├── features/teams/team-detail/polls/poll-editor.component.*      # NEW: create/edit form
├── features/teams/team-detail/team-detail.component.{html,ts}    # mount <jh-team-polls> before News (members only)
├── features/alerts/notification-row/*                            # TeamPoll title/supporting/icon/link + fragment
├── features/dashboard/modules/needs-you-card.component.*         # TeamPoll: headline, question line, "Answer" link with fragment
├── public/i18n/{en,de,es}.json                                   # ~70 keys, one commit (catalog-parity)
└── public/i18n/legal/{en,de,es}.json                             # 2 sentences reworded by category + terms version/lastUpdated

frontend/apps/web-e2e/src/…                                       # re-run the team-page suites; fix helpers if the page change breaks them
specs/037-account-deletion/data-model.md                          # amendment note: TeamPollVotes erased, TeamPolls retained
specs/005-team-space/spec.md                                      # amendment note: polls delivered by 062
```

**Structure Decision**: the existing web-application layout. The feature lives in the team
domain (`Services/Teams`, `features/teams/team-detail/polls`), plus one arm each in the
notification engine's composer, Home, the Alerts row and the Needs-you card.

## Phases (implementation order)

1. **Model + migration**:
   - entities, config, DbSets, `NotificationType.TeamPoll` and the category arm, DTO shapes;
   - generate the migration with a build and inspect it.
2. **Rules + service + endpoints (US1)**: `TeamPollRules` (unit-tested), `TeamPollOpen`, then:
   - list (the R5/R6 builder), create (team lock + cap), answer/withdraw (poll lock);
   - the controller.

   Tests: create/answer/change/withdraw, one-answer, counts, the member/admin/outsider guards,
   the cap, the non-member 404, and the concurrency of two answers from one member.
3. **Privacy (US3)**: anonymity and hidden results. The raw-JSON tests for author, second admin
   and member (SC-003/SC-003a) and the not-answered list (admins, named only).
4. **Close + edit + delete (US4, US5)**:
   - close as a conditional update;
   - edit under the poll lock, with unchanged-is-not-a-change, the `answered` lock and the
     close-time move;
   - delete with the alert removal and a badge refresh after commit.

   Tests: the edit-vs-first-answer race, the close-vs-answer race, the time-based close,
   `ModifiedDate` on each update.
5. **Notices (US2)**:
   - `CreateManyAsync` after commit;
   - email ×3 in the recipient's language;
   - the push composer arms;
   - the silent payload rewrite on a question edit;
   - the rename kind;
   - the settings copy.

   Tests: recipients and channels matrix, author excluded, E's email off, the German email,
   the push body without the question, the edit silence (`FakeNotificationRealtime`), the delete
   removing former members' rows.
6. **Home Needs you**: the query + DTO; tests for listed, answered, closed, author, left team.
7. **Account deletion + legal**: erase votes, retained "Polls", preview; the legal sentences ×3
   (German first) and the Terms version bump across `TermsOptions`/appsettings/catalogues.
8. **Frontend card + item + editor**: models, service, util, components, error mapping, deep
   link; i18n ×3 in one commit.
9. **Frontend Alerts row + Needs-you card**.
10. **Verification**:
    - the whole suites, lint and build; e2e team-page suites;
    - the Gate 7 checklist;
    - the browser walk (German, 375px + desktop, per quickstart);
    - the amendment notes; the PR.

## Traps (read before implementing)

**Database and concurrency**:
- **`ModifiedDate` on every `ExecuteUpdate`**: close, edit, and the engine's payload rewrite
  (which already does it). This is the likeliest Gate-2 failure.
- **`NotificationType.TeamPoll` is APPENDED (= 11) and needs its own arm** in
  `NotificationCategories.For`. The default arm silently returns `TeamNews`, which is right here
  by accident, so the mapping test must name it. It also needs **three composer arms**, or
  058's exhaustive guard fails.
- **Lock, then re-read.** `ExecuteSqlInterpolatedAsync("SELECT 1 … FOR UPDATE")` returns no data.
  Read the poll's state with a normal query **after** it, inside the same transaction.
- **`ChangeTracker.Clear()` first in every strategy delegate, and create entities inside it.** A
  replayed delegate must not re-attach the first attempt's options.

**Privacy**:
- **Anonymity lives in the builder only.** Never project voter names in SQL for all polls and
  then filter them afterwards. Load names only for named polls whose results the viewer may
  see. The raw-JSON tests are the guard: they search the body for other voters' handles and
  display names.
- **Hidden results apply to the author and admins too** (clarified). There is no admin bypass.
- **Names through `PlayerProfiles`, never `vote.User.Profile!`**: the ban filter makes that
  navigation misbehave (044).

**Request handling**:
- **`closesAt` is a `DateTimeOffset?`** in the request and is stored as UTC. A bare local string
  becomes `DateTimeKind.Unspecified`, and Npgsql rejects that for `timestamptz`.
- **The edit request has no `isAnonymous`.** Do not "helpfully" add it for symmetry: FR-016 is
  made structural by its absence.
- **Unchanged content is not a change**: compare the question, the options' texts in order, and
  both flags. Do not replace option rows otherwise, or moving the close time after answers
  would cascade-delete every answer.
- **Nullable request strings with loose guards** (061): an implicit `[Required]` gives an
  uncoded 400 that the client cannot translate.

**Frontend**:
- **Deep links are `[routerLink]` + `[fragment]`**, and the card scrolls after its data lands
  (`afterNextRender`). Neither a string with `#` in `routerLink` nor the router's
  `anchorScrolling` alone works here.
- **Zoneless**: every value the templates read is a signal, including the multi-choice selection
  and the editor's option rows.
- **One coral CTA per view**: *Umfrage starten* and the editor's submit are `secondary`, because
  the news composer's *Post news* is the page's coral CTA.
- **Changing the team page can break e2e helpers** that unit tests don't see (061's
  `createTeam`). Run the team-page e2e suites.

**Copy and legal**:
- **All three catalogues change in one commit** (`catalog-parity.spec.ts`). **The legal texts
  are German-first**, and the **Terms version moves in six places at once**: two in
  `TermsOptions`, `appsettings.json`, three catalogues. `TermsVersionParityTests` guards it.

## Complexity Tracking

| Deviation | Why needed | Simpler alternative rejected because |
|---|---|---|
| Options (≤10), per-option voters and the admin's not-answered list are embedded in `TeamPollDto` rather than served by paged endpoints (Principle III "lists paginate") | A poll is one thing on screen. The voter lists are bounded by the team's current membership, and the options by 10 | N extra round trips per page of polls, and a `totalCount` promising paging that nothing needs. Precedent: 061 `Links`, 044/009 `Roster` |
| "One answer per member" in a one-answer poll is enforced by the service under a row lock, not by an index | The one/many setting lives on the poll, and no index can reference it (R2) | A fourth table (Answer + Choice) would buy an index while still needing the same lock for edit/close races (R3). A concurrency test covers it |
| One raw statement (`SELECT … FOR UPDATE`) per mutation | EF Core has no row-lock API | The `TeamService` idiom, parameterised via `ExecuteSqlInterpolatedAsync` |

## Residuals (accepted, recorded)

- Emails and device notices already delivered keep an edited question, and cannot be recalled
  after a delete. The in-app rows do follow.
- An open page does not update live as others answer; a reload or the member's own action shows
  the current state.
- With visible counts, a small team can infer anonymous answers (FR-019). With hidden results, a
  member can answer to peek and then withdraw. Both are inherent and stated in the spec.
- A member who left and rejoined finds their old answer counted again (spec edge case).
- A poll whose only stored answers come from people who have since left still counts as
  answered for the content lock. Its content cannot be changed even though its counts show 0.
  This is the price of never deleting an answer that a returning member would get back.
- The rename's alert lookup and the delete's prefix lookup are sequential scans over
  `Notifications`. This was accepted in 057/061, and the recorded fix is an expression index.
- A deep link to an old closed poll that is not on the first page of closed polls lands on the
  card, not on the poll.
