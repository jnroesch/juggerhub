# Implementation Plan: The Team Page Leads Into the Team Chat

**Branch**: `060-team-chat-link` | **Date**: 2026-09-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/060-team-chat-link/spec.md` (GH #362)

## Summary

Members get a **Team chat** button on the team page that opens the team's own chat
(`ConversationKind.Team`), creating it first if nobody has opened Chat yet. It is backed by one new
endpoint, `GET /api/v1/chat/team/{teamId}` → `{ conversationId }`, which:

1. runs the inbox's own creation step (`EnsureAutoChatsForAsync(caller)`), which creates missing
   chats only for the caller's own rosters;
2. finds the team chat (`FindAutoAsync`);
3. lets **`ChatGuard.ResolveAsync`** alone decide access, so every non-member and made-up id gets the
   same generic 404.

**⚠ LOAD-BEARING: a pre-existing defect, found by reading (research R1).** Feature 027 put
Contact-admins threads (`TeamInquiry`) on the same `TeamId` column and re-scoped the unique index to
`Kind = 2`, but the **service** checks were never scoped. `EnsureAutoChatsForAsync` skips a team
that has *any* conversation, and `FindAutoAsync` returns *any* conversation with that `TeamId`. So
a team whose first conversation is a Contact-admins thread **never gets its chat**. The new
endpoint would open that **private thread for an admin** and 404 a plain member on their own team.
The fix adds `Kind == Team` to both checks. No migration: affected teams heal on the next inbox
load.

**Owner decisions (spec Clarifications)**:

- **No unread count.** The button resolves on press, and the team page's load is unchanged.
- **All member actions move into the side-column card.** For members the header carries no
  actions; only non-members, signed-out visitors and pending requesters see actions on top. A plain
  member's *Contact admins* moves from the header into the card with *Team chat*, *Invites* (admin)
  and *Manage*. Accepted: on phones that card sits at the bottom of the page.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 22 zoneless + Nx (frontend)

**Primary Dependencies**: EF Core + Npgsql, ASP.NET Core MVC, Transloco, Tailwind. **No new
dependency.**

**Storage**: PostgreSQL 18. **No schema change, no migration** (data-model.md).

**Testing**: xUnit integration tests against Testcontainers Postgres
(`backend/tests/JuggerHub.Api.IntegrationTests/Chat/`), Jest component and service specs, and the
catalogue-parity guard.

**Target Platform**: web (nginx-served SPA plus the API on AKS; compose locally).

**Project Type**: web application (`backend/` + `frontend/apps/web/`).

**Performance Goals**: none new. The endpoint runs on a press, not on page load: two `NOT EXISTS`
reads, one lookup and one guard query.

**Constraints**: team page load makes the same requests as today (SC-004); 375px German layout
(SC-006).

**Scale/Scope**: 1 endpoint, 1 DTO, 2 predicate fixes, 1 component (template + TS), 1 service
method, 4 i18n keys × 3.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design: still passes.*

| Gate | Status | Notes |
|------|--------|-------|
| 1 Architecture | ✅ | Thin controller action (`ChatConversationsController.OpenTeamChat`) forwards to `IChatConversationService.OpenTeamChatAsync`; returns `ChatResult<TeamChatRefDto>`; failures go through the existing `ChatHttp.Fail`. |
| 2 Data access | ✅ | Reads are `AsNoTracking` projections to `Guid`. The only write is the existing `EnsureAutoAsync` insert through the change tracker, so the audit interceptor runs. No `ExecuteUpdate`, no list endpoint. |
| 3 Security | ✅ | Authorised server-side by `ChatGuard` (FR-004). Non-member, non-existent and other-kind ids give the identical 404 (FR-005, 019 FR-048). A non-member's request creates nothing for the team (FR-007): creation is scoped to the caller's own rosters. Response carries only an id (FR-006). The client check (`isMember()`) is UX only. |
| 4 Auth | ✅ | Controller-level `[Authorize(JwtBearer)]`; no anonymous surface, so the 026 OpenAPI allowlist is not touched. |
| 5 Conventions | ✅ | Existing component keeps its separate `.html`/`.css`/`.ts`; no scripts. |
| 6 Parity | ✅ | No configuration. |
| 7 UI/Design | ⚠ **Engaged** | Markup moves and new copy → `checklists/ui-review.md`. Binding case: the member card in **German at 375px** (four buttons for a plain member at most, full labels), plus the header for a non-member, unchanged. |
| 8 Resilience | ✅ Not engaged as an integration | No outbound call. The new call is a browser `GET` the interceptor may retry; it is **replay-safe** because creation is idempotent (filtered unique index plus catch-and-re-find), the same property the inbox `GET` relies on (research R3). No rate-limit policy (research R3). |

No violations, so Complexity Tracking stays empty.

## Project Structure

### Documentation (this feature)

```text
specs/060-team-chat-link/
├── spec.md
├── plan.md              # this file
├── research.md          # R1 defect, R2 no second oracle, R3 GET/replay, R4 DTO, R5 FE flow, R6 placement, R7 copy
├── data-model.md        # no schema change; invariants I1–I4
├── quickstart.md
├── contracts/
│   └── team-chat-api.md
├── checklists/
│   ├── requirements.md
│   └── ui-review.md     # created during implementation (Gate 7)
└── tasks.md             # /speckit-tasks
```

### Source Code

```text
backend/
├── Dtos/Chat/ChatDtos.cs                          # + TeamChatRefDto(Guid ConversationId)
├── Services/Chat/IChatConversationService.cs      # + OpenTeamChatAsync
├── Services/Chat/ChatConversationService.cs       # + OpenTeamChatAsync; Kind filter in
│                                                  #   EnsureAutoChatsForAsync + FindAutoAsync (R1)
├── Controllers/ChatConversationsController.cs     # + GET team/{teamId:guid}
└── tests/JuggerHub.Api.IntegrationTests/Chat/
    └── ChatTeamChatLinkTests.cs                   # new

frontend/apps/web/
├── src/app/core/models/chat.models.ts             # + TeamChatRef
├── src/app/core/services/chat.service.ts          # + openTeamChat(teamId)
├── src/app/core/services/chat.service.spec.ts     # + URL/verb test (if the spec exists; else new)
├── src/app/features/teams/team-detail/
│   ├── team-detail.component.html                 # header block @if (!isMember()); card gains
│   │                                              #   Team chat + Contact admins; notices
│   ├── team-detail.component.ts                   # openTeamChat(), teamChatBusy/Error/Notice
│   └── team-detail.component.spec.ts              # placement + press/404/failure/busy
└── public/i18n/{en,de,es}.json                    # 4 keys under teams.detail (R7)

specs/019-chat/contracts/chat-api.md               # "Amended by feature 060" pointer
```

**Structure Decision**: the existing web-application layout. Everything lands in files that already
own the concern: the chat service owns chat lookups, and the team page owns its actions.

## Design notes the tasks depend on

- **`OpenTeamChatAsync` order is load-bearing** (research R2): ensure (caller-scoped), then find,
  then guard. Do **not** call `EnsureForTeamAsync(teamId)` directly. It would let anyone create a
  chat for any team, and it 500s on a made-up id through the FK violation it rethrows. Do **not**
  add a `TeamMemberships.Any(...)` check either. That would be the second membership rule FR-004
  forbids.
- **Both predicate fixes ship with a test that fails before them**: a Contact-admins thread created
  before any inbox load, then (a) the inbox lists a `kind = "Team"` row for the team, and (b) an
  admin's `GET chat/team/{id}` returns a conversation whose `kind` is `Team`, not the inquiry.
- **`ChatTeamPartyTests.A_team_has_exactly_one_chat_under_concurrent_first_access`** counts
  `c.TeamId == teamId` without a kind filter. It stays correct (no inquiry in that test) and is not
  edited.
- **Frontend state is signals** (zoneless): `teamChatBusy`, `teamChatError` (card), `teamChatNotice`
  (page level; must survive the reload that removes the card). Reset `teamChatNotice`/`teamChatError`
  on a team switch in the `paramMap` subscription, as 058 does for `joinNotice`.
- **The header block is wrapped in `@if (!isMember())`**, not individually per button. That also
  removes today's empty `w-full` row an admin gets in the header.
- **Existing specs**: the team-detail spec's `manage-team` / `team-tools` tests keep passing
  unedited. Any test asserting `contact-admins` in the header for a *member* must now find it in
  the card. There is none today (checked by grep).

## Complexity Tracking

None.
