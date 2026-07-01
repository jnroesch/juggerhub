# Implementation Plan: First-Login Onboarding Flow

**Branch**: `004-onboarding` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-onboarding/spec.md`

## Summary

Guide a newly-verified player through a **one-question-per-screen, mobile-first onboarding wizard** the first time they sign in, then get out of the way permanently. The flow (Welcome → display name\* → city → pompfen → team-stub → photo+bio → Done) **reuses feature 003's profile endpoints** for all persistence — owner update (`PUT /profiles/me`) and avatar upload (`PUT /profiles/me/avatar`) — so no new profile fields or write paths are invented. The **only new persisted state** is a nullable **`OnboardingCompletedAt`** on the existing `PlayerProfile`; it surfaces as an **`OnboardingCompleted` boolean on `AuthUserDto`** (returned by `/auth/login`, `/auth/me`, `/auth/refresh`) so the client knows, right after sign-in, whether to route into `/onboarding` or straight to the app. One new **owner-only, idempotent endpoint** — `POST /profiles/me/onboarding/complete` — sets the timestamp; both finishing and dismissing the flow call it, satisfying "shown once."

The backend change is deliberately tiny and rides the established slice: extend `PlayerProfile` + `AppDbContext` config, add one EF migration (`AddOnboardingCompletedAt`), extend `IProfileService`/`ProfileService` with `CompleteOnboardingAsync` and a lightweight `HasCompletedOnboardingAsync` projection, thread the flag through `AuthService` (login/refresh/me) and the Mapster `AuthUserDto` mapping, and add one thin controller action. The bulk of the work is **frontend**: a full-screen `/onboarding` route **outside the app shell** (like the auth screens), guarded so only a signed-in, not-yet-onboarded user runs it; a single `OnboardingComponent` that manages internal step state (not sub-routes) matching the "Minimal centered" wireframe but styled strictly from `DESIGN.md`; reuse of the existing `PompfeSelectorComponent`/`POMPFEN_CATALOG` and `ProfileService`; a new `ProfileService.completeOnboarding()`; the `onboardingCompleted` field on the `AuthUser` model; and the post-login redirect in `sign-in.component`. Validated desktop + mobile, backend via Testcontainers integration tests, frontend via Jest unit + one Playwright e2e (fresh verified user → onboarding → complete → relogin lands in app).

## Technical Context

**Language/Version**: Backend — C# 13 on .NET 10 (ASP.NET Core, EF Core 10, ASP.NET Core Identity). Frontend — TypeScript on Angular (standalone components) in an Nx workspace.

**Primary Dependencies**:
- Backend (existing, reused): `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Mapster`, `Asp.Versioning.Mvc`. **No new NuGet packages.**
- Frontend: `@angular/*` (router, reactive forms, signals), RxJS, Tailwind; `jest` (unit), `@playwright/test` (e2e). **No new runtime dependency** — reuses `ProfileService`, `PompfeSelectorComponent`, `POMPFEN_CATALOG`, `authGuard`, and the auth session signals.

**Storage**: PostgreSQL 18. **No new tables.** One new nullable column `OnboardingCompletedAt` (`timestamptz`) on the existing `PlayerProfiles` table (+ one EF migration). No index needed (the flag is only ever read for the current authenticated user, keyed by the existing `UserId` unique index).

**Testing**: Backend — xUnit + `WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` (extend the existing integration project): complete-onboarding is owner-only (401 without auth, acts only on the subject) and idempotent (second call is a no-op, timestamp unchanged); `OnboardingCompleted` is `false` on a fresh account and `true` after completion on both `/auth/me` and `/auth/login`; the migration applies cleanly. Frontend — Jest unit (onboarding step-machine logic: required display name gates Continue, skip advances without writing, Back preserves values, team selection is never persisted) + Playwright e2e (register → verify → first login redirects to `/onboarding`; complete the flow; sign out and back in lands on the dashboard, not onboarding). All tests run in containers.

**Target Platform**: Linux containers via Docker (`docker-compose`). Product UI targets desktop + mobile (responsive web).

**Project Type**: Web application — existing sibling `backend/` (.NET) and `frontend/` (Nx/Angular) trees.

**Performance Goals**: No throughput targets. The onboarding flag is a single projected boolean read alongside the existing user lookup (`HasCompletedOnboardingAsync` uses `.Select` + `AsNoTracking`); completion is a single-row tracked update. No list endpoints are added, so pagination rules are not engaged by new code.

**Constraints**: Security-first / OWASP / never-trust-the-client — completion marking and every profile write are authorized server-side against the authenticated subject (never a client-supplied id); the immutable handle is never editable in the flow; the client `onboardingCompleted` flag is UX only and never the authority. Generic `ProblemDetails` via existing middleware; no stack traces/secrets to client or logs. Environment parity (no new services/secrets; migration auto-applies local/Dev/Prod). `.ps1`-only scripts; Docker-only workflow; Angular keeps separate `.html`/`.css`/`.ts`; styled from `DESIGN.md` (the hand-drawn wireframe is layout guidance only); responsive at multiple viewports.

**Scale/Scope**: One new column, one migration, one new endpoint, two new service methods, one DTO field (+ its Mapster mapping and the three auth paths that emit it). Frontend: one new full-screen route + `OnboardingComponent` (with per-step sub-templates or inline step blocks), one new guard, one `ProfileService` method, one model-field addition, and the `sign-in` redirect. Reuses the 003 profile write path and pompfe selector wholesale.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | How this plan complies | Verdict |
|---|-----------|------------------------|---------|
| I | Security-First, Never Trust the Client | Completion marking is authorized server-side via the authenticated subject (`TryGetUserId` from the JWT `sub`, never a client id); profile writes reuse 003's owner-only endpoints. The client `onboardingCompleted` flag is a routing convenience only — the server is the authority (SC-008), and the API still enforces auth on every call regardless of client state. The immutable handle has no edit path in the flow (FR-023). Errors return generic `ProblemDetails` via existing middleware; no secrets/stack traces leak. OWASP reviewed (A01 broken access control — owner-scoped complete + reused owner writes; A03 injection — EF parameterization; A04 insecure design — idempotent completion, no team persistence to spoof; A05 misconfiguration — `/onboarding` is authenticated, redirect is UX not a boundary). | ✅ |
| II | Thin Controllers, Service-Centric | The new `POST /me/onboarding/complete` action does only auth-subject extraction + status→HTTP shaping, delegating to `IProfileService.CompleteOnboardingAsync`. The onboarding flag is produced by the service (`HasCompletedOnboardingAsync`) and mapped into `AuthUserDto` via Mapster; controllers never see entities. DI behind interfaces; **no repository layer**. | ✅ |
| III | Disciplined Data Access (EF Core + PostgreSQL) | `OnboardingCompletedAt` lives on the existing `PlayerProfile : BaseEntity` (UUIDv7 + audit interceptor already applied). `HasCompletedOnboardingAsync` uses `.Select`/projection + `AsNoTracking`. `CompleteOnboardingAsync` performs a single tracked read+set+save so the `AuditFieldsInterceptor` updates `ModifiedDate` automatically (no `ExecuteUpdate`, so no manual audit set needed). No new list endpoint → pagination rules not triggered. | ✅ |
| IV | Secure Authentication & Session Management | Reuses Identity + JWT-in-httpOnly-cookie unchanged. The flag is additively surfaced on the existing `AuthUserDto` emitted by login/refresh/me; it discloses nothing sensitive (only whether this signed-in user finished onboarding) and is not an enumeration oracle (only ever returned to the authenticated subject). Onboarding requires a verified, signed-in user — the 002 verify-before-login gate still applies. | ✅ |
| V | Environment Parity & Containerized Deployments | Same `docker-compose` stack; **no new services or secrets**. The single additive migration auto-applies on startup across local/Dev/Prod; existing local/dev profiles get `NULL` (treated as not-onboarded) — acceptable, no prod users. Per-service Dockerfiles unchanged. | ✅ |
| VI | Consistent Conventions & Tooling | Angular `OnboardingComponent` (+ any step sub-components) keep separate `.html`/`.css`/`.ts`; Tailwind styled from `DESIGN.md` tokens; the wireframe informs layout only. Any scripts added are `.ps1`. Frontend session state stays in signals; tokens remain in httpOnly cookies. | ✅ |
| — | Secret & Configuration Management | No new secrets or config. | ✅ |

**Result**: PASS — no violations; Complexity Tracking left empty. The feature is intentionally additive over 003/002 and introduces no new architecture.

## Project Structure

### Documentation (this feature)

```text
specs/004-onboarding/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — resolved decisions (trigger point, completion state, flag propagation, team stub, skip semantics)
├── data-model.md        # Phase 1 — the OnboardingCompletedAt addition, DTO deltas, state transition
├── quickstart.md        # Phase 1 — runnable end-to-end validation guide
├── contracts/
│   ├── openapi.yaml      #   POST /profiles/me/onboarding/complete + AuthUserDto delta (onboardingCompleted)
│   └── README.md
└── checklists/
    └── requirements.md  # Spec quality checklist (from /speckit-specify)
```

### Source Code (repository root)

```text
backend/                                         # .NET 10 solution (namespace JuggerHub)
├── Controllers/
│   └── ProfilesController.cs                     # EXTEND — NEW owner action POST me/onboarding/complete (thin; 204/404/401)
├── Services/
│   ├── Profile/
│   │   ├── IProfileService.cs                    # EXTEND — CompleteOnboardingAsync + HasCompletedOnboardingAsync
│   │   ├── ProfileService.cs                     # EXTEND — implement both (tracked set for complete; projection for flag)
│   │   └── ProfileMapping.cs                     # (unchanged — OwnerProfileDto keeps its shape)
│   └── Auth/
│       ├── AuthService.cs                        # EXTEND — build AuthUserDto with the onboarding flag in Login/Refresh/GetUser(me)
│       └── AuthMappingRegister.cs                # EXTEND — map User → AuthUserDto (flag filled via `with`, not Mapster nav)
├── Entities/
│   └── PlayerProfile.cs                          # EXTEND — nullable DateTime? OnboardingCompletedAt
├── Dtos/
│   └── Auth/AuthResponses.cs                     # EXTEND — AuthUserDto gains bool OnboardingCompleted
├── Data/
│   ├── AppDbContext.cs                           # (no config change required for a plain nullable column; verify)
│   └── Migrations/                               # NEW migration: AddOnboardingCompletedAt
└── tests/JuggerHub.Api.IntegrationTests/
    └── Onboarding/                               # NEW — complete owner-only + idempotent; flag on /me and /login; migration applies

frontend/apps/web/src/app/
├── core/
│   ├── models/auth.models.ts                     # EXTEND — AuthUser gains onboardingCompleted: boolean
│   ├── services/profile.service.ts               # EXTEND — completeOnboarding(): Observable<void>
│   └── guards/onboarding.guard.ts                # NEW — signed-in + not-yet-onboarded (else redirect to dashboard)
├── features/
│   ├── auth/sign-in/sign-in.component.ts         # EXTEND — post-login: redirect to /onboarding when !onboardingCompleted, else /
│   └── onboarding/
│       ├── onboarding.component.{ts,html,css}    # NEW — full-screen step-machine (Welcome…Done), reuses PompfeSelector + ProfileService
│       └── components/                            # NEW (optional) — small step presentational components if the template grows
├── shared/pompfen.catalog.ts                     # (reused unchanged)
├── features/profile/components/pompfe-selector/   # (reused unchanged)
└── app.routes.ts                                 # EXTEND — /onboarding (full-screen, outside shell, [authGuard, onboardingGuard])

frontend/apps/web-e2e/src/
└── onboarding.spec.ts                            # NEW — register→verify→first login→onboarding→complete→relogin lands in app
```

**Structure Decision**: Web application extending the existing `backend/` and `frontend/` trees. The onboarding route is a full-screen page **outside** the shell (like the auth and public-profile screens) and behind the auth guard; a second `onboardingGuard` bounces already-onboarded users to the dashboard so the flow is genuinely one-time. Backend changes are additive within the existing `Services/Profile` and `Services/Auth` groupings — no new project, service group, table, or endpoint family beyond the single completion action. The `OnboardingComponent` owns its step state internally (a small state machine over an enum of steps) rather than nested routes, matching the wireframe's single-surface, back/forward feel and keeping entered values in memory across Back without round-trips.

## Complexity Tracking

> No constitution violations. No entries required.
