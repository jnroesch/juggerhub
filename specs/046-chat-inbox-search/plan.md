# Implementation Plan: Chat Inbox Search by People and Conversation Names

**Branch**: `046-chat-inbox-search` | **Date**: 2026-09-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/046-chat-inbox-search/spec.md` — amends feature
019 (chat) User Story 6; GitHub #221.

## Summary

The chat inbox's search field stops searching message text and instead finds **conversations by
name**: the player's own conversations in which another current member's name matches, or
whose shown name matches. Results are the inbox's own rows, in the inbox's own order. Message-text
search is removed from the product — interface *and* API — because the inbox was its only
consumer. The people search that starts new chats (new-chat picker, compose-by-handle, profile
Message) is untouched and keeps its open reach.

Technical approach, in one breath: **the search is the inbox query with one more `WHERE`.**
`GET /chat/conversations` gains an optional `q`; `GetInboxAsync` appends a name predicate to the
existing `VisibleConversations` query — same membership expression as `ChatGuard.IsMemberOf`,
same hide and block exclusions, same projection, order and paging — so "same eligibility, same
row, same order, unchanged without a term" (FR-003/005/006, SC-007) hold by construction. The
predicate mirrors `IsMemberOf`'s per-kind branches with a name test, reads member names only
through `db.PlayerProfiles` (the ban filter makes unavailable members unmatchable for free), and
excludes the caller. `/chat/search` drops its `messages` half and keeps its `people` envelope, so
its three other callers are byte-identical. On the client, results live in their own signal and
the live `conversations` signal is never overwritten, so clearing the term restores the inbox
instantly. **No entity, no migration, no new dependency, no new endpoint.** Full reasoning in
[research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular 22 zoneless with signals
(frontend); Transloco 8 for i18n

**Primary Dependencies**: EF Core + Npgsql (`EF.Functions.ILike`, the mapped `unaccent`
function), ASP.NET Core; no new package on either side

**Storage**: PostgreSQL 18 — **no schema change, no migration.** Reads `Conversations`,
`ConversationParticipants`, `TeamMemberships`, `PartyMembers`, `EventAdmins`, `PlayerProfiles`,
`Teams`, `Events`, `UserBlocks` — all existing

**Testing**: xUnit integration tests against Testcontainers Postgres in
`backend/tests/JuggerHub.Api.IntegrationTests/Chat/` (new `ChatInboxSearchTests`, trimmed
`ChatSearchTests`); Jest for `chat.service` and `chat-inbox.component`; the i18n parity spec
guards the catalogues

**Target Platform**: Linux containers (backend + frontend) on AKS; local via docker-compose

**Project Type**: Web application — .NET monorepo backend + Nx/Angular frontend

**Performance Goals**: search results arrive within the inbox's own budget; the added predicate
is an `EXISTS` over a handful of roster/participant rows per conversation, bounded by the
caller's membership (research §6)

**Constraints**: FE and BE ship together (a removed response property on `/chat/search`); no
code path on the search route may read `ChatMessages`; hidden conversations and blocked DMs stay
excluded (owner decision); copy changes land in all three catalogues at once (parity guard)

**Scale/Scope**: jugger-club scale — dozens of conversations per player, rosters ≤ ~30, groups
capped at 50. ~6 backend files (2 services, 1 guard, 1 DTO file, 1 controller, tests),
~7 frontend files (model, service, component ts/html, 3 catalogues, 2 specs), 2 documentation
amendments to feature 019.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | Verdict | How this feature satisfies it |
|---|-----------|---------|-------------------------------|
| I | **Security-first, never trust the client** (NON-NEGOTIABLE) | ✅ Pass | Eligibility is decided server-side by the inbox's own `VisibleConversations` — the shared `ChatGuard.IsMemberOf` expression plus hide and block exclusions — with the name predicate *appended*, so a term that matches only a conversation the caller cannot see returns nothing and no `totalCount` hints at it (I1, SC-003). The term is a bound query parameter (EF parameterises the `ILIKE` pattern). Removing message-text search **shrinks** the surface that reads message bodies. Errors stay generic via the existing middleware; a too-short term is simply "no term", not an error path. |
| II | **Thin controllers, service-centric** | ✅ Pass | `ChatConversationsController.Inbox` gains one `[FromQuery] string? q` passthrough; the service applies it. Services keep returning DTOs from explicit `.Select` projections — the search reuses the inbox projection unchanged. No repository layer, no mapper. |
| III | **Disciplined data access** | ✅ Pass | The result is the inbox's `PagedResult<ConversationSummaryDto>` via the shared `PaginationRequest` — paginated, `AsNoTracking`, projected. No entity, no column, no index, **no migration**. |
| IV | **Secure auth & session** | ✅ Pass | No auth change; the endpoint keeps `[Authorize]` on the JWT-bearer scheme. |
| V | **Environment parity & containerized deploys** | ✅ Pass | No infrastructure, configuration or secret changes; the removed message search needs nothing torn down. |
| VI | **Consistent conventions & tooling** | ✅ Pass | Component keeps `.html` / `.css` / `.ts` separate. Matching uses the established `ILike` + `Unaccent` convention (feature 007) rather than a second mechanism. No scripts added. |
| VII | **Resilient by default, never amplifying** | ✅ Pass — **not engaged** | This feature adds **no network call**: the search is a local SQL predicate inside a query the inbox already runs. No timeout, retry or breaker belongs in this diff; wrapping a `SELECT` in resilience would be review-rejectable. |
| — | **Gate 7: UI/design compliance** | ✅ Pass | Markup and copy change (results region rewritten as the inbox list, search hint and label, loading line), so `checklists/ui-review.md` is instantiated from the template and verified against the diff before verification. DESIGN.md governs voice ("Find a chat by name…"), the `role="status"` loading line, and empty-vs-error distinction. |
| — | **Gate 8: Resilience** | ✅ N/A | No network call or outbound integration added. |

**Result: PASS.** No violations require justification, so Complexity Tracking is empty.

*Post-Phase-1 re-check: still PASS.* The design added no endpoint, no entity, no dependency and
no unbounded list. The one recorded **spec drift** (research §2): the inbox's *fallback* labels
("Party chat", "Group", "Team chat") are not matched by FR-002's "shown name" — they are
presentation defaults, not names anyone chose, and a live party chat is found through its
members as the spec's edge case already provides.

## Project Structure

### Documentation (this feature)

```text
specs/046-chat-inbox-search/
├── spec.md                          # Owner decisions + Clarifications session 2026-09-08
├── plan.md                          # This file
├── research.md                      # Phase 0 — decisions & rejected alternatives
├── data-model.md                    # Phase 1 — what is read per kind; invariants; deletions
├── quickstart.md                    # Phase 1 — how to run & validate
├── contracts/
│   └── chat-inbox-search-api.md     # Phase 1 — `q` on the inbox; `/chat/search` people-only
├── checklists/
│   ├── requirements.md              # Spec quality (16/16)
│   └── ui-review.md                 # Gate 7 — instantiated during implementation
└── tasks.md                         # Phase 2 — /speckit-tasks output
```

### Source Code (repository root)

```text
backend/
├── Services/Chat/
│   ├── ChatGuard.cs                     # + MatchesName(db, callerId, pattern) — one expression,
│   │                                    #   members || names, sibling of IsMemberOf, same per-kind branches
│   ├── IChatConversationService.cs      # GetInboxAsync(..., string? query)
│   ├── ChatConversationService.cs       # applies the predicate to VisibleConversations when q ≥ 2 chars
│   ├── IChatSearchService.cs            # doc: people only
│   └── ChatSearchService.cs             # − SearchMessagesAsync
├── Dtos/Chat/ChatDtos.cs                # − MessageSearchHitDto; ChatSearchResultDto(People)
├── Controllers/ChatConversationsController.cs   # Inbox(+q); Search unchanged in shape
└── tests/JuggerHub.Api.IntegrationTests/Chat/
    ├── ChatInboxSearchTests.cs          # NEW — SC-001…004, SC-007, edge cases
    └── ChatSearchTests.cs               # − 4 message tests; 2 rewritten against people; FR-010 assertion

frontend/apps/web/
├── public/i18n/{en,de,es}.json          # chat.inbox.searchSr / searchPlaceholder reworded;
│                                        # inYourMessages / people / chat removed (all three at once)
└── src/app/
    ├── core/
    │   ├── models/chat.models.ts        # − MessageSearchHit; ChatSearchResult = { people }
    │   ├── services/chat.service.ts     # + searchInbox(term, take) — never touches _conversations
    │   └── services/chat.service.spec.ts
    └── features/chat/chat-inbox/
        ├── chat-inbox.component.ts      # results signal; displayed = search ? results : conversations
        ├── chat-inbox.component.html    # one list; message/people sections removed; status line
        └── chat-inbox.component.spec.ts

specs/019-chat/
├── spec.md                              # "Amended by feature 046" callout; FR-034/035/036/050c, SC-006 marked superseded
└── contracts/chat-api.md                # Search section: one-line pointer to 046
```

**Structure Decision**: the existing web-application layout, touched in place. No new folder,
component, service class or endpoint: the feature is a predicate added to one query, a half
removed from one service, and a rewiring of one component. That smallness is deliberate — every
alternative that added a surface (a conversations half on `/chat/search`, a results component)
reintroduced the duplication feature 019 already paid for once (research §1, §4).

## Phase 2 — implementation sequencing

Ordered so each slice is independently demonstrable, in the spec's priority order:

1. **Backend, find by member (US1, P1)** — `HasMemberNamed` beside `IsMemberOf`; `q` on
   `GetInboxAsync` and the controller; `ChatInboxSearchTests` for member matches across Direct,
   Group and Team (Party where seeding allows), the 25-conversation case (SC-004), no-term
   identity (SC-007), hidden/blocked/former-member/self exclusions, accent and case, one-char
   term.
2. **Backend, remove message search (US2, P1)** — delete `SearchMessagesAsync`,
   `MessageSearchHitDto`, `ChatSearchResultDto.Messages`; trim and rewrite `ChatSearchTests`;
   the "message text never matches" and "no `messages` property" assertions (SC-002, FR-010).
3. **Backend, find by conversation name (US3, P2)** — `IsNamed`; tests for group name, team
   name, admin-contact thread name, and listed-once when both match.
4. **Frontend (US1–US3)** — model and service (`searchInbox`; `search` people-only), inbox
   component rewiring, catalogues in all three languages, Jest specs.
5. **Regression guard (US4, P2)** — run the existing new-chat, compose and profile quick-actions
   specs unchanged; confirm `ChatSearchTests`' people cases still pass.
6. **Documentation** — 019 amendments (spec callout + superseded markers; contract pointer).
7. **Polish** — `checklists/ui-review.md` verified against the diff (Gate 7); quickstart
   scenarios; full verification (backend `Chat` collection, frontend `web` tests incl. the
   catalogue parity spec, lint, build).

## Complexity Tracking

> No constitution violations require justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *(none)* | — | — |
