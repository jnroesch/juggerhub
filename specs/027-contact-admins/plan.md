# Implementation Plan: Contact the Admins

**Branch**: `027-contact-admins` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/027-contact-admins/spec.md`

## Summary

Add a **"Contact admins"** action to team pages and event pages that opens a chat thread between the
viewing player and that team's / event's admins. The thread is a **new mirrored `ConversationKind`**
whose admin-side membership is derived live from the admin roster (exactly like `Team`/`Party` chats
in `ChatGuard`), with the requesting player as the one fixed participant. It is created lazily on the
first sent message (feature 022 precedent), reused per (player, target) pair (feature 019 FR-008
precedent), tagged and named distinctly in the inbox (player sees the team/event name; admins see the
player's name), archived by snapshot when the team is deleted or the event is cancelled, and governed
by the existing chat rate limit, read-state, real-time, and "404-not-403" machinery.

To avoid a name clash with the existing **`EventContact` entity** (feature 006 — contact persons on
an event), the new kinds are named **`TeamInquiry`** and **`EventInquiry`** and the requester column
is `RequesterUserId`; the user-facing tag reads **"ADMINS"**.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular + Nx (frontend)

**Primary Dependencies**: Entity Framework Core (PostgreSQL 18), Mapster, SignalR + Redis backplane
(chat realtime, feature 019), Tailwind CSS

**Storage**: PostgreSQL 18. Reuses `Conversation`, `ConversationParticipant`, `ChatMessage`; adds two
nullable columns to `Conversation` and re-scopes/adds indexes. One EF migration.

**Testing**: xUnit integration tests (`JuggerHub.Api.IntegrationTests/Chat`), Angular/Jest zoneless
component specs. Follow the existing chat test support (`ChatTestSupport`, `FakeChatRealtime`).

**Target Platform**: Linux containers on AKS (Dev/Prod), docker-compose (local).

**Project Type**: Web application (ASP.NET Core API + Angular SPA).

**Performance Goals**: Inbox and access checks stay within chat's existing budget — membership resolves
in the same DB round trip via a projected predicate; no per-conversation N+1 introduced (cutoffs
batched, mirroring `ResolveJoinCutoffsAsync`).

**Constraints**: Server-side authorization only; non-member ⇒ 404 (never 403); paginated reads;
entities → DTOs via Mapster; `.html`/`.css`/`.ts` kept separate; DESIGN.md governs UI.

**Scale/Scope**: Small feature riding an existing subsystem. ~2 enum members, 2 columns, 3 index
changes, ~4 guard branches, 1 lazy-create + 1 archival path, 2–3 endpoints, 2 frontend entry points,
1 inbox tag case.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|-----------|------|--------|
| I. Security-first, never trust the client | All access via `ChatGuard` server-side; caller-supplied ids validated; admin-of-target check server-side; 404 for non-members; no secrets/stack traces to client | PASS — reuses the single guard; no new client-trust surface |
| II. Thin controllers, service-centric | New endpoints delegate to `IChatConversationService`; entities→DTO via Mapster; services expose interfaces | PASS |
| III. Disciplined data access (EF + PG) | New columns on `BaseEntity`-derived `Conversation`; projections + `AsNoTracking`; paginated lists; unique **filtered** indexes enforce one-thread-per-pair in the DB; archival sets `ModifiedDate` via tracked save | PASS |
| IV. Secure auth & sessions | Endpoints under existing `[Authorize]` JWT chat controller; no auth changes | PASS |
| V. Env parity & containerized | No infra change; Redis backplane already present; works identically local/Dev/Prod | PASS |
| VI. Conventions & tooling | Angular files kept separate; PowerShell-only scripts; no new script | PASS |
| Gate 7. UI/Design compliance | Entry-point button, inbox "ADMINS" tag, empty/loading/error states checked against DESIGN.md via the UI review checklist before verification | PASS (checklist instantiated in Phase 1) |

No violations. **Complexity Tracking table not required.**

## Project Structure

### Documentation (this feature)

```text
specs/027-contact-admins/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions & rationale
├── data-model.md        # Phase 1 — entities, columns, indexes, membership rules
├── quickstart.md        # Phase 1 — end-to-end validation guide
├── contracts/
│   └── chat-contact-api.md   # Phase 1 — the new endpoints
├── checklists/
│   └── requirements.md  # from /speckit-specify
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── ChatEnums.cs                 # + TeamInquiry, EventInquiry on ConversationKind
│   └── Conversation.cs              # + EventId, RequesterUserId; doc the new kinds
├── Data/
│   └── AppDbContext.cs              # re-scope IX_Conversations_TeamId by Kind; add inquiry
│                                    #   unique filtered indexes + Event/Requester FKs (Restrict)
├── Migrations/                      # + one migration (columns + indexes + FKs)
├── Dtos/Chat/
│   └── ChatDtos.cs                  # + ContactMessageSentDto (or reuse DirectMessageSentDto)
├── Services/Chat/
│   ├── ChatGuard.cs                 # + inquiry branches: IsMemberOf, ResolveAsync,
│   │                               #   ResolveParticipantUserIds, join-cutoffs; ChatAccess += EventId, RequesterUserId
│   ├── IChatConversationService.cs  # + SendFirstInquiry*, EnsureInquiry*, Archive*ForEvent/Team
│   └── ChatConversationService.cs   # lazy create-on-send; inbox projection naming/avatar;
│                                    #   generalized snapshot archival for many conversations
├── Controllers/
│   └── ChatConversationsController.cs  # + POST contact endpoints (rate-limited), optional GET resolve
├── Services/Teams/TeamService.cs    # DeleteAsync also archives team inquiry threads
└── Services/Events/EventService.cs  # CancelAsync archives event inquiry threads

frontend/apps/web/src/app/
├── core/
│   ├── models/chat.models.ts        # + TeamInquiry, EventInquiry kinds; request/response types
│   └── services/chat.service.ts     # + sendFirstInquiryToTeam/Event, resolveInquiryThread
├── features/chat/
│   ├── chat-inbox/…                 # tagFor(): "ADMINS" for the two inquiry kinds
│   └── chat-compose/…               # accept a team/event inquiry target → calls contact endpoint
├── features/teams/team-detail/…     # "Contact admins" button (hidden for team admins)
└── features/events/event-detail/…   # "Contact admins" button (hidden for event admins)

backend/tests/JuggerHub.Api.IntegrationTests/Chat/
└── ChatInquiryTests.cs (+ additions to ChatArchiveTests, ChatGuard/Access coverage)
```

**Structure Decision**: Web application. This feature is an extension of the feature-019 chat
subsystem — no new project, no new architectural layer. All backend logic lands in the existing
`Services/Chat` service behind its interface; the two entry points reuse the existing chat compose
flow and inbox rendering.

## Key Design Decisions (see research.md for full rationale)

1. **New mirrored kinds `TeamInquiry` / `EventInquiry`**, not a reused `Group`. Mirrored membership
   gives derived admin access (FR-006) and the distinguishing tag (FR-008) by construction; a plain
   group would need a sync step and a special-cased name.
2. **Reuse `Conversation.TeamId` for the team target; add `EventId` for the event target; add
   `RequesterUserId` for the fixed player.** Matches the codebase's column-per-relation style
   (TeamId/PartyId) over a polymorphic target. Requires re-scoping the existing one-chat-per-team
   unique index to `Kind = Team` so inquiry rows don't collide.
3. **Uniqueness per pair via unique filtered indexes** — `(TeamId, RequesterUserId) WHERE Kind =
   TeamInquiry` and `(EventId, RequesterUserId) WHERE Kind = EventInquiry` — so two concurrent first
   sends collide in the DB and resolve to one thread (FR-004), exactly like `DirectPairKey`.
4. **Create-on-first-send** (feature 022): `SendFirstInquiryAsync` validates → ensures (race-safe) →
   sends. Opening the compose persists nothing (FR-005).
5. **Admins need no participant rows** — `ChatGuard.IsMemberOf` resolves them from the roster, so they
   appear in their inbox automatically the moment a thread exists (no backfill, no `EnsureAutoChatsFor`
   entry for inquiries).
6. **Archival is the existing snapshot pattern generalized to N conversations.** Team delete archives
   the team chat **and** every team-inquiry thread; event cancel archives every event-inquiry thread.
   Snapshot roster → freeze name → null the link → set Archived, before any hard delete (R3a).
7. **Naming/avatar branch on viewer**: requester sees team name / event title + team-or-event avatar;
   an admin sees the requester's display name + the requester's avatar. Tag "ADMINS" for both.
8. **Blocking excluded; no new notification rows; existing `ChatStart` rate limit** on the send-first
   endpoints.

## Phase 0 — Research

See [research.md](research.md). All Technical Context items resolved; no NEEDS CLARIFICATION remain.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md) — the two enum members, the two new columns, index changes, FK
  behaviors, and the membership/cutoff rules per kind (including the archival snapshot invariant).
- [contracts/chat-contact-api.md](contracts/chat-contact-api.md) — the new endpoints and their DTOs.
- [quickstart.md](quickstart.md) — an end-to-end validation walkthrough on the docker-compose stack.
- UI review checklist to be instantiated at implementation from
  `.specify/templates/ui-review-checklist-template.md` into `checklists/ui-review.md`.

## Complexity Tracking

No constitution violations; table intentionally omitted.
