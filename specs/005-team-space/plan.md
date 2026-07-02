# Implementation Plan: Team Space & Member Handling

**Branch**: `005-team-space` | **Date**: 2026-07-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-team-space/spec.md`

## Summary

Give every player **teams**: any signed-in user creates a team (free, non-unique **name** + a unique, immutable, creator-chosen **slug** at `/t/<slug>`, and a type — **city team** with a city or **Mixteam** with none), becoming its first **admin**. A team page presents a cover header and tabs — **Members** (roster with role tags + profile positions), **Activity** (events the team played, reusing the 003 Events model), **News** (read-only feed) — with **Trainings** disabled. Admins grow the roster via a single reusable **invite link** (7-day expiry, revocable/rotatable) or **targeted invites** delivered by **email** (searchable backup); invitees open a token URL, see a **public** preview, and **accept & join as a member** or decline. Admins **promote/demote** roles and **remove** members under a hard **last-admin guard** (a team can never reach zero admins), and can **delete** the team (irreversible; cascades roster/invites/news while **preserving** historical event participations).

The approach adds a new domain slice under `Services/Teams/` following the established **thin-controller / DI-service / EF-Core-directly / Mapster-DTO / `BaseEntity`(UUIDv7)** conventions, mirroring the `Services/Profile` + `Services/Events` layout. Four new entities — **`Team`**, **`TeamMembership`**, **`TeamInvitation`**, **`TeamNewsPost`** — plus a nullable **`TeamId`** on the existing `EventParticipation` (realizing the `TeamLabel`→`TeamId` migration its own comment anticipated, `OnDelete(SetNull)` to preserve history). The team **slug** reuses the profile-handle model via a parallel `TeamSlugPolicy` (format/normalize/reserved-words) with a unique index; **names are not unique**. Authorization is strictly server-side: internal reads (roster/news/activity/invitations) require an authenticated **member** (non-members get 404 — no oracle), mutations require the **admin** role, and public info (name/type/city/count) + the invite preview are the only anonymous surfaces. Targeted invites reuse the existing `IEmailSender`/`IEmailTemplateService` pipeline (Mailpit/Resend) with a new `team-invite` template — **no new infrastructure**. The frontend adds a `TeamService`, a create screen, the tabbed team page (members-only), invitations + settings screens inside the shell, and a full-screen anonymous `/join/:slug/:token` accept page — all styled from `DESIGN.md` and validated desktop + mobile.

## Technical Context

**Language/Version**: Backend — C# 13 on .NET 10 (ASP.NET Core, EF Core 10, ASP.NET Core Identity). Frontend — TypeScript on Angular (standalone components) in an Nx workspace.

**Primary Dependencies**:
- Backend (existing, reused): `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Mapster`, `Asp.Versioning.Mvc`, the existing email pipeline (`IEmailSender`, `IEmailTemplateService`). **No new NuGet packages** — invite tokens use `RandomNumberGenerator`; the last-admin guard uses an EF transaction.
- Frontend: `@angular/*` (router, reactive forms, signals), RxJS, Tailwind; `jest` (unit), `@playwright/test` (e2e). **No new runtime dependency.**

**Storage**: PostgreSQL 18. New tables: `Teams`, `TeamMemberships`, `TeamInvitations`, `TeamNewsPosts`; altered `EventParticipations` (+ nullable `TeamId`, `OnDelete SetNull`). One EF migration `AddTeams` (auto-applies on startup).

**Testing**: Backend — xUnit + `WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` (extend the existing integration project): create + slug uniqueness/immutability + city rule; member-only reads (non-member → 404); admin-only mutations; **last-admin guard incl. the concurrent demote-each-other race**; invite link rotate/revoke + 7-day expiry; targeted-invite email + duplicate/already-member; accept/decline + already-member idempotency; delete cascade + participation `SetNull`; pagination. Frontend — Jest unit (`TeamService`, slug validator, role/last-admin gating) + Playwright `teams.spec.ts` (create → invite → accept as 2nd user → manage roles/last-admin → delete). All in containers.

**Target Platform**: Linux containers via Docker (`docker-compose`). Product UI targets desktop + mobile (responsive web).

**Project Type**: Web application — existing sibling `backend/` (.NET) and `frontend/` (Nx/Angular) trees.

**Performance Goals**: No throughput targets. All list reads use projections + `AsNoTracking` + `PagedResult<T>`; roster/activity/news/invitation/user-search queries hit `TeamId`/composite indexes; slug + token lookups hit unique indexes. The last-admin guard is a short `RepeatableRead`/`Serializable` transaction (rare retries acceptable).

**Constraints**: Security-first / OWASP / never-trust-the-client — **all** authorization server-side (membership for internal reads, admin role for mutations); public/preview endpoints return strictly public fields (stripped at the DTO/query boundary). Non-member access to an internal team surface is indistinguishable from an unknown team (both 404 — no membership oracle). Slug immutable + validated (format/reserved/uniqueness, race-safe). Invite tokens are unguessable capabilities with a 7-day expiry + revoke. Environment parity (email reuses Mailpit/Resend — no new infra/secrets). `.ps1`-only scripts; Docker-only workflow; responsive UI at multiple viewports; `.html`/`.css`/`.ts` kept separate.

**Scale/Scope**: ~18 endpoints across teams / members / invitations / accept; four new entities + four enums + one FK addition; one migration; ~6 new frontend screens (create, team page w/ 3 tabs, invitations, invite-people, settings, accept) + `TeamService` + `TeamSlugPolicy` + `TeamEmailService` + one email template. Extends `DevDataSeeder`. Sized so a later Trainings/polls/news-composer/public-page iteration consumes these foundations without reshaping them.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | How this plan complies | Verdict |
|---|-----------|------------------------|---------|
| I | Security-First, Never Trust the Client | Every mutation (create/invite/revoke/role/remove/delete/post) and every internal read (roster/news/activity/invitations) is authorized server-side from the authenticated subject's **membership + role** — never a client flag or client-supplied id. Non-member reads return **404** (no membership oracle). Public/preview endpoints return a **`TeamPublicDto`/`InvitePreviewDto`** carrying only name/type/city/count(+inviter) — no roster/news, stripped at the query boundary. Last-admin guard enforced atomically (409). Invite tokens unguessable + expiring + revocable. Generic `ProblemDetails`; no stack traces/secrets to client or logs. OWASP reviewed (A01 broken access control — the entire feature is authz; A03 injection — EF parameterization; A04 insecure design — capability tokens, last-admin invariant, capped lists; A08 integrity — server-derived roles). | ✅ |
| II | Thin Controllers, Service-Centric | `TeamsController` + `InvitationsController` do HTTP shaping + model validation only, delegating to `ITeamService`, `ITeamInvitationService`, `ITeamActivityService`, `ITeamNewsService`; DI behind interfaces; **no repository layer** (EF Core directly); responses are DTOs mapped with Mapster. Service methods return typed result enums/records (e.g. `LastAdmin`, `SlugTaken`, `Expired`) the controllers map to HTTP — mirroring `IProfileService`. | ✅ |
| III | Disciplined Data Access (EF Core + PostgreSQL) | New entities derive from `BaseEntity` (UUIDv7 + audit interceptor). Reads use `.Select`/`ProjectToType` + `AsNoTracking`; **every** list (roster, activity, news, invitations, user-search) is paginated via `PaginationRequest`/`PagedResult<T>`. Unique indexes: `Team.Slug`, `TeamInvitation.Token`, `(TeamId,UserId)`; partial-unique active-link + pending-targeted. `EventParticipation.TeamId` uses `OnDelete(SetNull)`. Team create + creator-admin membership use explicit `DbSet.Add` in a transaction (client-GUID nav-insert gotcha). Any `ExecuteUpdate` path would set `ModifiedDate`. | ✅ |
| IV | Secure Authentication & Session Management | Reuses Identity + JWT-in-httpOnly-cookie unchanged. Internal/admin endpoints require the JWT bearer scheme; accept/decline require auth and use the existing return-URL so an unauthenticated invitee signs in/registers and lands back on the invite. Slug availability is intentionally authenticated UX (creator is signed in); the invite preview is anonymous by design (public info only). No password/identity surface changes. | ✅ |
| V | Environment Parity & Containerized Deployments | Same `docker-compose` stack; **no new services or secrets** — targeted-invite email reuses the existing Mailpit(local)/Resend(Dev-Prod) sender + template service (one new template). Migration auto-applies on startup everywhere; per-service Dockerfiles unchanged. CI/CD + Terraform remain deferred (allowed by scope). | ✅ |
| VI | Consistent Conventions & Tooling | Angular components keep separate `.html`/`.css`/`.ts`; any scripts added are `.ps1` only; Tailwind styled from `DESIGN.md` tokens; the offline team wireframes inform layout only. Enums serialize as names via the existing global `JsonStringEnumConverter`. | ✅ |
| — | Secret & Configuration Management | No new secrets. Optional `Teams` options (invite-link TTL days, slug length bounds, news body cap) are plain config with safe defaults; no Key Vault. | ✅ |

**Result**: PASS — no violations; Complexity Tracking left empty. Storing invite-link tokens **raw** (so the active link is re-displayable to admins) is a deliberate capability-URL choice (research §4), bounded by high entropy + 7-day expiry + revoke — not a constitution deviation.

## Project Structure

### Documentation (this feature)

```text
specs/005-team-space/
├── plan.md              # This file
├── spec.md              # Feature specification (clarified)
├── research.md          # Phase 0 — slug policy, membership key, last-admin guard, invite/token model, email, activity FK, visibility, delete
├── data-model.md        # Phase 1 — entities, enums, relationships, DTOs, validation, migration, seeding
├── quickstart.md        # Phase 1 — runnable end-to-end validation guide (Scenarios A–G)
├── contracts/
│   ├── openapi.yaml      #   /api/v1/teams/*, /api/v1/invitations/* surface
│   └── README.md
└── checklists/
    └── requirements.md  # Spec quality checklist (from /speckit-specify + /speckit-clarify)
```

### Source Code (repository root)

```text
backend/                                            # .NET 10 solution (namespace JuggerHub)
├── Controllers/
│   ├── TeamsController.cs                           # NEW — thin: create, slug-available, {slug} (member), public,
│   │                                                #   delete (admin), members(+role/remove), activity, news,
│   │                                                #   invitations(+link/targeted/revoke/user-search)   (api/v1/teams/*)
│   └── InvitationsController.cs                     # NEW — token preview (anon), accept, decline           (api/v1/invitations/*)
├── Services/
│   ├── Teams/
│   │   ├── ITeamService.cs / TeamService.cs         # NEW — create (creator=admin), get detail (member), public, delete,
│   │   │                                            #   roster (paged), role change + remove + step-down (last-admin guard)
│   │   ├── ITeamInvitationService.cs / ….cs         # NEW — link get/rotate/revoke, targeted create (email), list,
│   │   │                                            #   user-search, preview, accept, decline
│   │   ├── ITeamActivityService.cs / ….cs           # NEW — team events (paged) by TeamId (reuses ActivityItemDto)
│   │   ├── ITeamNewsService.cs / ….cs               # NEW — read-only news feed (paged, author-role join)
│   │   ├── TeamSlugPolicy.cs                         # NEW — slug format/normalize/validate + team reserved-word set (mirrors HandlePolicy)
│   │   ├── TeamMembershipGuard.cs                    # NEW — membership/role resolution + transactional last-admin guard
│   │   └── TeamMapping.cs                            # NEW — Mapster config: entities → Team*/Invitation*/Member DTOs
│   ├── Email/
│   │   └── TeamEmailService.cs                       # NEW — renders team-invite template, builds /join/{slug}/{token}, sends via IEmailSender
│   ├── Profile/, Events/, Auth/, Security/, EmailTemplateService/  # EXISTING — HandlePolicy referenced as the slug-policy model
├── Entities/
│   ├── Team.cs                                       # NEW : BaseEntity — Slug (unique, immutable), Name, Type, City?
│   ├── TeamMembership.cs                             # NEW : BaseEntity — TeamId, UserId, Role, JoinedDate
│   ├── TeamInvitation.cs                             # NEW : BaseEntity — TeamId, Kind, Token, Status, ExpiresDate, CreatedByUserId, TargetUserId?
│   ├── TeamNewsPost.cs                               # NEW : BaseEntity — TeamId, AuthorUserId, Body
│   ├── EventParticipation.cs                         # EXTEND — add nullable TeamId (FK → Team, SetNull); TeamLabel kept as display snapshot
│   ├── User.cs                                       # (unchanged; reached via nav for display)
│   └── TeamEnums.cs                                  # NEW — TeamType, TeamRole, InvitationKind, InvitationStatus
├── Data/
│   ├── AppDbContext.cs                               # EXTEND — DbSets + config (unique Slug/Token, (TeamId,UserId), partial-unique link/targeted, EP.TeamId)
│   ├── Migrations/                                   # NEW migration: AddTeams
│   └── DevDataSeeder.cs                              # EXTEND — demo teams, memberships, news, participation TeamId backfill (Dev only)
├── Dtos/
│   └── Teams/                                        # NEW — CreateTeamRequest, SetMemberRoleRequest, CreateTargetedInviteRequest,
│                                                     #   TeamDetailDto, TeamPublicDto, TeamMemberDto, TeamNewsDto, TeamInvitationDto,
│                                                     #   InviteLinkDto, InvitableUserDto, InvitePreviewDto, AcceptInviteResultDto, SlugAvailabilityDto
├── Common/
│   └── TeamOptions.cs                                # NEW — InviteLinkTtlDays (7), slug min/max, news body cap (config, safe defaults)
├── EmailTemplates/
│   └── team-invite.html                             # NEW — invite email (extends existing base header/footer)
├── Program.cs                                        # EXTEND — register ITeamService/ITeamInvitationService/ITeamActivityService/ITeamNewsService/TeamEmailService; bind TeamOptions
└── tests/JuggerHub.Api.IntegrationTests/
    └── Teams/                                        # NEW — create/slug/city, member-only 404, admin-only, last-admin guard (+race),
                                                      #   invite link/targeted/expiry/revoke, accept/decline, delete cascade + SetNull, pagination

frontend/apps/web/src/app/
├── core/
│   ├── services/team.service.ts                     # NEW — teams/members/invitations/accept calls (signals)
│   └── models/team.models.ts                        # NEW — Team*/Member/Invitation/InvitePreview/SlugAvailability DTOs + enums
├── features/teams/
│   ├── team-create/           { *.ts/.html/.css }   # NEW — name + slug(+availability) + type toggle + city (in shell, authGuard)
│   ├── team-detail/           { *.ts/.html/.css }   # NEW — cover header + tabs (Members/Activity/News, Trainings disabled) at /t/:slug
│   │   └── components/roster, activity-list, news-feed, member-menu { *.ts/.html/.css }  # NEW — tab bodies + "…" role/remove sheet
│   ├── team-invitations/      { *.ts/.html/.css }   # NEW — pending list, copy/rotate/revoke link
│   ├── team-invite-people/    { *.ts/.html/.css }   # NEW — copy link + user search → invite
│   ├── team-settings/         { *.ts/.html/.css }   # NEW — step down + danger-zone delete
│   └── invite-accept/         { *.ts/.html/.css }   # NEW — full-screen anonymous accept/decline at /join/:slug/:token (+expired state)
├── layout/ (sidebar, top-nav, dashboard)            # EXTEND — "Teams" / "Create team" entry points
├── shared/pompfen.catalog.ts                        # EXISTING — reused for roster position labels
└── app.routes.ts                                    # EXTEND — /teams/new, /t/:slug(+/invitations,/settings) in shell; /join/:slug/:token outside shell

frontend/apps/web-e2e/src/
└── teams.spec.ts                                    # NEW — create → invite → accept (2nd user) → roles/last-admin → delete, desktop + mobile

backend/appsettings*.json / docker-compose.yml       # EXTEND (optional) — Teams section (invite TTL, slug bounds) with safe defaults
```

**Structure Decision**: Web application extending the existing `backend/` and `frontend/` trees. Backend stays organized by technical type with new logic grouped under `Services/Teams/` (mirroring `Services/Profile` + `Services/Events`), and the invite email under `Services/Email/` next to `AuthEmailService`. Two controllers split the surface by resource: team-scoped admin/member routes on `TeamsController`, token-addressed invitee routes on `InvitationsController`. The team page + management screens live **inside** the shell behind `authGuard` (membership enforced server-side); the invite accept page is a full-screen **anonymous** route **outside** the shell (like `/u/:handle`), requiring auth only to act. No new project or library is introduced.

## Complexity Tracking

> No constitution violations. No entries required.
