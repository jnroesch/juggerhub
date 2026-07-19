# Implementation Plan: Event Parties

**Branch**: `016-event-parties` | **Date**: 2026-07-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/016-event-parties/spec.md`

## Summary

Replace the direct team-join on **teams-only** events with a two-phase **party** flow. A team
admin **forms** a party (a temporary subset of one team, for one event); forming immediately
posts a **participation request** to every team member — a card pinned to the team space plus a
notification and email. Members **accept** (join, taking a roster spot up to a per-event cap) or
**decline** (soft, reversible, visible to the admin). The admin manages the roster (nudge, remove),
posts **party news** (private, deleted on disband), invites **co-admins** (team-scoped, mirroring
events), and — as a deliberate second step — **applies** the party to the event, which creates the
existing feature-006 `EventSignup(TeamId)` and hands off to the event's pending/payment/waitlist
flow unchanged. **Disband** is a guarded manual delete that also withdraws the event entry.

**Technical approach**: A new `Party` aggregate (`Party`, `PartyMember`, `PartyNewsPost`,
`PartyAdminInvitation`) plus a nullable `RosterCap` column on `Event`, added in one EF Core
migration. New services behind interfaces (`PartyService`, `PartyRosterService`, `PartyNewsService`,
`PartyInvitationService`) mirror the events slice one-for-one, reusing the established guards
(`TeamMembershipGuard`), a new `PartyAccess` guard, a `PartyCapacity` helper (pessimistic row lock,
exactly like `EventCapacity`), the notification fan-out (`INotificationService.CreateManyAsync`), and
the email-template pattern. Applying reuses `EventCapacity` to insert the team's `EventSignup`. The
Angular client gains a `parties` feature area (form, manage hub, news, co-admin invites, accept),
plus edits to event-detail join actions, team-detail (pinned request card), the event-create wizard
(roster-cap field), and the alerts renderer (new notification type).

## Technical Context

**Language/Version**: C# / .NET 10 (backend, `backend/`), TypeScript / Angular 21 (frontend Nx
workspace, `frontend/`). Zoneless change detection (no `fakeAsync` in specs — see
`catalogue-014-decisions`).

**Primary Dependencies**: EF Core 10 + Npgsql (PostgreSQL 18), Microsoft Identity, Mapster
(entity→DTO), Asp.Versioning; Angular + Nx + Tailwind, Angular signals/`resource`.

**Storage**: PostgreSQL. New tables `Parties`, `PartyMembers`, `PartyNewsPosts`,
`PartyAdminInvitations`; new nullable column `Events.RosterCap`.

**Testing**: Backend xUnit integration tests (`backend/tests/JuggerHub.Api.IntegrationTests`,
WebApplicationFactory as used by the events/teams suites); Angular specs (zoneless); optional
Playwright e2e (`frontend/apps/web-e2e`).

**Target Platform**: Linux containers (backend + Postgres via docker-compose locally); responsive
web (phone + desktop) per DESIGN.md.

**Project Type**: Web application (separate `backend/` API + `frontend/` SPA).

**Performance Goals**: Standard interactive web latency; all list surfaces paginated; reads use
projections + `AsNoTracking`; capacity mutations serialize on a single row lock (no table scans).

**Constraints**: Never-trust-the-client — every authorization decision server-side; no raw
exceptions/secrets to the client; DTOs out via Mapster; `.html`/`.css`/`.ts` kept separate;
PowerShell-only scripts; environment parity (local/Dev/Prod identical behavior).

**Scale/Scope**: Adds ~4 entities, ~1 migration, ~4 backend services + 2 controllers, and one
Angular feature area (~6 components) plus edits to 3 existing surfaces. No new middleware, no new
infrastructure.

## Constitution Check

*GATE: must pass before Phase 0 and re-checked after Phase 1. Constitution v1.1.0.*

| Principle | Applies | Compliance |
|---|---|---|
| I. Security-first, never trust client | Yes | Every party action (form/join/decline/leave/nudge/remove/apply/withdraw/news/co-admin/disband) authorized server-side via `PartyAccess`/`TeamMembershipGuard`; the event page is public but party surfaces are member-gated (non-members get 404, like teams). Generic errors only; no secrets/stack traces to client. |
| II. Thin controllers, service-centric | Yes | Two thin controllers (`PartiesController`, `PartyInvitationsController`) delegate to DI'd services behind interfaces; no repository layer; entities→DTOs via Mapster. |
| III. Disciplined data access (EF + PG) | Yes | New entities derive from `BaseEntity` (UUIDv7); audit fields via interceptor; reads use `.Select`/`AsNoTracking`; all lists paginate via `PaginationRequest`/`PagedResult<T>`; capacity uses row-lock + atomic write; `ExecuteUpdateAsync` paths set `ModifiedDate`. |
| IV. Secure auth & sessions | Yes | Reuses existing Identity/JWT-in-httpOnly-cookie unchanged; no auth surface added; co-admin tokens are opaque high-entropy with TTL + revoke (mirrors event invites). |
| V. Env parity & containers | Yes | No new services; notifications/email reuse Mailpit(local)/Resend(Dev/Prod); one migration runs identically per env. |
| VI. Conventions & tooling | Yes | Frontend keeps `.html`/`.css`/`.ts` separate; any scripts are `.ps1`; DESIGN.md drives UI with a per-feature UI review checklist. |

**Gate result**: PASS. No deviations — Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/016-event-parties/
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions
├── data-model.md        # Phase 1 — entities, indexes, constraints, migration
├── quickstart.md        # Phase 1 — end-to-end validation guide
├── contracts/
│   └── party-api.md     # Phase 1 — REST endpoint contracts
├── checklists/
│   ├── requirements.md  # (from /speckit-specify)
│   └── ui-review.md     # (instantiate from template during UI work)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
backend/
├── Entities/
│   ├── Party.cs                       # NEW aggregate root (team + event)
│   ├── PartyMember.cs                 # NEW (In/Declined; role Member/Admin)
│   ├── PartyNewsPost.cs               # NEW (private feed)
│   ├── PartyAdminInvitation.cs        # NEW (mirrors EventAdminInvitation, team-scoped)
│   ├── PartyEnums.cs                  # NEW (PartyStatus, PartyMemberStatus, PartyMemberRole)
│   ├── Event.cs                       # EDIT (+ int? RosterCap)
│   └── NotificationEnums.cs           # EDIT (+ PartyRequest→InvitesAndRoster, + PartyNews→TeamNews)
├── Services/
│   ├── Parties/
│   │   ├── IPartyService.cs / PartyService.cs                 # form / apply / withdraw / disband
│   │   ├── IPartyRosterService.cs / PartyRosterService.cs     # join / decline / leave / remove / nudge / list
│   │   ├── IPartyNewsService.cs / PartyNewsService.cs         # private news feed
│   │   ├── IPartyInvitationService.cs / PartyInvitationService.cs  # co-admin link + targeted
│   │   ├── PartyAccess.cs             # resolve caller's party role (mirrors EventAdminGuard)
│   │   └── PartyCapacity.cs           # row-lock + In-count (mirrors EventCapacity)
│   ├── Events/EventSignupService.cs   # EDIT (remove teams-only direct-join branch)
│   └── Email/PartyEmailService.cs     # NEW (request + co-admin invite emails)
├── Controllers/
│   ├── PartiesController.cs           # NEW
│   ├── PartyInvitationsController.cs  # NEW (token accept/preview)
│   ├── EventsController.cs            # EDIT (party-context endpoint; teams signup now 4xx)
│   └── TeamsController.cs             # EDIT (team party-requests endpoint)
├── Dtos/Parties/*.cs                  # NEW DTOs
├── EmailTemplates/party-request.html  # NEW (+ party-coadmin-invite.html, party-news.html)
├── Data/AppDbContext.cs               # EDIT (DbSets + model config)
├── Data/Migrations/*                  # NEW migration: AddParties
├── Data/DevDataSeeder.cs             # EDIT (seed a sample party)
└── tests/JuggerHub.Api.IntegrationTests/Parties/*  # NEW

frontend/apps/web/src/app/
├── features/parties/
│   ├── party-create/                 # "Form a party" (launched from event)
│   ├── party-manage/                 # hub: roster groups + readiness + tools + apply/disband
│   ├── party-news/                   # private feed + composer
│   ├── party-invitations/            # co-admin invite management
│   └── party-invite-accept/          # token accept
├── features/events/event-detail/components/join-actions.component.*   # EDIT (Enter/Manage party)
├── features/events/event-create/event-create.component.*              # EDIT (roster-cap field)
├── features/teams/team-detail/team-detail.component.*                 # EDIT (pinned request card)
├── features/alerts/*                 # EDIT (render PartyRequest notification)
├── core/services/party.service.ts + core/models/party.*.ts           # NEW
└── app.routes.ts                     # EDIT (party routes under /t/:slug/party/:eventId)
```

**Structure Decision**: Web-application layout. The feature is a faithful mirror of the feature-006
events slice (services behind interfaces, thin controllers, guard + capacity helpers, invitation
machinery) scoped to a team, plus targeted edits to the three existing surfaces it touches (event
detail, event create, team detail) and the alerts renderer.

## Complexity Tracking

No constitution deviations — this section is intentionally empty. Every new abstraction (services,
guard, capacity helper, invitation machinery) has a direct precedent in the events/teams slices, so
no added complexity requires justification.
