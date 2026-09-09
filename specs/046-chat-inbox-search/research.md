# Research: Chat Inbox Search by People and Conversation Names

Phase 0 for [plan.md](./plan.md). Every open technical question from the Technical Context is
resolved here as a decision with its rationale and the alternatives that were rejected. The
product decisions themselves were taken by the owner before the spec was written (see the
spec's Clarifications) and are not re-litigated.

> **The load-bearing idea**: the search is **the inbox query with one more `WHERE`**. It reuses
> the inbox's visibility predicate, projection, order and paging verbatim, so FR-003 (same
> eligibility), FR-005 (same row), FR-006 (same order and bound) and SC-007 (no term ⇒ identical
> inbox) hold by construction rather than by two code paths agreeing. Feature 019 learned this
> lesson the hard way — its plan records a membership predicate that was duplicated and drifted
> ("listed in the inbox but 404'd when opened") until it was shared. A separate search query
> would be the third copy.

## 1. Where the conversation search lives — a `q` on the inbox endpoint

**Decision**: `GET /api/v1/chat/conversations` gains an optional `q` query parameter.
`IChatConversationService.GetInboxAsync` takes the term; when the trimmed term has at least
`ChatConstants.MinSearchTermLength` (2) characters, the service applies one additional
`.Where(...)` to `VisibleConversations(callerId)` **before** the existing count / order / skip /
take / projection. Nothing else in the method changes. A shorter or absent term is treated as
"no term" and returns the plain inbox.

**Rationale**:
- The spec requires results to *be* inbox rows (FR-005), in the inbox's order and bound (FR-006),
  over exactly the inbox's eligible set (FR-003), and requires the no-term inbox to be unchanged
  (SC-007). One query with an optional predicate makes all four structural.
- `VisibleConversations` already composes `ChatGuard.IsMemberOf` (membership), the hide
  exclusion, and the block exclusion for DMs — precisely the eligibility the owner chose
  (hidden stays hidden; blocked DMs stay out). Reusing it means the search cannot leak a
  conversation the inbox would not show, and no count can hint at one (SC-003).
- No new endpoint, no new DTO, no new client model: `PagedResult<ConversationSummaryDto>` is what
  the inbox already renders.

**Alternatives considered**:
- *Keep `/chat/search` and make it return `{ conversations, people }`.* Either duplicates the
  inbox projection (the drift hazard above) or returns a different row shape the inbox would
  have to render separately. It also burdens the three other callers of `/chat/search` — the
  new-chat picker, compose-by-handle and the profile Message action — with conversation results
  they never use.
- *Filter the loaded inbox on the client.* Fails FR-004 / SC-004 outright: the client loads only
  the first 20 conversations and has no load-more, so anything beyond page one would be
  unfindable. It could also only match what the summary row carries — a group's members are not
  in it.

## 2. The name predicate — the same branches as `IsMemberOf`, with a name test

**Decision**: one expression builder next to `ChatGuard.IsMemberOf` (same file, same shape, so a
future conversation kind is added to both in one place) — `ChatGuard.MatchesName(db, callerId, pattern)`,
returning `Expression<Func<Conversation, bool>>`. It is **one** expression with two halves, ORed
inline, because EF Core cannot compose separately built lambdas without a helper library. The
halves, described separately for clarity:

- *Members* — true when a **current** member other than the caller matches:
  - Archived (any kind), Direct, Group → `c.Participants` with `LeftDate == null`;
  - Team → `db.TeamMemberships` on `c.TeamId`;
  - Party → `db.PartyMembers` on `c.PartyId` with `Status == In`;
  - TeamInquiry → the requester (`c.RequesterUserId`) **or** a current team admin;
  - EventInquiry → the requester **or** a current `EventAdmins` row.
  Each branch tests the member through
  `db.PlayerProfiles.Any(pp => pp.UserId == <member> && (ILike(Unaccent(pp.DisplayName), Unaccent(pattern)) || ILike(pp.Handle, pattern)))`.
- *Names* — true when the conversation's stored or derived name matches:
  `c.Name` (groups, and the frozen name of an archived chat), `c.Team.Name` (Team and
  TeamInquiry), `c.Event.Name` (EventInquiry). EF Core translates an `ILIKE` against a `NULL`
  column to `NULL`, which is falsy, so the null navigations need no guards.

The inbox predicate is `members || names`, and `pattern` is `%{term}%`. The data model's
pseudo-code names the halves `HasMemberNamed` and `IsNamed` for readability; in code they are
the two sides of the one `MatchesName` expression.

**Rationale**:
- **Membership as of now** (edge case "former members"): each branch reads the same source of
  truth `IsMemberOf` reads, so a player who left a group or was removed from a roster stops
  matching at the same instant they stop being a member. No sync, no second rule.
- **`db.PlayerProfiles.Any(...)` rather than `p.User.Profile!`**: `PlayerProfiles` carries the
  ban `HasQueryFilter`, and features 044/HomeService established that navigating through
  `User.Profile` misbehaves against it. Going through the DbSet applies the filter cleanly, and
  gives the "unavailable members are not matchable" edge case for free — a banned account's
  profile row is filtered out and an erased account's row is gone, so neither can match.
- **The caller is excluded** from member matching. The player is a member of every one of their
  conversations; matching their own name would list the whole inbox for one common term. The
  spec's "the names of the people in your conversations" is read as *the other people*.
- **Handles match too** (spec assumption): the existing people search matches display name or
  handle, and `@handle` is how players refer to each other on profiles. Kept for consistency;
  one line to remove if the owner flips the assumption.
- **Accent- and case-insensitive** via `EF.Functions.ILike` + `AppDbContext.Unaccent`, the
  convention every search in this codebase follows since feature 007.

**Deliberate exception (recorded as minor spec drift on FR-002)**: the *fallback* labels the
inbox prints when a conversation has no name of its own — "Party chat", "Group", "Team chat",
"Chat" — are **not** matched. They are English literals produced by `DisplayName(...)`, not
names anybody chose, and matching them would list every party chat for the term "party". A live
party chat is found through its members, as the spec's edge case already says.

**Alternatives considered**:
- *A single `Participants`-based predicate for all kinds.* Wrong for Team/Party/inquiry chats:
  their participant rows are lazily created per-user state, not membership (see
  `ConversationParticipant`'s remarks). A team-mate who never opened the team chat would be
  invisible to search.
- *Materialising a searchable "member names" column.* A write path and a sync problem for a
  read that is cheap: a player's conversations number in the dozens and rosters are ≤ ~30, so the
  correlated `Any` per conversation is negligible.

## 3. Removing message-text search — from the API, not just the interface

**Decision**: delete `ChatSearchService.SearchMessagesAsync`, `MessageSearchHitDto`, and the
`Messages` half of `ChatSearchResultDto`, which becomes `ChatSearchResultDto(PagedResult<PersonHitDto> People)`.
`GET /api/v1/chat/search` keeps its path and its `people` envelope so its three remaining callers
are untouched. The frontend drops `MessageSearchHit` and the `messages` member of
`ChatSearchResult`.

**Rationale**:
- FR-010 says removed, not hidden. A dormant endpoint half that reads message bodies is exactly
  the kind of unused capability the security-first principle exists to shrink.
- Verified by grep that the inbox is the **only** consumer of `messages`: `chat-new`,
  `chat-compose` and `profile-quick-actions` read `res.people.items` only.
- Keeping the `{ people }` wrapper rather than flattening to a bare `PagedResult` keeps those
  three callers byte-identical — the smallest blast radius for a breaking change.

**Consequences**:
- The `/chat/search` response shape changes (a removed property). Frontend and backend ship
  together, as features 020 and 042 established for breaking DTO changes.
- Four integration tests in `ChatSearchTests` test the removed half and are deleted; two others
  (`Search_results_are_paginated`, `Search_is_accent_insensitive`) exercise pagination and
  accent-folding *through* message hits and are rewritten against people hits, so the
  properties they guard survive. `A_short_or_empty_term_returns_an_empty_result_not_an_error`
  gains an assertion that the response carries **no** `messages` property — the FR-010 evidence.
- 019's FR-050c ("a deleted message never matches search") becomes moot and is annotated as
  such alongside FR-034–036.

## 4. Frontend — one list, two sources; the live signal is never overwritten

**Decision**: `ChatService.searchInbox(term, take = 20)` calls
`GET /chat/conversations?q=…&skip=0&take=20` and returns the page **without touching
`_conversations`**, the signal SignalR keeps current. `ChatInboxComponent` holds the results in
its own signal and renders a single `@for` over `displayed = isSearching() ? results ?? [] : conversations()`.
The "In your messages" and "People" sections, `chatWith()` and the `ChatSearchResult` import go.
Debounce (250 ms) and the two-character minimum stay as they are.

**Rationale**:
- Clearing the term must restore the full inbox instantly and unchanged (FR-007, SC-007): if
  results overwrote `_conversations`, clearing would need a reload and a realtime patch
  arriving mid-search would be applied to the wrong list.
- A message arriving while results are showing must not reorder or clear them (edge case):
  realtime updates keep patching `_conversations`; the results signal is untouched until the
  next search.
- One `@for` over one row template is what makes FR-005 ("the conversation's inbox row") true
  in the markup, not just in the DTO.

**Copy (FR-009), all three catalogues at once** — `catalog-parity.spec.ts` fails the build if
they diverge:

| Key | en | de | es |
|-----|----|----|----|
| `chat.inbox.searchSr` | Search your chats by name | Deine Chats nach Namen durchsuchen | Buscar en tus chats por nombre |
| `chat.inbox.searchPlaceholder` | Find a chat by name… | Chat nach Name finden… | Buscar un chat por nombre… |
| `chat.inbox.nothingMatched` | *(unchanged)* | *(unchanged)* | *(unchanged)* |
| `chat.inbox.inYourMessages`, `chat.inbox.people`, `chat.inbox.chat` | **removed** | **removed** | **removed** |

DESIGN.md voice: sentence case, "you", verbs over nouns — "Find a chat by name…" follows
"Find a team near you". The removed keys are used nowhere else (verified by grep).

**Loading line**: the current "Searching…" line is a bare `<p>`; DESIGN.md's loading rule is one
muted text line carrying `role="status"`. The rewrite adopts the rule (and the UI review
checklist, Gate 7, verifies it). No spinner, no skeleton, no layout shift.

**Alternatives considered**:
- *Reuse `loadInbox` with an optional term and let it set `_conversations`.* Rejected for the
  overwrite reasons above.
- *A separate results component.* Would duplicate the row markup — the same drift hazard as §1,
  in the template.

## 5. Amending feature 019's documents

**Decision**: following feature 022's precedent, `specs/019-chat/spec.md` gains a second
callout under its `## Amendments` heading ("Amended by feature 046 — inbox search finds
conversations by name; message-text search removed"), and FR-034, FR-035, FR-036, FR-050c and
SC-006 get an inline "*(superseded by 046)*" marker. `specs/019-chat/contracts/chat-api.md`'s
Search section gets the same one-line pointer. No other 019 document is edited.

**Rationale**: FR-013 — the source of truth must stay accurate, and 019 is what a reader of
the chat code reaches for first. Minimal, additive edits keep 019's history legible.

## 6. Performance, limits and Principle VII

**Decision**: no new rate-limit policy, no index, no resilience wrapping.

**Rationale**:
- The predicate runs inside a query already bounded by the caller's membership; per
  conversation it evaluates an `Any` over a handful of participant or roster rows joined to
  `PlayerProfiles`. At jugger-club scale (dozens of conversations, rosters ≤ ~30, groups capped
  at 50) this is well inside the inbox's existing budget.
- `%term%` `ILIKE` cannot use a B-tree index regardless, exactly like the existing people and
  team searches; adding a trigram index for name columns would be a new extension and a
  migration for no measurable gain at this scale.
- The client debounces at 250 ms and the inbox endpoint carries no rate-limit policy today; the
  existing `/chat/search` doesn't either. Unchanged.
- **Principle VII is not engaged**: this feature adds no network call — the search is a local
  SQL predicate. Wrapping it in retry or a breaker would be review-rejectable.

## 7. Test strategy

**Decision**:
- **Backend** — a new `Chat/ChatInboxSearchTests.cs` (Testcontainers Postgres, real API) covering
  SC-001 (member match across Direct, Group and Team; Party where the shared seeding helper can
  produce one), SC-002 (message text never matches), SC-003 (a name only in someone else's
  conversation returns nothing and no count), SC-004 (25 conversations, the 25th found by
  member name), SC-007 (no `q` ⇒ identical list), conversation-name matches (group name, team
  name), accent/case folding, a one-character term returning the plain inbox, hidden stays
  hidden, blocked DM stays out, a former group member no longer matches, and the caller's own
  name not matching everything. `ChatSearchTests` trimmed and rewritten as in §3.
- **Frontend** — `chat.service.spec.ts`: `searchInbox` issues the right request and leaves
  `conversations()` untouched; `search` no longer expects `messages`. `chat-inbox.component.spec.ts`:
  results render as conversation rows, the empty state names the term, clearing restores the
  inbox, message sections are gone.
- **e2e** — none added. There is no chat e2e suite today (the only e2e file mentioning chat is
  `resilience.spec.ts`), and the behaviour is fully observable at the API and component seams.

## 8. UI review (Gate 7)

**Decision**: instantiate `checklists/ui-review.md` from
`.specify/templates/ui-review-checklist-template.md` and verify it against the diff before
verification. This feature ships markup and copy changes (the results region is rewritten, the
search field's hint and label change, a loading line changes), so unlike feature 045 the gate
is engaged.
