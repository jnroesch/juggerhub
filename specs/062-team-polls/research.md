# Research: Team Polls (062)

Every decision below was taken by reading the code as it stands on `main` after 061 (PR #377).
Where a decision rests on a specific line, the file is named so the implementer can re-check it
rather than trust this document.

---

## R1 — Where the code lives

**Decision**: A new `TeamPollsController` routed at `api/v1/teams/{slug}/polls`, a new
`ITeamPollService`/`TeamPollService` in `backend/Services/Teams/`, a pure `TeamPollRules` class
(validation and error codes) and a shared open-predicate `TeamPollOpen` beside it. DTOs go in
`backend/Dtos/Teams/TeamPollDtos.cs`.

**Rationale**: `TeamsController` already takes eleven constructor dependencies and holds
everything from creation to logos, so it has no room for a seventh endpoint family. Polls are
team-scoped and use `TeamMembershipGuard` exactly as news does, so the service belongs to the
team domain. `PartyInvitationsController` is the precedent for a second controller over one
domain. The class-level XML comment on `TeamsController` (reads are member-only, non-member ≡
unknown team) applies unchanged and is repeated on the new controller.

**Alternatives rejected**:
- Adding the routes to `TeamsController`, which would make it longer again for no gain.
- A `Services/Polls/` namespace, which would suggest polls could belong to other owners. The
  spec forbids that (FR-041).

---

## R2 — Three tables, one vote row per chosen option

**Decision**: `TeamPoll`, `TeamPollOption` and `TeamPollVote(PollId, OptionId, UserId)`. A
multi-choice answer is several vote rows. "The member answered" means at least one vote row
exists for `(PollId, UserId)`.

The spec's "one answer per member" (FR-008) and "exactly one option in a one-answer poll"
(FR-007) are enforced by the service under the poll row lock (R3), not by an index.

**Rationale**: These are the spec's three entities with nothing added. Counting per option is a
`GROUP BY OptionId`. Deleting a poll or an option cascades to its votes. Erasing an account is
one `ExecuteDelete` by `UserId`.

**Alternatives rejected**:
- **Answer + AnswerChoice** (two tables): the unique `(PollId, UserId)` makes "one answer"
  structural. But R3 must lock the poll row anyway to serialise answers against edits and
  closing, and inside that lock a second table buys nothing. A fourth entity would be
  complexity without a job.
- **A `uuid[]` of chosen options on one row** (the `Party.PositionsNeeded` precedent): counting
  per option would need `unnest` in raw SQL, and the options could not be foreign-keyed.
- **A partial unique index for single-choice polls**: not expressible, because the one-or-many
  setting lives on the poll, not on the vote.

---

## R3 — Concurrency: what locks what

Every multi-step write runs as ONE `CreateExecutionStrategy` unit with all mutation inside the
delegate (constitution VII). Each delegate begins with `ChangeTracker.Clear()` and creates its
entities inside, so a replay re-creates rather than re-attaching (061's lesson).

| Operation | Serialised by | Why |
|---|---|---|
| Start a poll | `SELECT 1 FROM "Teams" WHERE "Id" = … FOR UPDATE` (the `TeamService` idiom, `TeamService.cs:468`) | The 10-open cap (FR-005) is a count-then-insert. Without the lock, two admins at 9 open both see 9 and the team ends with 11. |
| Answer, withdraw | `SELECT 1 FROM "TeamPolls" WHERE "Id" = … FOR UPDATE`, then re-read the poll | Serialises against editing (FR-023: an answer must never name an option an edit just removed — US5 scenario 3) and against closing (FR-020). It also serialises two answers from the same member, which keeps "one answer" true without an index (R2). |
| Edit (question/options/settings/close date) | the same poll row lock | The "no answers yet" check and the option replacement must see committed truth (FR-023). |
| Close early | a single conditional `ExecuteUpdate … WHERE Id ∧ TeamId ∧ open(now)` | An `UPDATE` takes the row lock itself. If an answer holds the lock, the `UPDATE` waits and then re-evaluates its `WHERE` against the committed row, which is Postgres READ COMMITTED semantics. 0 rows means the poll was already closed or is gone. |
| Delete | `ExecuteDelete` of the poll (cascades options and votes) + `DeleteManyAsync` of its alerts, in one transaction (057's shape) | The poll and its notices go together or not at all (FR-024). |

**Every `ExecuteUpdate` sets `ModifiedDate`** (constitution III, Gate 2): close, edit, and the
alerts' payload rewrite (the engine already does that one).

---

## R4 — Open and closed are derived, never swept

**Decision**:
- `ClosesAt` (nullable) is the scheduled close.
- `ClosedAt` (nullable) is set only by closing early.
- A poll is open iff `ClosedAt == null && (ClosesAt == null || ClosesAt > now)`. This is written
  ONCE as `TeamPollOpen.At(now)`, an `Expression<Func<TeamPoll, bool>>` shared by:
  - the list (open/closed split);
  - the answer, withdraw and edit guards;
  - the cap count;
  - Home's *Needs you*.
- The moment a poll closed is `ClosedAt ?? ClosesAt`. The closed list orders by it,
  descending; EF translates `??` to `COALESCE`.

**Rationale**: FR-020 needs a poll to close by itself at its time. Deriving it from the
timestamp makes that exact to the request, with no job, no race between a job and an answer,
and nothing to fail. This is the same move as 058's shared `JoinRequestWaiting.Predicate`: one
meaning of "open", so the Home item, the card and the guards cannot disagree.

**Alternatives rejected**: a background service that stamps `ClosedAt` when `ClosesAt` passes
would add a moving part and a window during which the poll is "past its time but still open".

---

## R5 — Whose answers count

**Decision**: An answer counts, and is shown, iff its voter currently holds a `TeamMembership`
on the poll's team and is not `Banned`. The spec's other case, deleted accounts, has no
memberships left after 037's erasure.

Each read loads:
1. the team's current non-banned members with their names — one query, through `PlayerProfiles`,
   whose ban filter already hides banned players;
2. all vote rows of the polls on the page — one query.

It then computes in memory:
- per-option counts;
- the number who answered;
- the voter lists;
- the not-answered list;
- the member count used as the denominator.

Nothing is written when membership changes (spec Assumptions). A member who leaves stops
counting at once. A member who rejoins counts again, with the answer they gave (spec Edge
Cases).

**Rationale**:
- One definition read in one place.
- Every way of leaving is covered: leave, removal, ban, erasure and team deletion. Each has its
  own code path, and a delete-on-leave design would need a line in each of them.
- A team roster is small: the product has no member cap, but every real team is tens of people.

**The names come through `_db.PlayerProfiles.Where(p => p.UserId == …)`**, never
`vote.User.Profile!`. The ban `HasQueryFilter` makes that navigation misbehave (044's lesson,
repeated in `HomeService`'s join-request query).

---

## R6 — Anonymity is enforced where the DTO is built

**Decision**: One builder turns the in-memory aggregates (R5) into a `TeamPollDto` for one
viewer. Voter identities enter the DTO in exactly two places, each behind one condition:

- `option.Voters` is set only when `!poll.IsAnonymous && resultsVisible`.
- `notAnswered` is set only when `!poll.IsAnonymous && viewer is a current admin`.

For an anonymous poll, the vote rows' `UserId`s are used only to compute counts and the
viewer's own `MyOptionIds`, and never leave the service.

`resultsVisible = !poll.ResultsAfterAnswer || !isOpen || viewerHasAnswered` (FR-012a). When it
is false, every `option.Count` is `null` and `option.Voters` is `null`. `AnsweredCount` stays,
since the spec keeps it visible.

**Tests prove it by inspecting the raw JSON**, not the typed DTO, for the author, a second admin
and a member of an anonymous poll with answers (SC-003). The serialised response must contain
none of the other voters' handles, display names or user ids. The same technique covers a
hidden-results poll for a member who has not answered (SC-003a): no `count` values and no
`voters` in the body.

**`HasAnswers`** (any stored vote, current members or not) is exposed so the admin UI can lock
the content fields. It is the server's own rule (FR-023) and reveals nothing about any person.

---

## R7 — One list endpoint, paged, split by state

**Decision**: `GET /teams/{slug}/polls?state=open|closed&skip&take` →
`PagedResult<TeamPollDto>`.

- Open polls are ordered newest first and number at most 10 because of the cap (FR-005), so the
  card asks with `take=10` and always gets them all.
- Closed polls are ordered most-recently-closed first and paged. The card asks for 5 and offers
  "Show older polls".

Every mutating endpoint returns the updated `TeamPollDto` for the caller (201 on create, 200
otherwise), except delete (204).

**Rationale**: Constitution III ("list endpoints paginate") is satisfied without a deviation.
The open list's bound is the cap, not a take. Two calls on the card's first load, open and
closed, is fine: the card is shown to members only, so non-members make no call at all
(SC-009).

**Deviation, recorded in the plan**: each poll embeds its options (≤10), each named option
embeds its voters, and the admin view embeds the not-answered list. Both lists are bounded by
the team's current membership, not by a `take`. This is the 061 `Links` / 044 `Roster`
precedent: a page of polls that made N more calls for names would be worse on every axis.

---

## R8 — Validation, error codes and the request shapes

**Decision**: `TeamPollRules` is a pure static class with the limits as public constants:

- question 1–200 characters;
- 2–10 options of 1–80 characters, with no duplicates ignoring case and surrounding spaces;
- a close date later than now and no more than 365 days ahead.

It is unit-tested without a database, as `TeamDetailsPolicy` was in 061. The service returns a
status and a code. The controller maps them to ProblemDetails with `extensions.code` (camelCase)
and, where it applies, `extensions.option` (the 0-based index of the offending option, 061's
`link` precedent).

| HTTP | `code` | When |
|---|---|---|
| 400 | `question` | question empty or > 200 after trimming |
| 400 | `optionCount` | fewer than 2 or more than 10 options |
| 400 | `optionLength` (+`option`) | an option empty or > 80 after trimming |
| 400 | `optionDuplicate` (+`option`) | the second of two equal options |
| 400 | `closesAtPast` | close date not later than now |
| 400 | `closesAtTooFar` | close date more than 365 days ahead |
| 400 | `choiceCount` | one-answer poll given ≠ 1 option, or multi given 0 |
| 400 | `choiceUnknown` | an option id not in this poll |
| 409 | `tooManyOpen` | the team already has 10 open polls |
| 409 | `closed` | answering, withdrawing, editing or closing a poll that is closed |
| 409 | `answered` | changing question/options/one-many/results-timing after the first answer |
| 403 | — | not an admin (create, edit, close, delete) |
| 404 | — | team not found / not a member ("Team not found", reused), or poll not in this team ("Poll not found") |

**The client renders the code, never `detail`** (GH #179). `detail` stays English for API
readers.

**Binding guards are looser than the rules** (061's walk lesson: MVC's implicit `[Required]` on
a non-nullable string refuses whitespace with an uncoded 400). Request strings are nullable. The
guards only cap the payload size, for example `[MaxLength(20)]` on the options list and
`[StringLength(1000)]` on each string, so that every user-facing refusal carries a code.

**`closesAt` is bound as `DateTimeOffset?`** and stored as `.UtcDateTime`. The client sends an
ISO instant it converted from the `datetime-local` value in the viewer's zone (FR-004). A bare
local string never reaches Npgsql's `timestamptz`, which throws on `DateTimeKind.Unspecified`.
This differs from events, whose times are wall-clock at the venue; a poll's close is an instant.

**Anonymity cannot be edited because the edit request has no field for it** (FR-016 made
structural). The edit request is the create request minus `isAnonymous`.

**Unchanged content is not a change.** If the edit's question, options (text, in order), one-many
and results timing equal the stored ones, the option rows are not replaced and `answered` is not
raised. Moving only the close date after answers therefore works (FR-022), and option ids stay
stable.

---

## R9 — The notice: a new type on the existing engine

**Decision**: `NotificationType.TeamPoll = 11`, **appended**. The values are stored as integers.

**Category and payload**:
- `NotificationCategories.For` gets an explicit `TeamPoll => TeamNews` arm. The default arm
  already returns `TeamNews`, but the 039 note says every type needs its own case and a test.
- The payload is `TeamPollPayload(TeamSlug, TeamName, PollId, Question)`. The question is at most
  200 characters, so it is copied whole with no excerpt. The actor is the author. The payload
  names no person: an erased author's alerts must not identify them (037 FR-023, 058's rule).
- The dedupe prefix is `poll:{pollId}`, spelled once as `PollDedupePrefix`.

**Sending**:
- The notice is created by `CreateManyAsync` after the poll's transaction commits. It is
  best-effort, inside a `try/catch` that logs without text, like `TeamNewsService.PostAsync`.
- The engine already evaluates in-app and push independently per the recipient's *Team news*
  preferences (055), and sends the realtime "created" event.
- Recipients are every current membership except the author.

**Later changes to the poll**:
- An edit that changes the question calls `ReplacePayloadAsync(TeamPoll, prefix, payload)` inside
  the edit transaction. It is silent: no unread flag, no reorder, no realtime event (057,
  FR-030).
- Delete calls `DeleteManyAsync` inside the delete transaction and `RefreshUnreadBadgesAsync`
  after the commit (057, FR-024).
- **Rename is covered for free.** 061's `ReplaceTeamNameAsync` finds rows by
  `Payload->>'teamSlug'`, never by type, so a payload that carries `teamSlug` is rewritten on
  rename. `TeamRenameRewriteTests` grows from nine kinds to ten (FR-026).

**Push (`PushContentComposer`)** needs three arms, and the exhaustive composer guard added in 058
(`PushComposerTests`) fails until all three exist:
- title: `teamName`;
- body: a fixed `teamPoll.body` sentence ×3 locales with no question (FR-028, clarified);
- url: `/t/{slug}#poll-{pollId}`, built from the validated slug and a parsed Guid, never from
  free text (the `UrlFor` rule).

**`NotificationDto.Resolved`** stays `false` for polls. The spec asks nothing of the Alerts row
once a poll closes, and adding it would cost a query per page for no requirement.

---

## R10 — Email in the recipient's language

**Decision**:
- A new `team-poll.html` template in `EmailTemplates/{en,de,es}/`.
- `EmailTemplateService.GenerateTeamPollEmailAsync(teamName, question, authorName, url, culture)`.
- `EmailLocalizer` keys `subject.teamPoll`, `title.teamPoll` and `footer.teamPoll` ×3.
- `TeamEmailService.SendTeamPollEmailAsync(…, culture)`.

The culture is resolved per recipient with `SupportedLanguages.ResolveOrDefault(user.PreferredLanguage)`,
which is 058's pattern (`TeamJoinRequestService` at the admin fan-out). The link is the team
page with the `poll-{id}` fragment.

**Rationale**: FR-029 requires the recipient's language. Team *news* email is English-only today
(`team-news.html` exists only in `en/`), so copying that path would ship a known defect.
`TemplateParityTests` already fails when a template is missing from any locale, so the three
files are guarded for free.

**Values are user text**: team name, question and author name go through the template engine's
HTML-escaping, like every existing variable. Only the URL is `RawHtml`.

---

## R11 — Home's *Needs you*

**Decision**:
- `NeedsYouKind.TeamPoll` is **appended**; it is serialised by name.
- `NeedsYouParamsDto` gains `Question`. `TeamName` and `TeamSlug` already exist.
- `HomeService.LoadNeedsYouAsync` gains one query:
  `myTeamIds.Contains(p.TeamId) && p.AuthorUserId != userId && TeamPollOpen.At(now) && !p.Votes.Any(v => v.UserId == userId)`,
  ordered newest first, `Take(cap)`.
- The item's `Id` is the poll id and its `LinkTarget` is the team slug. The client builds
  `['/t', slug]` with fragment `poll-{id}`.

The item carries names and no sentence (058). Its headline is
`home.needsYouItem.teamPollTitle` ("{{team}} asks"). Its second line is the question and the
time. Its one action is an **Answer** link to the poll, not accept/decline buttons: a poll is not
answered from Home (spec FR-031, FR-036).

It lists only the viewer's own unanswered polls, so it cannot disclose anyone else's
participation (FR-018).

---

## R12 — The settings copy

**Decision**: `NotificationPreferenceService.CategoryCopy[TeamNews]` gets a new description in
all three languages. The copy lives server-side; the labels are server-owned since 031.

| Language | Current | New |
|---|---|---|
| en | *"News posted to your teams"* | *"News and polls posted to your teams"* |
| de | *"Neuigkeiten, die in deinen Teams gepostet werden"* | *"Neuigkeiten und Umfragen aus deinen Teams"* |
| es | *"Novedades publicadas en tus equipos"* | *"Novedades y encuestas publicadas en tus equipos"* |

The label ("Team news") is unchanged. No category is added (clarified).

---

## R13 — Account deletion and the published texts

**Decision**:
1. `AccountDeletionService.EraseOwnedDataAsync` gains
   `await _db.TeamPollVotes.Where(v => v.UserId == userId).ExecuteDeleteAsync(ct);` in its
   *Participation* block (FR-039). The voter FK is `Restrict`, but that forces nothing: erasure
   neutralises the `User` row and never deletes it, so a forgotten line would fail silently and
   leave a named answer behind. **The test that the votes are gone is the only guard**, so it is
   a task of its own.
2. The poll an erased admin started stays. `TeamPoll.AuthorUserId` is `Restrict` like
   `TeamNewsPost`, and the author's name projects to `null` once the profile is gone. The client
   renders the former-player placeholder.
3. `RetainedCategories` gains `"Polls"`, and `account.delete.retained.Polls` is added ×3 ("Polls
   you started stay with the team, with no author"). This is what 037 FR-025 promises the member
   before they delete.
4. **Legal texts (FR-040).** Two sentences enumerate what outlasts a deletion, both naming
   "messages … and your news posts":
   - `legal/{en,de,es}.json` terms `endingIt` (en line 84);
   - privacy `rights` (en line 203).

   Both are rewritten to the **category**, following the standing rule that legal text describes
   categories of data and never individual features. For example: *"Messages you wrote and what
   you posted to a team or event stay where they are…"* German is authoritative and drafted
   first; en/es follow.
5. **Changing the Terms text bumps the Terms version.** The acceptance record must evidence what
   the person saw (041 R1), so the version moves to the ship date in:
   - `TermsOptions` (default + `ResolvedVersion` fallback);
   - `appsettings.json`;
   - `terms.version` + `terms.lastUpdated` in all three catalogues.

   `TermsVersionParityTests` fails the build if any of them disagrees. The privacy policy has
   no version, and its shared `meta.lastUpdated` is left alone, as 055/056 did.

**Alternatives rejected**:
- **Deleting an erased author's polls**: it would destroy every other member's answers.
- **Leaving the legal sentences as they are**: "Two things outlast that" would become an
  incomplete statement in a binding text and a transparency notice.

---

## R14 — Frontend architecture

**New files**:
- `core/models/poll.models.ts`;
- `core/services/poll.service.ts`, providedIn root and stateless;
- `features/teams/team-detail/polls/`:
  - `team-polls` — the card: loads open and closed, owns the create editor, lists the polls,
    handles "Show older polls" and the deep-link scroll;
  - `poll-item` — one poll: its question, meta and chips, the answer controls, results, admin
    menu and dialogs;
  - `poll-editor` — the create/edit form;
- `core/utils/poll-close-time.ts`, converting `datetime-local` ↔ UTC instant.

**Mounted** as `<jh-team-polls [slug] [isAdmin] />` inside the team page's existing
`@if (isMember())` block, directly **before News**. This is the team-internal section: News and
*What's happening* are already there, and a non-member's page is untouched (SC-009). The Needs
you item, the alert and the push all deep-link to the poll itself, so its position does not
decide whether a member finds it. **Alternative considered**: above the roster, just after
About. It is more prominent, but for teams that never use polls it would put an empty state high
on every member's page. Confirm in the browser walk.

**Zoneless (045's lesson)**: everything the templates read is a signal. This covers:
- the lists and the busy/error keys;
- the editor's question, option rows and choices;
- `saving`, `menuOpen` and `confirming`;
- the multi-choice selection before saving.

Focus moves via `afterNextRender`.

**Deep link**: the card reads `ActivatedRoute.fragment`. After its lists land, if the fragment is
`poll-{id}` and that poll is rendered, it scrolls it into view and focuses the poll's heading
(`tabindex="-1"`) in `afterNextRender`. The router's own `anchorScrolling` runs at navigation
end, before the polls have loaded, so it cannot do this. A link to a poll that is not on the
loaded pages, such as an old closed one, lands on the card heading. The notification row gains a
`fragment()` beside `link()`. The anchor must be `[routerLink]` + `[fragment]`, never a string
with `#`: `routerLink` would encode the `#`, and 036's `<base href>` lesson applies to bare
`href="#…"`.

**Errors**: every refusal maps from `code` (or status) to a translation key, never to `detail`.
`409 closed` reloads that poll's list and shows "This poll has closed". A 404 on the poll removes
it and shows a card-level notice, the 057 pattern.

**No automatic retry**: the retry interceptor already retries only GET/HEAD. Mutations retry by
the member pressing again (FR-038).

---

## R15 — Interaction and visual decisions (Gate 7)

**Answering**:
- A **one-answer** poll records on the press of an option, one press (SC-002). Pressing your
  current option does nothing. Withdrawing is a separate small "Withdraw my answer" text button.
- A **multi-answer** poll toggles options locally and saves with one **Save answer** press
  (FR-036: one press per option + one confirm). Clearing every choice and saving is a withdrawal
  (edge case). The client sends DELETE rather than an empty PUT.
- Options are full-width `<button>`s at least 44px tall, `aria-pressed`, in a `role="group"` named
  by the question. They are not radio inputs, because a radio group suggests a form submit. The
  member's own choice shows a `check` icon plus visually hidden "your answer" text: never colour
  alone.

**Results**:
- When visible, each option carries a thin fill bar behind the label. The fill is
  `brand-primary`, which DESIGN.md assigns to progress fills, at a soft tint. The count sits at
  the end, with digits in `font-mono` (061's lesson: mono only for numbers).
- Named voters are a caption line of names under the option, each a link to `/u/{handle}`.
- The admin's not-answered line sits under the options.
- When results are hidden, the options show no bars and no counts, and a caption says the result
  appears once you answer.

**Meta and chips**:
- *Names shown* / *Anonymous*, *Results after you answer*, *Several answers*, *Closed*. These use
  the `jhChip` tones, with `muted` as the default.
- The anonymous chip is accompanied by the sentence "Nobody sees who chose what". The wording is
  about **seeing**, never about "can't be worked out" (FR-019).

**Editor**:
- It is inline at the top of the card, opened by a **secondary** "Start a poll" button in the
  card header. It is not a modal: 057 kept editors inline because the iOS keyboard sits over a
  fixed sheet.
- Its submit is **secondary**, because the page's one coral CTA is the news composer's
  *Post news* (057's precedent for the inline editor's Save).
- The three binary choices are `fieldset`s of two native radio inputs, each with a label and a
  one-line explanation. Radios are chosen over switches because each choice is between two named
  outcomes, not on/off, and anonymity needs its consequence spelled out before it is fixed.
  DESIGN.md has no radio spec; the checklist records that gap rather than inventing a style.
- The close date is an optional `datetime-local` with a Clear button.

**Admin menu**:
- It uses the `jh-news-post` menu idiom: `ellipsis`, `role="menu"`, Escape and outside-click
  close it, and the card is `overflowVisible` so the menu is not clipped.
- Items: *Edit* (content disabled with an explanation once answered, close date always), *Close
  now*, *Delete*.
- Close and Delete confirm in the fixed dialog idiom of `jh-news-post`, with the safe answer
  focused. Delete is `danger`.

**Binding case for Gate 7**: German at 375px:
- a poll with ten 80-character options, results visible, named;
- the admin's not-answered line;
- the editor with ten option rows;
- the anonymous notice;
- the close and delete dialogs as bottom sheets.

---

## R16 — Principle VII / Gate 8

**Not engaged as an integration**: no outbound call is added. Email and push reuse their
existing senders and dispatcher, which already carry resilience. What VII does require is
designed in:
- every multi-step write is one execution-strategy unit with every mutation inside it and
  entities created inside it (R3);
- side effects (notices, email, badge refresh) happen after the commit and are best-effort;
- browser mutations are never auto-retried;
- no log line carries a question, an option or a name.

---

## R17 — Deliberately not done

- **Live tallies**: a page already open does not update as others answer (spec Assumptions;
  like news and join requests).
- **Reminders, close notices, "someone answered" notices**: out of scope.
- **`Resolved` on poll alerts**: see R9.
- **An index on `Payload->>'teamSlug'`**: the rename scan was already accepted in 061. One more
  kind does not change it.
- **A poll in *What's happening* or Home's news module**: FR-042.
- **Retention sweep**: closed polls stay until deleted (clarified).
