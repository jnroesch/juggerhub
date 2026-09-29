# Implementation Plan: The Party Page Leads Into the Party Chat

**Branch**: `063-party-chat-link` | **Date**: 2026-09-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/063-party-chat-link/spec.md` (GH #382)

## Summary

Crew members get a **Party chat** button on the party page that opens the party's own chat
(`ConversationKind.Party`), creating it first if nobody in the crew has opened Chat yet. It is 060's
team-chat link (GH #362) applied to parties, backed by one new endpoint,
`GET /api/v1/chat/party/{partyId}` → `{ conversationId }`, which:

1. runs the inbox's own creation step (`EnsureAutoChatsForAsync(caller)`), which creates missing
   chats only for the caller's own teams and crews;
2. finds the party chat (`FindAutoAsync(Party, partyId)`);
3. lets **`ChatGuard.ResolveAsync`** alone decide access, so everyone outside the crew, a made-up
   id, a disbanded party and another entity's id all get the same generic 404.

**The order lives in ONE place (research R1).** 060 wrote it into `OpenTeamChatAsync`. This feature
extracts the three steps into a private `OpenAutoChatAsync(callerId, kind, ownerId)` that both public
methods call. The order is the security design, and two copies of it would be free to drift. 060's
own test suite is the regression net for the extraction and must pass unedited.

**No defect to fix first, unlike 060.** 060's defect came from 027's inquiry threads sharing
`TeamId`. Nothing but the party chat uses `PartyId` (the unique index at `AppDbContext.cs:1129` is
filtered on `PartyId IS NOT NULL` alone), and 060 already scoped both party lookups by kind
(`ChatConversationService.cs:594-598`, `:1105-1110`). Checked by reading, not assumed (research R2).

**Owner decisions (spec Clarifications)**:

- **Placement: the viewer's own card at the top.** A crew member who is not a party admin gets it
  beside *Leave party* in the "You're in this crew" card. A party admin gets it beside *Apply to
  event* / *Withdraw* in the readiness card, as a secondary button.
- **Team members outside the crew keep 016's page**, without the button and without a hint. The
  owner asked why they see the page at all; the answer (the page is where the team-wide request is
  answered) was accepted, so who may see the page is unchanged.
- **Disbanded parties need nothing**: disband is a hard delete, so the page is "not found".
- **No unread count** (060's decision, carried over): resolve on press, and the page's load is
  unchanged.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Angular 22 zoneless + Nx (frontend)

**Primary Dependencies**: EF Core + Npgsql, ASP.NET Core MVC, Transloco, Tailwind. **No new
dependency.**

**Storage**: PostgreSQL 18. **No schema change, no migration** (data-model.md).

**Testing**: xUnit integration tests against Testcontainers Postgres (new
`backend/tests/JuggerHub.Api.IntegrationTests/Parties/PartyChatLinkTests.cs`, in the `Parties`
collection because the party seeding helpers live in `PartyTestSupport`), Jest component and service
specs (the party page has **no spec today**, so this feature creates it), and the catalogue guards.

**Target Platform**: web (nginx-served SPA plus the API on AKS; compose locally).

**Project Type**: web application (`backend/` + `frontend/apps/web/`).

**Performance Goals**: none new. The endpoint runs on a press, not on page load: the inbox's two
`NOT EXISTS` reads, one lookup and one guard query, exactly 060's cost.

**Constraints**: the party page's load makes the same requests as today for every viewer (SC-004);
German at 375px (SC-006).

**Scale/Scope**: 1 endpoint, 1 DTO, 1 private helper extracted from 060's method, 1 component
(template + TS) and its new spec, 1 service method, 4 i18n keys × 3.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design: still passes.*

| Gate | Status | Notes |
|------|--------|-------|
| 1 Architecture | ✅ | Thin controller action (`ChatConversationsController.OpenPartyChat`) forwards to `IChatConversationService.OpenPartyChatAsync`; returns `ChatResult<PartyChatRefDto>`; failures go through the existing `Fail` → `ChatHttp`. No repository, no mapper. |
| 2 Data access | ✅ | Reads are `AsNoTracking` projections to `Guid`. The only write is the existing `EnsureAutoAsync` insert through the change tracker, so the audit interceptor runs. No `ExecuteUpdate`, no list endpoint. |
| 3 Security | ✅ | Authorised server-side by `ChatGuard` (FR-004), whose party branch is `PartyMembers.Status == In`, marketplace guests included. Everyone outside the crew, a non-existent or disbanded party and another kind's id give the identical 404 (FR-005, 019 FR-048). A request from outside the crew creates nothing for that party (FR-007): creation is scoped to the caller's own crews. The response carries only an id (FR-006). The client's `isCrew()` check is UX only. |
| 4 Auth | ✅ | Controller-level `[Authorize(JwtBearer)]`; no anonymous surface, so the 026 OpenAPI allowlist is not touched. |
| 5 Conventions | ✅ | The party page keeps its separate `.html`/`.css`/`.ts`; the new spec is a `.spec.ts`; no scripts. |
| 6 Parity | ✅ | No configuration. |
| 7 UI/Design | ⚠ **Engaged** | New button in two existing cards, one inline error line, one page-level notice, new copy → `checklists/ui-review.md`. Binding case: both cards in **German at 375px** (research R6: the admin row wraps). One deliberate visible change to existing controls: *Leave party* and *Withdraw* move from `size="sm"` (36px) to the default 44px so each row is even and meets DESIGN.md's touch-target rule (research R6). |
| 8 Resilience | ✅ Not engaged as an integration | No outbound call. The new call is a browser `GET` the interceptor may retry; it is **replay-safe** because creation is idempotent (the filtered unique index plus `EnsureAutoAsync`'s catch-and-re-find), 060 research R3's argument unchanged. No rate-limit policy (the inbox `GET` that does the same has none). |

No violations, so Complexity Tracking stays empty.

## Project Structure

### Documentation (this feature)

```text
specs/063-party-chat-link/
├── spec.md
├── plan.md              # this file
├── research.md          # R1 shared order, R2 no defect, R3 who is crew, R4 DTO, R5 FE flow, R6 placement/size, R7 copy
├── data-model.md        # no schema change; invariants I1–I4
├── quickstart.md
├── contracts/
│   └── party-chat-api.md
├── checklists/
│   ├── requirements.md
│   └── ui-review.md     # created during implementation (Gate 7)
└── tasks.md             # /speckit-tasks
```

### Source Code

```text
backend/
├── Dtos/Chat/ChatDtos.cs                          # + PartyChatRefDto(Guid ConversationId)
├── Services/Chat/IChatConversationService.cs      # + OpenPartyChatAsync
├── Services/Chat/ChatConversationService.cs       # + OpenPartyChatAsync; OpenTeamChatAsync's body
│                                                  #   → private OpenAutoChatAsync (R1)
├── Controllers/ChatConversationsController.cs     # + GET party/{partyId:guid}
└── tests/JuggerHub.Api.IntegrationTests/Parties/
    └── PartyChatLinkTests.cs                      # new

frontend/apps/web/
├── src/app/core/models/chat.models.ts             # + PartyChatRef
├── src/app/core/services/chat.service.ts          # + openPartyChat(partyId)
├── src/app/core/services/chat.service.spec.ts     # + URL/verb test
├── src/app/features/parties/party-manage/
│   ├── party-manage.component.html                # Party chat in the crew card and the readiness
│   │                                              #   card; inline error; page-level notice
│   ├── party-manage.component.ts                  # openPartyChat(), partyChatBusy/Error/Notice
│   └── party-manage.component.spec.ts             # NEW: placement, press, busy, 404, failure
└── public/i18n/{en,de,es}.json                    # 4 keys under parties.manage (R7)

specs/019-chat/contracts/chat-api.md               # "Amended by feature 063" pointer
```

**Structure Decision**: the existing web-application layout. Everything lands in files that already
own the concern: the chat service owns chat lookups, and the party page owns its actions.

## Design notes the tasks depend on

- **`OpenAutoChatAsync` keeps 060's order verbatim** (research R1): ensure (caller-scoped), then
  find, then guard. Do **not** call `EnsureForPartyAsync(partyId)` directly: anyone could create any
  party's chat, and an unknown id fails the foreign key and rethrows (500). Do **not** add a
  `PartyMembers.Any(...)` check: that would be the second membership rule FR-004 forbids. The
  helper's doc comment carries 060's "why", moved from `OpenTeamChatAsync`, not duplicated.
- **060's `ChatTeamChatLinkTests` pass unedited** after the extraction. Run them in the same step.
- **Test seeding**: the crew member joins through the real `POST /parties/{id}/join` (a team member
  with a spot). The marketplace guest is seeded directly as a `PartyMember { Status = In, ViaMarket =
  true }` of a user who is **not** on the team, which is the state 017's accept path produces. The
  disbanded case uses the real `DELETE /parties/{id}`, so the archive path (`PartyId` nulled) is what
  answers 404.
- **Another kind's id**: pass the party's **team id**, after its team chat exists, and expect 404.
  That pins `FindAutoAsync`'s kind scoping for parties (research R2).
- **Frontend state is signals** (zoneless): `partyChatBusy`, `partyChatError` (in the card),
  `partyChatNotice` (page level, beside `error()`; it must survive the reload that swaps the crew card
  for the request card). The page reads its id from the route snapshot and has no `paramMap`
  subscription, so there is nothing to reset on navigation.
- **`isCrew()` already exists** (`myState === 'In' || 'Admin'`) and matches `ChatGuard`'s rule for
  every viewer the page renders (research R3). The crew card's `@if (p.myState === 'In')` branch and
  the `isAdmin()` readiness card are exactly the two places; no new computed is needed.
- **Error text never comes from the server** (GH #179). The page's existing `fail()` still renders
  `err.error?.detail` for its other actions. That is pre-existing, out of scope, and **not** copied.
- **Injecting `ChatService` into the page**: it is a root service with a constructor effect and
  `HttpClient`, so the new spec stubs it (060's lesson). No other spec renders `PartyManageComponent`
  (checked by grep), so no existing TestBed needs the stub.

## Complexity Tracking

None.
