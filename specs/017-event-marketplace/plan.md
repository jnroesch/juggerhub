# Implementation Plan: Event Marketplace (Mercenaries)

**Branch**: `017-event-marketplace` | **Date**: 2026-07-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/017-event-marketplace/spec.md`

## Summary

Add a two-sided **mercenary market** to the event page of a **teams** event, extending Event Parties
(016) and Events (006). Individuals with no crew seat **post themselves as free agents** (positions +
pitch); party admins flip a party into **recruiting** (opt-in, spots/positions/blurb). Both sides meet
through one **two-way handshake**: a free agent **applies** to a party, or a party admin **invites** a
mercenary — each a pending **MarketRequest** the other side must **accept** or **decline** (revocable
before answered). Accepting seats the mercenary as a **guest** `PartyMember` (In, counted toward the
016 roster cap, "guest · via market" tag), atomically under the existing `PartyCapacity` lock; joining
one crew **auto-cancels** the joiner's other pending requests and **takes down** their listing. The
mercenary is reachable in three places — the event board, a **dashboard market module**, and an in-app
**notification + email** per invite. Party admins can also **invite any eligible user directly** by
name/@handle search (mirroring the 006 event co-admin search).

**Technical approach**: Two new aggregates — `MercenaryListing` (event+user) and `MarketRequest`
(party+user, directional) — plus **recruiting fields on `Party`** and a **guest marker on
`PartyMember`**, and a new `NotificationType.MarketInvite`, in one EF Core migration. Three services
behind interfaces mirror the parties/events slices one-for-one: `MarketListingService` (post/edit/
take-down + free-agents board side + eligibility), `MarketRecruitingService` (toggle recruiting +
parties board side), and `MarketRequestService` (apply/invite/accept/decline/revoke + both inboxes +
dashboard summary + join side-effects). They reuse the established `PartyGuard`, `PartyResult`/
`PartyOutcome`, `PartyCapacity` (row-lock), the 006 all-user search shape, `INotificationService`
fan-out, and a new `MarketEmailService` (one template). The guest reconciliation touches
`PartyService.ProjectAsync` and `PartyRosterService.ListGroupAsync` so guests are counted and listed.
A thin `MarketController` (event-scoped board/listing/mercenary-inbox/dashboard) plus recruiting/
handshake endpoints joins the existing `PartiesController` surface. The Angular client gains a
`marketplace` feature area (board embedded in event-detail, post-listing + apply/invite sheets, a
recruiting block in party-manage, the party recruiting inbox, the mercenary market inbox, a dashboard
market module, direct-invite search) plus an alerts renderer for `MarketInvite`.

## Technical Context

**Language/Version**: C# / .NET 10 (backend, `backend/`), TypeScript / Angular 21 (frontend Nx
workspace, `frontend/`). Zoneless change detection (no `fakeAsync` in specs — see
`catalogue-014-decisions`).

**Primary Dependencies**: EF Core 10 + Npgsql (PostgreSQL 18), Microsoft Identity, Mapster
(entity→DTO), Asp.Versioning; Angular + Nx + Tailwind, Angular signals/`resource`.

**Storage**: PostgreSQL. New tables `MercenaryListings`, `MarketRequests`; new columns on `Parties`
(`IsRecruiting`, `SpotsAdvertised`, `RecruitBlurb`, `PositionsNeeded`) and `PartyMembers`
(`ViaMarket`). Positions stored as an `int[]` of `Pompfe` values (Postgres array), matching the
profile pompfen shape.

**Testing**: Backend xUnit integration tests (`backend/tests/JuggerHub.Api.IntegrationTests`,
WebApplicationFactory as used by the events/parties/teams suites); Angular specs (zoneless); optional
Playwright e2e (`frontend/apps/web-e2e`).

**Target Platform**: Linux containers (backend + Postgres via docker-compose locally); responsive
web (phone + desktop) per DESIGN.md.

**Project Type**: Web application (separate `backend/` API + `frontend/` SPA).

**Performance Goals**: Standard interactive web latency; all list surfaces paginated; reads use
projections + `AsNoTracking`; the accept path serializes on the existing single party-row lock (no
table scans).

**Constraints**: Never-trust-the-client — every authorization decision server-side; no raw
exceptions/secrets to the client; DTOs out via Mapster; `.html`/`.css`/`.ts` kept separate;
PowerShell-only scripts; environment parity (local/Dev/Prod identical behavior).

**Scale/Scope**: Adds 2 entities, 2 column-sets, 1 migration, 3 backend services + 1 controller (plus
edits to `PartiesController`, `PartyService`, `PartyRosterService`), and one Angular feature area
(~8 components) plus edits to event-detail, party-manage, the dashboard, and the alerts renderer. No
new middleware, no new infrastructure.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1. Constitution v1.1.0.*

| Principle | Applies | Compliance |
|---|---|---|
| I. Security-first, never trust client | Yes | Every marketplace action (post/edit/take-down listing, toggle recruiting, apply/invite/accept/decline/revoke, direct-invite, read inbox) authorized server-side via `PartyGuard` (party side) or owner-id checks (listing/inbox side). Eligibility ("not In a party here") enforced server-side. Board reads are public per event but member/owner-gated writes; inbox reads scoped to the caller's user id. Generic errors only; no secrets/stack traces to client. |
| II. Thin controllers, service-centric | Yes | One new thin controller (`MarketController`) + a few endpoints on `PartiesController` delegate to DI'd services behind interfaces; no repository layer; entities→DTOs via projections/Mapster. |
| III. Disciplined data access (EF + PG) | Yes | New entities derive from `BaseEntity` (UUIDv7); audit fields via interceptor; reads use `.Select`/`AsNoTracking`; every list paginates via `PaginationRequest`/`PagedResult<T>`; accept uses the existing row-lock + atomic write; `ExecuteUpdateAsync` cancel/cleanup paths set `ModifiedDate`. |
| IV. Secure auth & sessions | Yes | Reuses existing Identity/JWT-in-httpOnly-cookie unchanged; no auth surface added; no tokens minted (market requests are in-app, not link-token based). |
| V. Env parity & containers | Yes | No new services; notifications/email reuse Mailpit(local)/Resend(Dev/Prod); one migration runs identically per env. |
| VI. Conventions & tooling | Yes | Frontend keeps `.html`/`.css`/`.ts` separate; any scripts are `.ps1`; DESIGN.md drives UI with a per-feature UI review checklist. |

**Gate result**: PASS. No deviations — Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/017-event-marketplace/
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions
├── data-model.md        # Phase 1 — entities, indexes, constraints, migration
├── quickstart.md        # Phase 1 — end-to-end validation guide
├── contracts/
│   └── marketplace-api.md   # Phase 1 — REST endpoint contracts
├── checklists/
│   ├── requirements.md  # (from /speckit-specify)
│   └── ui-review.md     # (instantiate from template during UI work)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── MercenaryListing.cs            # NEW (event + user + positions + pitch)
│   ├── MarketRequest.cs               # NEW (party + user, Application|Invite, status)
│   ├── MarketEnums.cs                 # NEW (MarketRequestDirection, MarketRequestStatus)
│   ├── Party.cs                       # EDIT (+ IsRecruiting, SpotsAdvertised, RecruitBlurb, PositionsNeeded)
│   ├── PartyMember.cs                 # EDIT (+ bool ViaMarket)
│   └── NotificationEnums.cs           # EDIT (+ MarketInvite → InvitesAndRoster)
├── Services/
│   ├── Marketplace/
│   │   ├── IMarketListingService.cs / MarketListingService.cs       # post/edit/take-down/free-agents board/eligibility
│   │   ├── IMarketRecruitingService.cs / MarketRecruitingService.cs # recruiting toggle + parties board side
│   │   ├── IMarketRequestService.cs / MarketRequestService.cs       # apply/invite/accept/decline/revoke + inboxes + dashboard
│   │   └── MarketEligibility.cs        # shared "is user In a party for this event?" helper
│   ├── Parties/PartyService.cs         # EDIT (ProjectAsync InCount incl. guests)
│   ├── Parties/PartyRosterService.cs   # EDIT (In group incl. guests + ViaMarket flag)
│   └── Email/MarketEmailService.cs     # NEW (market-invite email)
├── Controllers/
│   ├── MarketController.cs            # NEW (event-scoped: board, listing, my-inbox, dashboard)
│   └── PartiesController.cs           # EDIT (recruiting toggle/get, applications+invites inbox, invite/accept-on-behalf, direct search)
├── Dtos/Marketplace/*.cs             # NEW DTOs
├── EmailTemplates/market-invite.html  # NEW
├── Data/AppDbContext.cs               # EDIT (DbSets + model config + Party/PartyMember columns)
├── Data/Migrations/*                  # NEW migration: AddEventMarketplace
├── Data/DevDataSeeder.cs             # EDIT (seed listings, a recruiting party, sample requests)
└── tests/JuggerHub.Api.IntegrationTests/Marketplace/*  # NEW

frontend/apps/web/src/app/
├── features/marketplace/
│   ├── market-board/                 # two-sided board (embedded in event-detail)
│   ├── listing-editor/               # post/edit free-agent listing (sheet)
│   ├── apply-sheet/                  # free agent → apply to a party
│   ├── invite-sheet/                 # party admin → invite a mercenary
│   ├── my-market/                    # mercenary inbox (event page section)
│   ├── recruiting-inbox/             # party admin inbox (applications + sent invites)
│   └── direct-invite/                # search any user + invite
├── features/events/event-detail/     # EDIT (embed market-board + my-market below participant-groups)
├── features/parties/party-manage/    # EDIT (recruiting block + link to recruiting inbox)
├── features/dashboard/modules/market-card.component.*  # NEW (dashboard market module)
├── features/alerts/*                 # EDIT (render MarketInvite with inline Accept/Decline)
├── core/services/market.service.ts + core/models/market.models.ts   # NEW
└── app.routes.ts                     # EDIT (recruiting-inbox route under the party path)
```

**Structure Decision**: Web-application layout. The feature is a faithful mirror of the feature-016
parties slice and feature-006 events invitation/search machinery (services behind interfaces, thin
controllers, `PartyGuard` + `PartyCapacity`, `PartyResult`/`PartyOutcome`, notification + email
fan-out), plus surgical edits to the two 016 read paths that must now include guests, the event-detail
page, party-manage, the dashboard, and the alerts renderer.

## Complexity Tracking

No constitution deviations — this section is intentionally empty. Every new abstraction (three
marketplace services, the eligibility helper, the market email service, the market request/listing
entities) has a direct precedent in the events/parties slices, so no added complexity requires
justification.
