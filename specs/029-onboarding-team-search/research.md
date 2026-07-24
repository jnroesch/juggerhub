# Phase 0 Research: Onboarding Team Search

**Feature**: `specs/029-onboarding-team-search/` | **Date**: 2026-07-24

No `NEEDS CLARIFICATION` markers survived into the spec — the two product forks were settled with
the owner before drafting, and the third surfaced during planning. What follows records the
decisions, the verified facts behind them, and the alternatives that lost.

---

## 1. What already exists (verified against source, not assumed)

| Capability | Where | Verified fact |
|---|---|---|
| Team search | `SearchService.browseTeams()` → `GET /api/v1/teams` | Takes `q`, `city`, `activeOnly`, `beginnersWelcome`, `sort`, `skip`, `take`. Returns `PagedResult<TeamCard>` with `slug`, `name`, `city`, `playerCount`, `beginnersWelcome`, `logoInitial`. |
| Join request | `TeamService.requestToJoin(slug)` → `POST /api/v1/teams/{slug}/join-requests` | Returns `204 No Content`. **Idempotent while a request is pending** and `409 Conflict` when the caller is already a member — confirmed by `backend/tests/…/Teams/JoinRequestTests.cs:53-78`. |
| List state machine | `features/browse/browse-list.ts` | Owns items, total, loading, error, and the five-way `BrowseState`. `filtered` decides `no-results` vs `empty`. |
| Row treatment | `features/browse/browse-teams/browse-teams.component.html:20-41` | Initial chip, name, `city ·` mono `playerCount` players, "Beginners" pill. |
| Loading line | `shared/ui/loading` (`jh-loading`) | One muted line, `role="status"`, self-timing patient copy after 2 s (feature 028). |
| Retry/timeout | `core/interceptors/retry.interceptor.ts` | 15 s per-attempt limit on **everything**; up to 2 jittered retries on `GET`/`HEAD` only. |

**Consequence**: this feature writes no resilience code, no service method, and no HTTP call of its
own. Everything is wiring.

---

## 2. Decision — opening list is beginners-welcome, but says so and offers more

**Decision**: On entering the step, fetch `beginnersWelcome: true, activeOnly: true, sort: 'NameAsc'`
and render the result under copy that explicitly invites searching for any other team. The moment a
query is entered, drop `beginnersWelcome` entirely and search all teams.

**Rationale**: The person on this screen registered less than a minute ago. Showing them teams that
have declared themselves open to new players is the single most useful default. But a filtered
default silently becomes a cage if the reader assumes it is the whole list — so the copy carries the
weight, not the filter. The owner's instruction was explicit on this point: preload
beginners-welcome, *and* make sure the reader is clearly guided to searching for other teams.

**Alternatives rejected**:
- *Blank until they type* — cheapest and calmest, and it is what a pure search field would do. Loses
  the one moment where a brand-new player benefits most from discovery.
- *Unfiltered first page (A–Z)* — never wrong, but "teams starting with A" is a worse first
  impression than "teams that want you".

**Risk accepted**: one search request fires for every player who reaches the step, including those
who skip. At onboarding volume this is nothing, and it is a `GET` that already inherits the retry
interceptor's protection.

---

## 3. Decision — asking to join is its own press; Continue never sends anything

**Decision**: Selecting a row reveals a secondary "Ask to join *{team}*" action. Only that action
sends the request. Continue, "I'm not on a team yet", Skip, and Back stay pure navigation.

**Rationale**: Two spec requirements collide unless the send is separated from the navigation.
FR-013 says the pending-request confirmation must be shown *on this step*, and FR-017/FR-018 say
nothing may block the player. If Continue both sent the request and advanced, the confirmation would
flash past unread; if Continue sent it and *waited* to confirm, Continue would acquire a network
call, an in-flight state, and a failure mode — precisely the thing that must never happen on the
first screen after registration.

Separating them makes the guarantee **structural instead of careful**: `next()` keeps its current
two-line body and cannot regress into something that can fail. It also means an exploratory tap on a
row costs nothing, and the confirmation is the direct consequence of a press the player chose to
make, so it is unambiguously seen.

**Alternatives rejected**:
- *Continue sends, then confirms in place, second Continue advances* — matches issue #74's wording
  literally, but the first press appears to do nothing except make a line of text appear, and it
  puts a network call behind the wizard's primary action.
- *Send on row tap* — fewest presses, but an exploratory tap becomes a real join request that a team
  admin has to decline. Making a stranger do cleanup for a mis-tap is not acceptable.

**Deviation recorded**: issue #74 says "picking a team and continuing sends a join request". The
outcome is identical — a pending request created from the step — but the trigger is an explicit
action rather than Continue. Decided with the owner during planning.

---

## 4. Decision — reuse `BrowseList`, not `BrowseShellComponent`

**Decision**: Instantiate `BrowseList<TeamCard>` inside `OnboardingComponent` for fetching and state.
Copy the row markup from `browse-teams.component.html`. Do **not** use `BrowseShellComponent`.

**Rationale**: `BrowseList` is the part worth reusing — it is framework-light, already owns the exact
five states this step needs, and is the same code path the browse page runs, so "results here match
results there" is true by construction rather than by care.

`BrowseShellComponent` is the opposite: it renders a page title, the Teams/Events/Players tab nav, a
Filters button and panel, active-filter chips, a sort control, a count line, and a "Load more"
button. In a `max-w-sm` onboarding step every one of those is wrong, and using it would mean passing
inputs whose only purpose is to suppress what it renders. The row markup it wraps is twelve lines;
copying twelve lines is more honest than bending a page-level shell around a wizard step.

**Alternatives rejected**:
- *Extract a shared `TeamSearchList` component* — a shared surface with exactly one consumer is an
  abstraction invented for its own sake. If a second consumer appears, extraction is easy and the
  duplication will have shown what the shared shape actually is.
- *Refactor `BrowseShellComponent` to make its chrome optional* — changes a component three shipped
  screens depend on, to serve one step. Wrong blast radius.

---

## 5. Decision — `filtered` drives empty-vs-no-results; the two never share wording

**Decision**: Set `BrowseList.filtered` to `Boolean(query)` on every reload. With a query, an empty
result renders "no teams match that" and invites another search. With no query, an empty result means
the beginners-welcome list is genuinely empty, which is a "nothing here yet" state, not a failure.
`error` is a third, visibly different state carrying a **secondary** "Try again" button.

**Rationale**: DESIGN.md is unambiguous — *"Showing an empty state for a failed load quietly lies to
the reader."* `BrowseList` already models the distinction, so the work is setting one flag correctly.
The retry button is `variant="secondary"` because DESIGN.md allows exactly one coral CTA per view and
the step's Continue already is it.

---

## 6. Decision — inherit resilience, add none

**Decision**: Write no retry, no timeout, no backoff.

**Rationale**: Constitution VII, verified in `retry.interceptor.ts`:
- the search is a `GET` → 15 s per-attempt limit, up to 2 retries with full jitter, transient
  statuses only;
- the join request is a `POST` → time-limited, **never** retried.

The second is the never-retry-a-browser-mutation rule, and it holds here *even though* the endpoint
is idempotent while a request is pending. The rule is about what the browser can know, and the
browser cannot know that. A slow search needs no special handling either: `jh-loading` switches to
its patient line by itself after 2 s.

Anything hand-rolled on top would be review-rejectable under VII's "applied by default, never per
call site".

---

## 7. Failure-copy mapping

| Outcome | Reader sees | Action offered |
|---|---|---|
| Search fails | "We couldn't load teams just now." | "Try again" (secondary) |
| Search returns nothing, query present | "No teams match that — try a different name." | none (invites retyping) |
| Opening list genuinely empty | "No teams are looking for new players right now — try searching by name." | none |
| Join request `409` | "You're already on that team." | none |
| Join request fails otherwise | "We couldn't send that request just now." | none — the ask button remains pressable |

No status code, stack trace, or internal detail reaches the reader (constitution I, FR-009).

---

## 8. Testing approach

Jest, zoneless (no `fakeAsync` — see the 014 catalogue precedent).

- Drive the component's protected surface through the existing `OnboardingApi` test interface,
  extended with the new members.
- `HttpTestingController` asserts the exact requests: opening fetch carries `beginnersWelcome=true`;
  a query fetch carries `q` and **no** `beginnersWelcome`.
- Debounce is exercised with `jest.useFakeTimers()` around the typing entry point.
- The load-bearing negative tests: advancing the step issues **zero** requests in every state; a
  failed search leaves Continue enabled; a failed join request leaves the flow advanceable; a
  selected-but-never-asked team writes nothing; the finish payload is byte-identical to today's.
