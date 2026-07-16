# Implementation Plan: Chat

**Branch**: `019-chat` | **Date**: 2026-07-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/019-chat/spec.md`

## Summary

In-app messaging: an inbox at a new top-level **Chat** nav destination, four kinds of conversation
(1:1 direct, manually-created named groups, and auto-created chats mirroring each **team** roster and
each event **party** roster), live delivery with typing indicators and read receipts, message search
scoped to the searcher's own conversations, view-only cards for pasted JuggerHub links, per-sender
message deletion, and mute / hide / block.

Technical approach, in one breath: **new `Chat*` entities behind a `Services/Chat` service set,
exposed by thin controllers over REST, with realtime pushed over a second SignalR hub cloned from
feature 010's push-only per-user-group design.** Three choices carry most of the weight —
(1) messages order by their **UUIDv7 `Id`**, which is monotonic, so chronological order is
server-assigned and identical for every viewer, and unread is a keyset comparison against a single
`LastReadMessageId` marker rather than a receipt table; (2) team/party chat membership is **derived
from the roster** rather than copied into participant rows, so a removed player losing chat access is
true by construction rather than by remembering to sync; and (3) link unfurl **parses our own routes
and reads our own database**, never fetching a URL, so the SSRF surface a normal unfurl service has
simply does not exist. Full reasoning in [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular 21 zoneless with signals (frontend)

**Primary Dependencies**: EF Core + Npgsql, Mapster (entity→DTO), ASP.NET Core SignalR
(`@microsoft/signalr` on the client), Microsoft Identity + JWT-in-httpOnly-cookie,
`Microsoft.AspNetCore.RateLimiting` (**newly registered by this feature** — see research §7),
Tailwind CSS

**Storage**: PostgreSQL 18. New tables: `Conversations`, `ConversationParticipants`, `ChatMessages`,
`UserBlocks`. Reads `TeamMemberships` / `PartyMembers` for derived membership.

**Testing**: xUnit integration tests in `backend/tests/JuggerHub.Api.IntegrationTests` (a
`Chat/` folder, following `Trainings/` and `Marketplace/`); Jest specs for frontend services

**Target Platform**: Linux containers (backend + frontend), Postgres; local via docker-compose

**Project Type**: Web application — .NET monorepo backend + Nx/Angular frontend

**Performance Goals**: A sent message reaches a connected participant in < 1 s on a healthy
connection. Inbox and history load within normal page budgets. Fan-out is O(participants) per message
— bounded by the 50-member group cap and by roster sizes (research §8).

**Constraints**: Live delivery is **best-effort over durable storage** (FR-023) — every value the
socket carries must be reachable on a normal REST load, so a disconnected client is never wrong, only
stale. Pagination is mandatory on every list. No unbounded collections. No outbound HTTP for unfurl.

**Scale/Scope**: Jugger-club scale — teams of ~9–30, groups capped at 50, parties bounded by their
roster cap. ~8 backend service files, 4 entities, 1 hub, 2 controllers; ~10 frontend components plus
a service and a realtime client. 8 user stories, 58 functional requirements.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | Verdict | How this feature satisfies it |
|---|-----------|---------|-------------------------------|
| I | **Security-first, never trust the client** (NON-NEGOTIABLE) | ✅ Pass | Every read/send authorized server-side against current membership (FR-047); a request for a non-member conversation is refused without disclosing existence (FR-048). Realtime fan-out resolves participants server-side and pushes only to validated per-user groups — a client cannot subscribe itself into a stream (research §1). Unfurl never fetches a URL, so **no SSRF** (§5), and cards re-check the *viewer's* permission, so a sender cannot leak a team-only training into a DM (FR-040). Blocking enforced on every send/start path, not in the UI (FR-031). Message bodies stored and rendered as plain text — never interpreted as markup (FR-014), closing the stored-XSS path a chat is the natural home for. Rate limiting added (§7). Generic errors via the existing global exception middleware. |
| II | **Thin controllers, service-centric** | ✅ Pass | Controllers do HTTP shaping only and forward to `IChatConversationService`, `IChatMessageService`, `IChatSearchService`, `IChatBlockService` — each behind an interface and DI-registered. No repository layer; services use EF directly. Services return entities; controllers map to DTOs with Mapster. |
| III | **Disciplined data access** | ✅ Pass | All four new entities derive from `BaseEntity` (UUIDv7 `Id`, audit dates via the interceptor). Reads use `AsNoTracking` + projections. Every list paginated: inbox / members / search via the shared `PaginationRequest`/`PagedResult<T>`; message history via **keyset** on `Id`, which the constitution explicitly prefers for large, rapidly-changing tables (§9). Any `ExecuteUpdateAsync` path sets `ModifiedDate` explicitly. |
| IV | **Secure auth & session** | ✅ Pass | No new auth. `ChatHub` reuses `NotificationHub`'s `[Authorize]` JWT-bearer scheme; the browser sends the httpOnly cookie on the same-origin handshake. No token touches `localStorage`. |
| V | **Environment parity & containerized deploys** | ⚠️ Pass with a flagged risk | No new service, no new infrastructure component, no new secret — chat rides the existing backend/frontend containers and Postgres, identical local/Dev/Prod. **Risk (research §10)**: SignalR runs in-process with no backplane, valid only while the backend is a single instance. The unmerged `015-hosting` branch moves to AKS; if it ever runs >1 replica, chat breaks visibly where notifications only degrade quietly. Not solved here — raised for 015. |
| VI | **Consistent conventions & tooling** | ✅ Pass | Angular components keep `.html` / `.css` / `.ts` separate. Any script added is `.ps1`. Search reuses the established `ILike` + `Unaccent` convention rather than introducing a second mechanism (§6). Nx + Tailwind throughout. |
| — | **Gate 7: UI/design compliance** | ✅ Pass | DESIGN.md is the source of truth: the wireframe's blue own-bubbles render coral, its invented nav is discarded for the real one, its illustrative link shapes for the real routes. All conflicts **reported** in research §11 and the spec's Assumptions, not silently resolved. `checklists/ui-review.md` is instantiated from the template and verified against the diff before verification. |

**Result: PASS.** One item (V) passes with a documented, deliberately-deferred risk that belongs to
another feature's branch. No violations require justification, so Complexity Tracking is empty.

*Post-Phase-1 re-check: still PASS.* The design added no repository layer, no new external dependency,
no new auth path and no unbounded list. `AddRateLimiter` is new middleware but is explicitly admitted
by principle II's "lean middleware" carve-out for security middleware, and is required by FR-049a.

## Project Structure

### Documentation (this feature)

```text
specs/019-chat/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions & rejected alternatives
├── data-model.md        # Phase 1 — entities, relationships, rules
├── quickstart.md        # Phase 1 — how to run & validate the feature
├── contracts/
│   └── chat-api.md      # Phase 1 — REST + realtime event contract
├── checklists/
│   ├── requirements.md  # Spec quality (16/16)
│   └── ui-review.md     # DESIGN.md compliance, verified against the diff
└── tasks.md             # Phase 2 — /speckit-tasks output
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── ChatEnums.cs                    # ConversationKind, MessageKind, LinkKind, ConversationState
│   ├── Conversation.cs
│   ├── ConversationParticipant.cs      # per-user state; membership for Direct/Group only
│   ├── ChatMessage.cs
│   └── UserBlock.cs
├── Data/
│   ├── AppDbContext.cs                 # +4 DbSets, indexes, unique constraints
│   └── Migrations/                     # one migration
├── Dtos/Chat/                          # ConversationSummaryDto, MessageDto, LinkCardDto, …
├── Services/Chat/
│   ├── ChatGuard.cs                    # membership resolution (derived for team/party) + block checks
│   ├── IChatConversationService.cs / ChatConversationService.cs
│   ├── IChatMessageService.cs / ChatMessageService.cs
│   ├── IChatSearchService.cs / ChatSearchService.cs
│   ├── IChatBlockService.cs / ChatBlockService.cs
│   ├── ChatLinkParser.cs               # route-shape → (LinkKind, id); no network
│   ├── ChatLinkResolver.cs             # (kind,id) → card, per *viewer* permission
│   └── Realtime/
│       ├── IChatRealtime.cs
│       ├── ChatHub.cs                  # push-only, user:{id} groups (clone of NotificationHub)
│       └── SignalRChatRealtime.cs
├── Controllers/
│   ├── ChatConversationsController.cs
│   └── ChatMessagesController.cs
├── Program.cs                          # DI, AddRateLimiter, MapHub<ChatHub>("/hubs/chat")
└── tests/JuggerHub.Api.IntegrationTests/Chat/

frontend/apps/web/src/app/
├── core/
│   ├── models/chat.models.ts
│   └── services/chat.service.ts        # signals + REST + SignalR (mirrors notification.service.ts)
├── layout/nav-model.ts                 # + 'chat' NavId, badge
└── features/chat/
    ├── chat-inbox/                     # rows, tags, typing, empty state, search
    ├── chat-conversation/              # bubbles, divider, jump pill, composer, receipts
    ├── chat-details/                   # members, shared, add/leave/mute/hide/block
    ├── chat-new/                       # one person → DM; several → named group
    └── chat-shell/                     # mobile screens ↔ desktop rail+conversation+panel
```

**Structure Decision**: The existing web-application layout (`backend/` + `frontend/apps/web/`) with
per-feature service folders. Chat follows the shape feature 018 (Trainings) established most
recently: a guard class plus several focused services behind interfaces, a `Realtime/` sub-folder
copying feature 010's seam, DTOs in `Dtos/Chat/`, and an Angular feature folder with one component
per screen and a signal-based core service.

## Phase 2 — implementation sequencing

Ordered so each slice is independently demonstrable, matching the spec's story priorities:

1. **Foundation** — entities, `AppDbContext` wiring, migration, `ChatGuard`, DI, rate limiter.
2. **US1 (P1)** — conversations + messages + inbox + unread over REST; the loop works on refresh.
3. **US2 (P1)** — `ChatHub`, `IChatRealtime`, typing endpoint, client socket; the loop goes live.
4. **US3 (P2)** — named groups, add/leave, system lines, sender labels.
5. **US4 (P2)** — derived team/party chats, ensure-on-access, TEAM/PARTY tags, archive on disband.
6. **US5 (P2)** — mute, hide, block.
7. **US6 (P3)** — message + people search.
8. **US7 (P3)** — link parse, per-viewer resolve, cards.
9. **US8 (P3)** — desktop rail + details panel.
10. **Polish** — UI review checklist against DESIGN.md, quickstart validation, full verification.

## Complexity Tracking

> No constitution violations require justification. The single ⚠️ (principle V, no SignalR backplane)
> is an **inherited** decision from feature 010 that remains valid on main's single-instance target,
> and is recorded as a risk against the unmerged `015-hosting` branch rather than as a violation here
> — building a backplane for a deployment topology that has not landed would be speculative work.
> See research §10.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *(none)* | — | — |
