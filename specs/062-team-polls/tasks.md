# Tasks: Team Polls — a Team Can Ask Its Members a Question

**Input**: Design documents from `specs/062-team-polls/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/team-polls-api.md](./contracts/team-polls-api.md), [quickstart.md](./quickstart.md)

**Tests**: included, per the project's convention. Each behaviour gets its test before, or alongside,
the code that makes it pass. The privacy tests (US3) inspect the **raw JSON** of responses, never
the typed DTO.

**Organization**: by user story.
- The Foundational phase holds everything every story reads: the model, the rules, the open
  predicate, the DTO builder with its anonymity and hidden-results conditions, the list
  endpoint, and the frontend model/service. The builder's privacy conditions sit there on
  purpose: no increment may ever ship a poll that leaks who chose what.
- **US1** (ask and answer) is the MVP.
- **US2** (notices + Home) and **US3** (privacy suite + the editor's choices) build on it.
- **US4** (closing) and **US5** (edit + delete) follow.
- The account-deletion and legal work is cross-cutting, in the final phase.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: US1–US5 from spec.md

---

## Phase 1: Setup

- [X] T001 Record the baseline on the fresh branch before touching code:
  - run `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~Teams|FullyQualifiedName~Home|FullyQualifiedName~Notifications|FullyQualifiedName~Push|FullyQualifiedName~AccountDeletion|FullyQualifiedName~Terms|FullyQualifiedName~Email"`;
  - in `frontend/`, run `npx nx test web --watch=false --testPathPatterns="team-detail|notification-row|needs-you|catalog-|legal-catalog"`.

  Write down any pre-existing failure here so it is not later mistaken for a regression. *(Done: backend 551/551, frontend subset 117/117; no pre-existing failure.)*

---

## Phase 2: Foundational (blocking: model, rules, predicate, builder, list, client model)

- [X] T002 [P] Create the three entities with XML docs that state the rules in [data-model.md](./data-model.md).
  - `backend/Entities/TeamPoll.cs`: `sealed class TeamPoll : BaseEntity` with `Guid TeamId`, `Guid AuthorUserId`, `string Question`, `bool AllowsMultiple`, `bool IsAnonymous` (doc: **fixed at creation; no code path updates it**, FR-016), `bool ResultsAfterAnswer`, `DateTime? ClosesAt`, `DateTime? ClosedAt` (doc: set only by closing early, never cleared). Navigations: `Team`, `User Author`, `ICollection<TeamPollOption> Options`, `ICollection<TeamPollVote> Votes`.
  - `backend/Entities/TeamPollOption.cs`: `PollId`, `Text`, `Position`, `TeamPoll Poll`.
  - `backend/Entities/TeamPollVote.cs`: `PollId`, `OptionId`, `UserId`, `TeamPoll Poll`, `TeamPollOption Option`, `User User`. Doc: one row per chosen option; "counts" is derived from current membership (R5).
  - In `backend/Entities/Team.cs`, add `ICollection<TeamPoll> Polls`.
- [X] T003 [P] In `backend/Entities/NotificationEnums.cs`:
  - **append** `TeamPoll = 11` to `NotificationType`. Its doc says what it is (feature 062), that it is link-only, and that the payload names no person;
  - add an explicit `NotificationType.TeamPoll => NotificationCategory.TeamNews` arm to `NotificationCategories.For`;
  - extend the `TeamNews` category's XML doc to mention polls.
- [X] T004 [P] In `backend/Dtos/Notifications/NotificationDtos.cs`, add `TeamPollPayload(string TeamSlug, string TeamName, Guid PollId, string Question)`. Its doc says **never add the author's or anyone's name** (037 FR-023; the actor carries the author), and that `TeamSlug`/`TeamName` are what 061's rename rewrite matches.
- [X] T005 [P] Create `backend/Services/Teams/TeamPollRules.cs`, a pure static class (the `TeamDetailsPolicy` precedent). It holds:
  - the constants `QuestionMaxLength = 200`, `OptionMaxLength = 80`, `MinOptions = 2`, `MaxOptions = 10`, `MaxOpenPolls = 10`, `MaxCloseAhead = TimeSpan.FromDays(365)`;
  - the enum `TeamPollCode { Question, OptionCount, OptionLength, OptionDuplicate, ClosesAtPast, ClosesAtTooFar, ChoiceCount, ChoiceUnknown, TooManyOpen, Closed, Answered }`;
  - `ValidateContent(string? question, IReadOnlyList<string?>? options, DateTimeOffset? closesAt, DateTime utcNow)`, which returns either the normalised content (a trimmed question, trimmed options in order, `DateTime? ClosesAtUtc`) or `(TeamPollCode code, int? optionIndex)`. It checks in this order:
    1. question 1–200 after trimming;
    2. option count 2–10;
    3. each option 1–80 after trimming, reporting the index;
    4. duplicates by `Trim()` + `ToLowerInvariant()`, reporting the index of the **later** one;
    5. close date > now, then close date ≤ now + 365d.
  - `ValidateChoice(bool allowsMultiple, IReadOnlyCollection<Guid> distinctIds, IReadOnlySet<Guid> pollOptionIds)` → `null` or `ChoiceCount`/`ChoiceUnknown`.
- [X] T006 [P] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamPollRulesTests.cs`, pure unit tests with no factory. They cover:
  - the question: whitespace-only refused; 200 accepted; 201 refused;
  - option counts: 1 and 11 refused; 2 and 10 accepted;
  - option length: an empty option refused with its index; 80 accepted; 81 refused with its index;
  - duplicates: `"Rot"` / `" rot "` refused with index 1;
  - the close date: `null` accepted; now refused (`ClosesAtPast`); now+366d refused; now+365d accepted;
  - the stored close date is UTC (an input with a `+02:00` offset is converted);
  - choices: single with 2 ids ⇒ `ChoiceCount`; multi with 0 ⇒ `ChoiceCount`; an unknown id ⇒ `ChoiceUnknown`.
- [X] T007 [P] Create `backend/Services/Teams/TeamPollOpen.cs` with `public static Expression<Func<TeamPoll, bool>> At(DateTime utcNow) => p => p.ClosedAt == null && (p.ClosesAt == null || p.ClosesAt > utcNow);` and a doc saying this is THE meaning of open (R4). The list, the guards, the cap and Home all call it. Never re-type it.
- [X] T008 Configure the model in `backend/Data/AppDbContext.cs`, per the data model:
  - add `DbSet`s `TeamPolls`, `TeamPollOptions`, `TeamPollVotes`;
  - `TeamPoll`:
    - `Question` max `TeamPollRules.QuestionMaxLength`, required; index `(TeamId, CreatedDate)`;
    - Team FK **Cascade**;
    - Author FK **Restrict**, with a comment giving the `TeamNewsPost` precedent (the account row is never deleted; an erased author's poll stays);
  - `TeamPollOption`: `Text` max `OptionMaxLength`, required; unique `(PollId, Position)`; Poll FK Cascade;
  - `TeamPollVote`:
    - unique `(OptionId, UserId)` and index `(PollId, UserId)`;
    - Poll FK Cascade and Option FK Cascade;
    - User FK **Restrict**, with a comment: erasure deletes votes explicitly, and Restrict forces nothing because the user row is never deleted (R13).
- [X] T009 Create `backend/Dtos/Teams/TeamPollDtos.cs`, exactly per [the contract](./contracts/team-polls-api.md).
  - Response DTOs: `TeamPollDto`, `TeamPollOptionDto(Guid Id, string Text, int? Count, IReadOnlyList<TeamPollPersonDto>? Voters)`, `TeamPollPersonDto(string? Name, string? Handle)`.
  - Request DTOs: `CreateTeamPollRequest`, `UpdateTeamPollRequest` (**no `IsAnonymous`**, with a doc saying why), `AnswerTeamPollRequest`.
  - Request strings are **nullable**, with loose payload guards (`[MaxLength(20)]` on the option list, `[StringLength(1000)]` per string) and a doc comment: the rules live in `TeamPollRules` so every refusal carries a code (061 R8).
  - `ClosesAt` is `DateTimeOffset?`.
  - Add `public enum TeamPollState { Open, Closed }`.
- [X] T010 Build (`dotnet build backend/JuggerHub.slnx`, gating on its exit code; do not pipe it through grep). Then generate the migration **with** a build: `dotnet ef migrations add AddTeamPolls --project backend`. Open the file and confirm it creates the three tables with the FKs (Cascade/Restrict as configured), the unique indexes and the `(TeamId, CreatedDate)` index. An empty `Up()` means the build was skipped (056).
- [X] T011 Create `backend/Services/Teams/ITeamPollService.cs`:
  - `enum TeamPollStatus { Ok, Created, Invalid, Conflict, Forbidden, TeamNotFound, PollNotFound }`;
  - `record TeamPollResult(TeamPollStatus Status, TeamPollDto? Poll, TeamPollCode? Code, int? OptionIndex, string? Reason)` with small factories;
  - `ListAsync(slug, userId, TeamPollState, PaginationRequest)` → `PagedResult<TeamPollDto>?` (null = team not found / not a member);
  - `CreateAsync`, `UpdateAsync`, `AnswerAsync`, `WithdrawAsync`, `CloseAsync` → `TeamPollResult`;
  - `DeleteAsync` → `TeamPollStatus`.

  Doc every method with its auth rule (member / admin).
- [X] T012 Create `backend/Services/Teams/TeamPollService.cs` with the constructor (`AppDbContext`, `TeamMembershipGuard`, `INotificationService`, `INotificationPreferenceService`, `TeamEmailService`, `ILogger`) and implement **`ListAsync` and the private DTO builder** (research R5/R6):
  1. Resolve access via the guard; not a member ⇒ `null`.
  2. Query the polls: `AsNoTracking`, `TeamId`, open or closed via `TeamPollOpen.At(now)` / its negation. Order open by `CreatedDate` desc and closed by `ClosedAt ?? ClosesAt` desc. Skip/Take; `CountAsync` for the total. Project the poll fields, the options ordered by `Position`, and the author name/handle through `_db.PlayerProfiles.Where(pp => pp.UserId == p.AuthorUserId)` sub-selects. **Never `p.Author.Profile`** (044).
  3. Load the current members: `TeamMemberships` of the team joined to `PlayerProfiles` (the ban filter drops banned players) → `(UserId, DisplayName, Handle)`. This list is the counted set and the denominator.
  4. Load the vote rows `(PollId, OptionId, UserId)` for the page's poll ids.
  5. Build each DTO in memory:
     - keep only votes whose `UserId` is in the member set;
     - `answeredCount` = distinct voters; `memberCount` = set size;
     - `myOptionIds`; `hasAnswers` = any stored vote, **unfiltered**;
     - `isOpen`; `closedAt` = `ClosedAt ?? ClosesAt` when not open, else null;
     - `resultsVisible = !ResultsAfterAnswer || !isOpen || myOptionIds.Any()`;
     - `option.Count` only when `resultsVisible`;
     - `option.Voters` **only when `!IsAnonymous && resultsVisible`**;
     - `notAnswered` **only when `!IsAnonymous && access.IsAdmin`** (members in roster order without a counted vote).

  Put the two identity conditions in clearly named local functions with a comment citing FR-018 and R6. Expose the builder as a private method `BuildAsync(IReadOnlyList<Guid> pollIds, TeamAccess access, Guid viewerId, ct)`, which every mutation reuses to return the caller's view.
- [X] T013 Create `backend/Controllers/TeamPollsController.cs`:
  - `[Route("api/v{version:apiVersion}/teams/{slug}/polls")]`, `[Authorize(JwtBearer)]`, `[ApiVersion("1.0")]`, with a class doc copying `TeamsController`'s access rules;
  - `GET` with `[FromQuery] TeamPollState state` + `PaginationRequest`, returning 200, or 404 "Team not found" with the exact detail `TeamsController.TeamNotFound` uses;
  - a private `Map(TeamPollResult)` for the other actions:
    - `Invalid` ⇒ 400 and `Conflict` ⇒ 409 ProblemDetails `title: "Invalid poll"` / `"Poll conflict"`, with `Extensions["code"]` = the camelCase code and `Extensions["option"]` when an index is set (the 061 `UpdateDetails` shape);
    - `Forbidden` ⇒ 403;
    - `TeamNotFound` ⇒ 404 "Team not found";
    - `PollNotFound` ⇒ 404 `title: "Poll not found"`.

  Register `AddScoped<ITeamPollService, TeamPollService>()` in `backend/Program.cs` beside `ITeamNewsService`.
- [X] T014 [P] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamPollTestSupport.cs`, shared helpers (the private helpers of `TeamNewsEditDeleteTests` lifted into one reusable base):
  - `NewUserAsync`, `TeamWithMembersAsync(int members)`, `JoinAsync`, `LeaveAsync`, `SetRoleAsync`;
  - `CreatePollAsync(admin, slug, question, options, allowsMultiple, isAnonymous, resultsAfterAnswer, closesAt)` → the JSON;
  - `ListAsync(player, slug, state)` → the raw body string **and** the parsed JSON;
  - `AnswerAsync`, `WithdrawAsync`, `CloseAsync`, `UpdateAsync`, `DeleteAsync`, `ProblemCodeAsync(resp)`;
  - `WithDbAsync` for direct reads.
- [X] T015 [P] Frontend model, service and util:
  - `frontend/apps/web/src/app/core/models/poll.models.ts`: `TeamPoll`, `TeamPollOption`, `TeamPollPerson`, `CreateTeamPoll`, `UpdateTeamPoll`, `TeamPollState`, `TeamPollErrorCode` (union of the contract's codes).
  - `frontend/apps/web/src/app/core/services/poll.service.ts`, providedIn root and stateless: `list(slug, state, skip, take)`, `create`, `update`, `answer(slug, id, optionIds)`, `withdraw`, `close`, `remove`, against `…/teams/{slug}/polls`.
  - `frontend/apps/web/src/app/core/utils/poll-close-time.ts` + `.spec.ts`: `toUtcInstant(local: string): string | null` (empty ⇒ null; `new Date(local).toISOString()`) and `toLocalInputValue(iso: string | null): string` (UTC → `yyyy-MM-ddTHH:mm` in the viewer's zone). The spec pins a fixed zone via the date values and round-trips.

**Checkpoint**: the backend builds; T006 and the util spec are green; the frontend compiles.
Commit `feat(062): poll model, rules and the list endpoint (#365)`.

---

## Phase 3: User Story 1 - An admin asks, the team answers (Priority: P1) 🎯 MVP

**Goal**: Admins start polls, and members answer, change or withdraw and see the result on the
team page.

**Independent Test**: spec US1. As an admin, start a named single-choice poll; two members
answer differently and one changes. Each sees one answer per member, the names, "2 of N", and
their own choice. A non-member sees no card and gets 404 on the list.

### Tests for User Story 1

- [X] T016 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamPollTests.cs` (collection `"Teams"`, base `TeamPollTestSupport`) covering US1:
  - create 201 returns the DTO with options in order, `isOpen`, the author name/handle, `memberCount`, and `answeredCount: 0`;
  - a plain member's create gets **403**; an outsider's create and list get **404 "Team not found"**, the same as for an unknown slug;
  - answer single ⇒ `myOptionIds` and a count of 1; answering another option moves it (counted once);
  - multi: two options ⇒ both counts 1, `answeredCount` 1;
  - withdraw ⇒ unanswered, and withdrawing again is still 200;
  - coded refusals: `choiceCount` (single + 2 ids), `choiceUnknown` (another poll's option id);
  - the list splits open and closed and is paged;
  - named poll: `voters` carry names and handles; an admin gets `notAnswered` while a member gets `null`;
  - **the cap**: the 11th create gets **409 `tooManyOpen`**;
  - **concurrency**: 10 parallel answers from one member to a single-choice poll with different option ids leave exactly **one** vote row (`WithDbAsync`);
  - **membership**: a member who answered then leaves ⇒ counts and names drop, and after rejoining it counts again;
  - `ModifiedDate` is not touched by reads.

### Implementation for User Story 1

- [X] T017 [US1] Implement `CreateAsync` in `backend/Services/Teams/TeamPollService.cs`:
  1. The guard: not a member ⇒ `TeamNotFound`; not an admin ⇒ `Forbidden`.
  2. `TeamPollRules.ValidateContent` ⇒ `Invalid` + code/index.
  3. `CreateExecutionStrategy().ExecuteAsync`, with inside the delegate:
     - `ChangeTracker.Clear()`, begin a transaction;
     - `SELECT 1 FROM "Teams" WHERE "Id" = {teamId} FOR UPDATE`;
     - count open polls via `TeamPollOpen.At(now)`; ≥ `MaxOpenPolls` ⇒ return `Conflict/TooManyOpen`;
     - create the `TeamPoll` + `TeamPollOption`s (Position = index) **inside the delegate**, `SaveChanges`, commit.
  4. Return `Created` with the builder's DTO.

  Leave a clearly marked `// US2: notify after commit` spot, placed after the commit.
- [X] T018 [US1] Implement `AnswerAsync` and `WithdrawAsync` in `backend/Services/Teams/TeamPollService.cs`:
  1. The guard (member).
  2. Strategy delegate:
     - `ChangeTracker.Clear()`, begin a transaction;
     - `SELECT 1 FROM "TeamPolls" WHERE "Id" = {pollId} AND "TeamId" = {teamId} FOR UPDATE`;
     - **then** read the poll (open? `AllowsMultiple`, option ids) with a normal query; missing ⇒ `PollNotFound`; not open ⇒ `Conflict/Closed`;
     - answer only: `ValidateChoice` on the **distinct** ids;
     - `ExecuteDelete` of this user's votes for the poll; answer only: `AddRange` new `TeamPollVote`s; `SaveChanges`; commit.
  3. Return `Ok` with the DTO.

  Comment on why the lock is needed (R3: it serialises against edit/close and a double submit; one-answer is enforced here, not by an index).
- [X] T019 [US1] Add the actions to `backend/Controllers/TeamPollsController.cs`: `POST` (201 via `StatusCode(201, dto)`), `PUT {pollId:guid}/answer`, `DELETE {pollId:guid}/answer`. Make T016 green.
- [X] T020 [P] [US1] Create the card `frontend/apps/web/src/app/features/teams/team-detail/polls/team-polls.component.{ts,html,css}` (selector `jh-team-polls`):
  - inputs `slug`, `isAdmin`;
  - signals `open`, `closed`, `closedTotal`, `loading`, `loadError`, `notice`, `creating`;
  - on slug change, load `open` (take 10) and `closed` (take 5); "Show older polls" appends the next closed page;
  - heading `teams.polls.title` with `tabindex="-1"` and an id;
  - admins get a **secondary** "Start a poll" button that opens `jh-poll-editor` inline at the top;
  - the empty state (`jh-empty-state inline`, `teams.polls.empty`, plus `teams.polls.emptyAdmin` for admins);
  - the load error per DESIGN.md (sentence + Try again);
  - the list renders `jh-poll-item` per poll, with `track poll.id`, open ones first;
  - `jh-card overflowVisible`, because the menus drop below.

  Replace a poll in its list from each child's `changed` output; move it between lists when `isOpen` flips; remove it on `removed`.
- [X] T021 [P] [US1] Create `frontend/apps/web/src/app/features/teams/team-detail/polls/poll-item.component.{ts,html,css}` (selector `jh-poll-item`, input `poll`, `slug`, `isAdmin`, output `changed(TeamPoll)`/`removed({gone})`). It shows:
  - the question as `h3` with `id="poll-{id}"` heading, `tabindex="-1"`, `break-words`;
  - the meta line (author or `alerts.row.formerPlayer`, relative time, closes/closed time);
  - chips (`jhChip`): named/anonymous, results timing, several answers, closed;
  - "N of M answered".

  **Answer controls** (R15):
  - full-width `<button>` per option, ≥44px, `aria-pressed`, in a `role="group"` labelled by the question;
  - single-choice records on press, and pressing the current option does nothing;
  - multi-choice toggles a local `selection` signal and saves via a **Save answer** button; an empty selection sends `withdraw`;
  - a "Withdraw my answer" text button when answered and open;
  - `busy` disables the controls; no auto-retry.

  **Results** when `resultsVisible`:
  - a soft `brand-primary` fill bar per option, sized by count/answered;
  - the count in `font-mono`;
  - the own choice marked with the `check` icon + a sr-only "your answer";
  - voters as a caption of `/u/{handle}` links;
  - the admin `notAnswered` caption.

  Hidden results show the caption `teams.polls.resultsAfterAnswerHint` and no bars. The question and options are plain interpolation only.

  **Errors map from `code`/status** to `teams.polls.error.*`:
  - `409 closed` ⇒ emit `changed` after re-fetching the list, and show `teams.polls.error.closed`;
  - `404` ⇒ emit `removed({gone:true})`.
- [X] T022 [P] [US1] Create `frontend/apps/web/src/app/features/teams/team-detail/polls/poll-editor.component.{ts,html,css}` (selector `jh-poll-editor`):
  - inputs: `mode: 'create' | 'edit'`, an optional `poll`, `slug`, `locked` (has answers);
  - outputs `saved(TeamPoll)`, `cancelled`;
  - every field a signal: `question` (static `maxlength="200"` attribute), `options` (2–10 rows, add/remove, `maxlength="80"`), `allowsMultiple`, `isAnonymous` (create only), `resultsAfterAnswer`, `closesAtLocal` (optional `datetime-local` + Clear);
  - the three binary choices are `fieldset`/`legend` groups of two native radios, each with a one-line explanation. The anonymous explanation is `teams.polls.editor.anonymousHint`, "Nobody sees who chose what" (FR-019 wording);
  - submit is **secondary** (`teams.polls.editor.start` / `teams.polls.editor.save`), Cancel is ghost;
  - client checks mirror only the lengths and counts, for UX;
  - server codes map to field-level messages (`option` index ⇒ that row);
  - in edit mode with `locked`, the content fields are disabled with `teams.polls.editor.lockedHint` and only the close time stays editable.

  US1 uses the create mode; US5 wires the edit mode.
- [X] T023 [US1] Mount the card in `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.html`, inside the members-only block **directly before the News card**, as `<jh-team-polls [slug]="team.slug" [isAdmin]="isAdmin()" />`, with a comment citing R14 and the alternative. Import the component in `team-detail.component.ts`.
- [X] T024 [US1] Add the `teams.polls.*` keys for US1 in **all three** catalogues in one commit: `frontend/apps/web/public/i18n/{en,de,es}.json`. They cover:
  - title, start, empty, emptyAdmin;
  - answered "{{answered}} of {{members}} answered";
  - by/asked, closes/closed;
  - chip labels, yourAnswer, withdraw, saveAnswer, notAnswered, resultsAfterAnswerHint, showOlder;
  - loading/loadError/retry;
  - editor labels, hints and validation;
  - `error.*` for every contract code, and `error.generic`.

  German uses *Umfrage*, *Antwort*, *antworten*; Spanish uses *encuesta*. Use German `–`, never `—` (the copy rule).
- [X] T025 [P] [US1] Specs:
  - `team-polls.component.spec.ts`: loads both lists; empty state for member vs admin; the start button only for admins; "Show older";
  - `poll-item.component.spec.ts`:
    - single-choice answers on one press;
    - multi-choice save; an empty save withdraws;
    - hidden results render no bars;
    - voters and notAnswered render when present;
    - a `409 closed` shows the closed message;
    - a `404` emits `removed`;
  - `poll-editor.component.spec.ts`: 2..10 rows; codes map to fields; submit is secondary; anonymity absent in edit mode.

  Fix any `team-detail.component.spec.ts` setup the new child needs (provide a `PollService` stub).

**Checkpoint**: US1 works end to end; the backend and frontend suites are green. Commit
`feat(062): admins start polls, members answer on the team page (#365)`.

---

## Phase 4: User Story 2 - Members hear that a poll is waiting (Priority: P1)

**Goal**: Opening a poll notifies every other member on the channels they allow. An unanswered
poll waits in Home's *Needs you*. Everything deep-links to the poll.

**Independent Test**: spec US2.
- A team with 3 other members: each gets exactly one Alerts row (team + question), an email in
  their language where email is on, and a device notice without the question.
- The author gets nothing.
- Each sees the Home item until answering.

### Tests for User Story 2

- [X] T026 [P] [US2] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamPollNotificationTests.cs`:
  - on create, every member except the author has exactly one `TeamPoll` row whose payload is `{teamSlug, teamName, pollId, question}` with no name key, and the actor is the author;
  - an outsider has none;
  - a member with *Team news → Email* off gets the row but no email (`TestEmailSender`);
  - a German-preference member's email is German (subject + body marker);
  - `FakePushDispatcher` shows one push per enabled device, whose body does **not** contain the question and whose url is `/t/{slug}#poll-{id}`;
  - with *Team news → In-app* off, push still goes (055's independence);
  - a notification failure never fails the create.
- [X] T027 [P] [US2] Extend `backend/tests/JuggerHub.Api.IntegrationTests/Notifications/NotificationCategoryMappingTests.cs` so that `TeamPoll` maps to `TeamNews`. Extend `backend/tests/JuggerHub.Api.IntegrationTests/Push/PushComposerTests.cs` with the TeamPoll title (team name), a fixed body without the question in en/de/es, and the url. The exhaustive guard must pass.
- [X] T028 [P] [US2] Extend `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamRenameRewriteTests.cs`: a delivered `TeamPoll` row shows the new team name after a rename. It is the tenth kind; update the count/comment that says nine.
- [X] T029 [P] [US2] Extend `backend/tests/JuggerHub.Api.IntegrationTests/Home/NeedsYouTests.cs`. The item is:
  - listed for a member with `kind: "TeamPoll"`, `params.question`, `params.teamSlug`, `linkTarget` = the slug;
  - not listed for the author;
  - gone after answering, after close, after delete, and after leaving the team;
  - not listed for a poll whose close time passed.

### Implementation for User Story 2

- [X] T030 [US2] In `TeamPollService.CreateAsync`, at the US2 spot **after commit**:
  - recipients = current memberships except the author;
  - `CreateManyAsync(recipients, TeamPoll, new TeamPollPayload(slug, name, pollId, question), actorUserId: author, dedupeKeyPrefix: PollDedupePrefix(pollId))` in `try/catch`, logging only ids;
  - then email: `GetEnabledRecipientsAsync(recipients, TeamNews, Email)` → load `(Email, PreferredLanguage)` → per recipient `SendTeamPollEmailAsync(…, SupportedLanguages.ResolveOrDefault(lang))` in its own `try/catch`.

  Add `private static string PollDedupePrefix(Guid id) => $"poll:{id}";` with a doc: create, edit and delete all find rows by it.
- [X] T031 [P] [US2] Email:
  - create `backend/EmailTemplates/{en,de,es}/team-poll.html`, copying the structure of `join-request.html` with the variables `EMAIL_TITLE`, `TEAM_NAME`, `QUESTION`, `AUTHOR_NAME`, `POLL_URL` (RawHtml), `FOOTER_REASON`;
  - add `GenerateTeamPollEmailAsync(teamName, question, authorName, pollUrl, culture)` to `IEmailTemplateService` and `backend/Services/EmailTemplateService/EmailTemplateService.cs`;
  - add the keys `subject.teamPoll`, `title.teamPoll` and `footer.teamPoll` ×3 to `backend/Services/Email/EmailLocalizer.cs`. The subject is "{0} started a poll" with **no question**. A mail app's lock-screen preview shows the subject, and the owner kept the question off lock screens (FR-028's reasoning); the question is in the body;
  - add `SendTeamPollEmailAsync(toEmail, teamName, slug, pollId, question, authorName, culture, ct)` to `backend/Services/Email/TeamEmailService.cs`, with the link `…/t/{slug}#poll-{pollId}`.

  `TemplateParityTests` must stay green.
- [X] T032 [P] [US2] Push: in `backend/Services/Notifications/Push/PushContentComposer.cs`, add `TeamPoll` to the `teamName` title arm, a body arm `_localizer.Get("teamPoll.body", culture)`, and the url arm `/t/{slug}#poll-{pollId}` (from `Slug(payload,"teamSlug")` + `Id(payload,"pollId")`; either missing ⇒ `/t/{slug}` or `/`). In `backend/Services/Notifications/Push/PushLocalizer.cs`, add `teamPoll.body` in en/de/es ("started a poll" / "hat eine Umfrage gestartet" / "ha abierto una encuesta"), with no placeholder for the question.
- [X] T033 [P] [US2] Settings copy: in `backend/Services/Notifications/NotificationPreferenceService.cs`, change the `CategoryCopy[TeamNews]` description ×3 to the R12 texts. Update any test asserting the old text (`PreferenceTests`).
- [X] T034 [US2] Home:
  - in `backend/Dtos/Home/HomeDtos.cs`, **append** `TeamPoll` to `NeedsYouKind` (doc: link-only, answered on the team page) and add `string? Question` to `NeedsYouParamsDto` (doc: TeamPoll only);
  - in `backend/Services/Home/HomeService.cs` `LoadNeedsYouAsync`, add the R11 query using `TeamPollOpen.At(now)` and concat it, and update the method's remarks.

  Make T029 green.
- [X] T035 [P] [US2] Alerts row:
  - in `frontend/apps/web/src/app/core/models/notification.models.ts`, add `'TeamPoll'`, `TeamPollPayload` and `isTeamPoll`;
  - in `frontend/apps/web/src/app/features/alerts/notification-row/notification-row.component.{ts,html}`:
    - title `alerts.row.teamPollTitle` ("{{team}} asks");
    - supporting = the question (wraps, not truncated);
    - icon `list-checks`, or an existing glyph; check `jh-icon`'s set and add none without need;
    - `link()` = `/t/{slug}` plus a new `fragment()` = `poll-{pollId}`, and the anchor binds `[fragment]`.

  Extend `notification-row.component.spec.ts`.
- [X] T036 [P] [US2] Needs-you card:
  - in `frontend/apps/web/src/app/core/models/home.models.ts`, add `'TeamPoll'` and `question`;
  - in `frontend/apps/web/src/app/features/dashboard/modules/needs-you-card.component.{ts,html}`:
    - `title()` → `home.needsYouItem.teamPollTitle`, `contextName()` → the question;
    - `link()` → `['/t', slug]` with a new `fragment()` → `poll-{id}` (bind `[fragment]` on the headline link);
    - add a `@case ('TeamPoll')` rendering one secondary **Answer** link (`home.needsYouItem.answerPoll`) with the same route and fragment. There are no accept/decline buttons.

  Extend `needs-you-card.component.spec.ts`.
- [X] T037 [US2] Deep link in `team-polls.component.ts`:
  - read `ActivatedRoute.fragment`;
  - after the lists load, when the fragment is `poll-{id}` and that poll is rendered, `afterNextRender` → `scrollIntoView({block:'start'})` + focus `#poll-{id}`; otherwise focus the card heading;
  - react to fragment changes while on the page.

  Add spec coverage.
- [X] T038 [US2] Add the keys `alerts.row.teamPollTitle`, `home.needsYouItem.teamPollTitle` and `home.needsYouItem.answerPoll` ×3 in one commit. Make T026–T029 green.

**Checkpoint**: Commit `feat(062): a new poll reaches the team; unanswered polls wait on Home (#365)`.

---

## Phase 5: User Story 3 - An anonymous poll stays anonymous (Priority: P2)

**Goal**: Admins can choose *anonymous* and *results after answering*. No response ever connects
a member to an option, and hidden results carry no counts.

**Independent Test**: spec US3 + US1-9. The raw JSON for the author, a second admin and a member
contains no other voter's identity, and hidden results carry no counts until an answer.

- [X] T039 [P] [US3] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamPollPrivacyTests.cs`, all assertions on the **raw response body string**:
  - anonymous poll, 3 voters: for the author, a second admin and a member, on `GET list`, the `answer` response, the `close` response and the `update` response:
    - no other voter's handle or display name appears;
    - `voters` is null everywhere and `notAnswered` is null;
    - `myOptionIds` equals the caller's own answer only;
  - the Home body for a member contains only their own unanswered poll;
  - hidden results (`resultsAfterAnswer: true`), for a member who has not answered, including when that member is the author or an admin:
    - no `"count":` number, and `voters` null;
    - `answeredCount` present;
    - after answering, the counts appear; after withdrawing, they are gone again; after close, they are visible to all;
  - `UpdateTeamPollRequest` JSON carrying `"isAnonymous": false` for an anonymous poll leaves it anonymous (the field is ignored).
- [X] T040 [US3] Complete the editor's choices in `poll-editor.component.*`: the anonymous and results-timing fieldsets with their explanations (present in create; anonymity absent in edit, per T022). In `poll-item.component.*`:
  - the anonymous notice line `teams.polls.anonymousNotice` ("Nobody sees who chose what") shown **before** answering;
  - the results-timing hint;
  - the chips.

  Extend both specs (the notice is present for anonymous polls; there are no voters/notAnswered even if the input carried them, as a belt-and-braces render guard).
- [X] T041 [US3] Add the US3 keys ×3 in one commit. Make T039 green.

**Checkpoint**: Commit `feat(062): anonymous polls and results after answering (#365)`.

---

## Phase 6: User Story 4 - A poll closes and its result stays (Priority: P2)

**Goal**: A poll closes by its time or early, final, and its close time can move while open.

**Independent Test**: spec US4.
- After a near close time passes, answers are refused and the result stays.
- An early close by a second admin is final.
- Moving the close time after answers keeps the answers.

- [X] T042 [P] [US4] Extend `TeamPollTests.cs`:
  - a poll whose `ClosesAt` is set into the past **directly in the DB** (`WithDbAsync`, `ExecuteUpdate` with `ModifiedDate`) reads `isOpen: false`, `closedAt == closesAt`, is listed as closed, and refuses answer/withdraw with **409 `closed`**;
  - `POST close` by a second admin ⇒ 200 `isOpen: false`, `ClosedAt` set, and `ModifiedDate` moved; a second close ⇒ 409 `closed`; a plain member ⇒ 403;
  - closed polls are ordered by the close moment;
  - **close-vs-answer race**: parallel close + answer end either closed-with-the-answer or closed-without-it, and an answer after the close never lands;
  - `PUT` changing only `closesAt` on an answered open poll ⇒ 200, with the answers and option ids unchanged;
  - `closesAtPast` / `closesAtTooFar` on update.
- [X] T043 [US4] Implement in `TeamPollService.cs`:
  - **`CloseAsync`**: the guard (admin), then a single `ExecuteUpdate … WHERE Id ∧ TeamId ∧ TeamPollOpen.At(now)` setting `ClosedAt = now` and **`ModifiedDate = now`**. 0 rows ⇒ an existence check (`Conflict/Closed` vs `PollNotFound`).
  - **`UpdateAsync`**, the whole of it except the alert rewrite, which US5 adds in T047. It runs as a strategy delegate:
    1. the guard (admin); `ChangeTracker.Clear()` → transaction → `SELECT 1 FROM "TeamPolls" WHERE "Id" = … AND "TeamId" = … FOR UPDATE`;
    2. read the poll, its options and `HasAnswers`; missing ⇒ `PollNotFound`; not open ⇒ `Conflict/Closed`;
    3. `ValidateContent` ⇒ `Invalid`;
    4. `contentChanged` = the question, the option texts in order, `AllowsMultiple` or `ResultsAfterAnswer` differ;
    5. `contentChanged && hasAnswers` ⇒ `Conflict/Answered`, with **nothing written**, not even the close time;
    6. only if the options changed: `ExecuteDelete` the options and `AddRange` new ones created inside the delegate. **Never replace unchanged options**, and comment why: moving the close time would otherwise cascade-delete every answer;
    7. `ExecuteUpdate` of `Question`, `AllowsMultiple`, `ResultsAfterAnswer`, `ClosesAt` and **`ModifiedDate`**; `SaveChanges`; commit.

  Add the `POST {pollId:guid}/close` and `PUT {pollId:guid}` routes in the controller.
- [X] T044 [US4] Frontend: in `poll-item.component.*`, add the admin menu (`jh-news-post`'s idiom: `ellipsis`, `role="menu"`, Escape + outside click on its **own** wrapper) with *Close now* and *Change closing time*.
  - *Close now* opens the fixed confirm dialog (`news-post` idiom; safe answer focused; Tab trapped; bottom sheet at 375px).
  - The closed state renders the final result, a "Closed" chip and the close moment.
  - A 409 `closed` shows `teams.polls.error.alreadyClosed` and refreshes.

  The card moves a poll to the closed list on `changed` with `isOpen: false`. Add the keys ×3 and extend the specs.

**Checkpoint**: Commit `feat(062): polls close by their time or early (#365)`.

---

## Phase 7: User Story 5 - Admins correct or remove a poll (Priority: P3)

**Goal**: Admins edit the content until the first answer, silently correcting delivered alerts,
and delete a poll with its alerts.

**Independent Test**: spec US5.
- Editing before an answer updates the card and every recipient's Alerts row with nothing
  re-sent.
- After an answer, the content is locked.
- Delete leaves no trace for anyone, including former members.

- [X] T045 [P] [US5] Extend `TeamPollTests.cs`:
  - an edit before any answer replaces the question/options/one-many/results timing and returns the DTO;
  - with an answer: a content change ⇒ **409 `answered`** and nothing applied (the close time in the same request is not applied either); unchanged content with a moved close time ⇒ 200;
  - **edit-vs-first-answer race** (parallel): the outcome is either (edit applied and answer to a new option id valid) or (answer stored and edit 409); never a vote row referencing a missing option, never a lost answer;
  - edit of a closed poll ⇒ 409 `closed`;
  - delete ⇒ 204; the poll, options and votes are gone; a second delete ⇒ 404 "Poll not found";
  - a plain member's edit/delete ⇒ 403;
  - a poll id of another team under this slug ⇒ 404 "Poll not found".
- [X] T046 [P] [US5] Extend `TeamPollNotificationTests.cs`:
  - a question edit rewrites every recipient's row payload `question` **including a former member's**, with `IsRead`/`CreatedDate` unchanged, `ModifiedDate` moved, and no realtime `created`, email or push (`FakeNotificationRealtime`, `TestEmailSender`, `FakePushDispatcher`);
  - editing only options (question unchanged) leaves the rows untouched;
  - delete removes every recipient's row, including a former member's, lowers unread counts, and sends a badge refresh to recipients who lost an unread row, after commit.
- [X] T047 [US5] Add the silent alert correction to `UpdateAsync` in `TeamPollService.cs`. Inside the same strategy delegate, after the poll's `ExecuteUpdate` and before the commit: when the question changed, call `_notifications.ReplacePayloadAsync(NotificationType.TeamPoll, PollDedupePrefix(pollId), new TeamPollPayload(<current team slug>, <current team name>, pollId, <new question>), ct)`. Read the team's slug and name inside the delegate so that a concurrent rename is respected. Nothing is pushed (FR-030).
- [X] T048 [US5] Implement `DeleteAsync` (057's shape):
  - strategy tx: `ExecuteDelete` the poll `WHERE Id ∧ TeamId`; 0 ⇒ `PollNotFound` (and never reach the alerts); `DeleteManyAsync(TeamPoll, prefix)`; commit;
  - then `RefreshUnreadBadgesAsync(recipients)`.

  Add the `DELETE {pollId:guid}` route. Make T045/T046 green.
- [X] T049 [US5] Frontend:
  - the menu gains *Edit*, which opens `jh-poll-editor` in edit mode in place of the poll, with the content fields locked when `hasAnswers`, and *Delete*, which opens the confirm dialog with `variant="danger"` and focus on *Keep poll*;
  - the body `teams.polls.deleteBody` says the poll, its answers and its notices disappear for everyone;
  - a 409 `answered` from the editor shows `teams.polls.error.answered` and switches the editor to locked;
  - a delete 404 ⇒ `removed({gone:true})`, and the card shows `teams.polls.gone`.

  Add the keys ×3 and extend the specs.

**Checkpoint**: Commit `feat(062): admins edit a poll before its first answer, or delete it (#365)`.

---

## Phase 8: Polish & Cross-Cutting (account deletion, published texts, verification)

- [X] T050 Account deletion (FR-039):
  - in `backend/Services/Account/AccountDeletionService.cs`, add `await _db.TeamPollVotes.Where(v => v.UserId == userId).ExecuteDeleteAsync(ct);` in the *Participation* block, with a comment that the Restrict FK forces nothing, so the test is the only guard;
  - append `"Polls"` to `RetainedCategories`, after `"NewsPosts"`;
  - add `account.delete.retained.Polls` ×3 ("Polls you started stay with the team, with no author" / de / es).

  In `backend/tests/JuggerHub.Api.IntegrationTests/AccountDeletion/AccountErasureTests.cs`:
  - an erased voter's votes are gone and the counts drop;
  - an erased admin's poll survives with `authorName: null`;
  - the preview lists `Polls`.
- [X] T051 Published texts (FR-040, R13), **German first**, in `frontend/apps/web/public/i18n/legal/de.json`, then `en.json` and `es.json`. Rewrite the terms `endingIt` sentence and the privacy `rights` sentence that enumerate "messages … and your news posts" to the **category** ("what you posted to a team or event"), keeping the rest of each paragraph. Then bump the Terms version to the ship date in:
  - `backend/Common/TermsOptions.cs` (the default **and** the `ResolvedVersion` fallback);
  - `backend/appsettings.json` `Terms:CurrentVersion`;
  - `terms.version` + `terms.lastUpdated` in all three catalogues.

  Run `TermsVersionParityTests`, `TermsAcceptanceRegistrationTests` and `legal-catalog.spec.ts`.
- [X] T052 [P] Amendment notes:
  - `specs/005-team-space/spec.md`: under the existing update callouts, add a line saying polls, out of scope there, are delivered by 062;
  - `specs/037-account-deletion/data-model.md`: `TeamPollVotes` erased, `TeamPolls` retained (author placeholder).
- [ ] T053 Create `specs/062-team-polls/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and verify each item against the diff. DESIGN.md wins. Record the radio-group gap (R15) as a DESIGN.md question, not a local style, and the binding case (German at 375px, see plan Gate 7).
- [ ] T054 Run everything:
  - `dotnet test backend/JuggerHub.slnx` (gate on the exit code);
  - in `frontend/`: `npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`;
  - rebuild the containers (`docker compose up -d --build backend frontend`);
  - run the e2e suites that open team pages (`trainings`, `onboarding`) with `BASE_URL=http://localhost:3000 MAILPIT_URL=http://localhost:8025`, and fix `frontend/apps/web-e2e/src/support/*` helpers if the page change broke them.
- [ ] T055 Browser walk (the owner's standing rule): walk [quickstart.md](./quickstart.md) scenarios 1–16 in German at **375px and desktop**, one Playwright context per actor, calling `waitFor()` before asserting. Save screenshots of the binding cases for the PR. Read the driver's output, since false passes happen. Fix what the walk finds.
- [ ] T056 Open the PR `feat(062): team polls (#365)` with `Closes #365`. The PR body lists:
  - the nine owner decisions;
  - the legal/Terms change;
  - the residuals from the plan;
  - the screenshots.

  Update the memory file `team-polls-062-decisions.md` with anything the walk taught.

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002–T015)** → the user stories.
- **US1 (T016–T025)** is the MVP and depends only on Foundational.
- **US2 (T026–T038)** depends on US1's create (T017).
- **US3 (T039–T041)** depends on US1. The privacy conditions themselves are already in T012, so
  US3 adds proof and UI.
- **US4 (T042–T044)** depends on US1.
- **US5 (T045–T049)** depends on US4's `UpdateAsync` shell and the menu (T043–T044).
- **Polish (T050–T056)**: T050/T051 can start any time after Foundational. T053–T056 come last.

### Within stories

Tests are written first and must be seen failing. Backend comes before the frontend that calls
it. Every story's i18n lands ×3 in one commit (`catalog-parity.spec.ts`).

## Parallel Opportunities

- Foundational: T002, T003, T004, T005, T006, T007, T014 and T015 touch different files.
- US1: T020, T021 and T022 (three components) in parallel after T015; T016 alongside T017.
- US2: T026–T029 (tests), T031, T032, T033, T035 and T036 are independent files.
- US4/US5: the backend tests (T042, T045, T046) in parallel with their frontend work once the
  routes exist.

### Parallel example: User Story 2

```text
Task: "T031 email templates ×3 + localizer + TeamEmailService"
Task: "T032 push composer arms + PushLocalizer ×3"
Task: "T035 Alerts row TeamPoll rendering + fragment"
Task: "T036 Needs-you card TeamPoll case + Answer link"
```

## Implementation Strategy

1. **MVP = Phase 1 + 2 + US1**: admins ask, members answer, on the team page. It is already safe
   on privacy, because the builder's conditions are in T012.
2. **+ US2**: people hear about it. This is the highest remaining value, since nobody answers a
   question they never see.
3. **+ US3**: the privacy suite and the anonymous/results-timing UI.
4. **+ US4, US5**: closing, correcting, deleting.
5. **Polish**: erasure, legal + Terms version, the checklist, the walk, the PR.

Commit at each checkpoint with the message given there.
