# Implementation Plan: Player Profile & Public Share Link

**Branch**: `003-profile` | **Date**: 2026-07-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-profile/spec.md`

## Summary

Give every account a **player profile** with a **public, unauthenticated share page** at a short PayPal.me-style URL (`/u/<handle>`). Registration is extended so the user claims a **unique, immutable, URL-safe handle** at signup; the handle addresses the profile and is shown as `@handle`. Owners edit display name, profile picture, short description, hometown, and their favorite **pompfen** (multi-select over the fixed catalog — Stab, Langpompfe, Schild, Q-Tip, Kette, Doppel-Kurz — plus the Läufer position). The public page shows only non-sensitive fields (never email/account data) plus **recent activity** derived from a **minimal real Events + participation model**. Teams and Badges appear as **UI-only stub sections** (no backing model this round).

The approach extends 002's `AuthController.Register` (add `Handle`, plus an anonymous availability check) and introduces a new domain slice grouped under `Services/Profile/` and `Services/Events/`, following the established thin-controller / DI-service / EF-Core-directly / Mapster-DTO / `BaseEntity`(UUIDv7) conventions. A new **`PlayerProfile : BaseEntity`** (1:1 with `User`) holds the handle (unique index) and profile fields; **`ProfilePompfe`** holds selections over a fixed `Pompfe` enum; **`Event : BaseEntity`** and **`EventParticipation : BaseEntity`** back activity (participation carries a lightweight team label until a real teams feature lands). The **avatar is stored as `bytea`** in a dedicated `ProfileAvatar` row (separate from the projected profile row) and served by a backend endpoint — a deliberate MVP choice that preserves local/Dev/Prod parity with **no new infrastructure**, with a documented migration path to object storage. A new anonymous `ProfilesController` serves the public profile via a **public DTO that strips sensitive fields at the response boundary** (not merely hides them in the UI). The frontend adds a handle field + live availability to the register screen, an owner profile view/edit inside the shell, and a full-screen anonymous `/u/:handle` public page — all styled from `DESIGN.md` and validated desktop + mobile.

## Technical Context

**Language/Version**: Backend — C# 13 on .NET 10 (ASP.NET Core, EF Core 10, ASP.NET Core Identity). Frontend — TypeScript on Angular (standalone components) in an Nx workspace.

**Primary Dependencies**:
- Backend (existing, reused): `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Mapster`, `Asp.Versioning.Mvc`. **No new NuGet packages** — image validation uses framework APIs; avatar bytes persist via EF/Npgsql `bytea`.
- Frontend: `@angular/*` (router, reactive forms), RxJS, Tailwind; `jest` (unit), `@playwright/test` (e2e). **No new runtime dependency.**

**Storage**: PostgreSQL 18. New tables: `PlayerProfiles`, `ProfilePompfen`, `ProfileAvatars`, `Events`, `EventParticipations` (+ one EF migration). Avatar binary stored as `bytea` in `ProfileAvatars` (1:1 optional with profile).

**Testing**: Backend — xUnit + `WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` (extend the existing integration project): register-with-handle (valid/duplicate/malformed/reserved), handle immutability, owner-only edit authorization, public DTO omits email/account data, avatar upload validation, activity ordering + pagination/cap. Frontend — Jest unit (ProfileService, handle-availability validator, pompfe selector) + Playwright e2e (register-with-handle → edit profile → open `/u/<handle>` signed-out → assert no email present). All tests run in containers.

**Target Platform**: Linux containers via Docker (`docker-compose`). Product UI targets desktop + mobile (responsive web).

**Project Type**: Web application — existing sibling `backend/` (.NET) and `frontend/` (Nx/Angular) trees.

**Performance Goals**: No throughput targets. Profile reads use projections + `AsNoTracking`; public profile is a single projected query plus a capped activity query; avatar served from an indexed lookup. Handle lookups hit a unique index.

**Constraints**: Security-first / OWASP / never-trust-the-client; all authorization enforced server-side (owner-only edits; public read is intentionally anonymous). Public responses carry strictly the public field set (sensitive fields stripped at the DTO boundary). No stack traces/secrets to client or logs. Handle is immutable and validated server-side against format + reserved-word + uniqueness rules (race-safe via unique index). Environment parity (avatar storage identical local/Dev/Prod — no object-storage dependency introduced). `.ps1`-only scripts; Docker-only workflow; responsive UI at multiple viewports.

**Scale/Scope**: One extended endpoint (register) + ~7 new endpoints (handle-availability, owner get/update, avatar put/get, public get, public activity), five new entities, one enum catalog, ~3 new frontend screens (register extension, owner profile view/edit, public page) + a ProfileService and a pompfe catalog constant. Sized so a later real Events/Teams feature consumes these foundations without reshaping them.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | How this plan complies | Verdict |
|---|-----------|------------------------|---------|
| I | Security-First, Never Trust the Client | Owner-only edits authorized server-side via the authenticated subject (never a client-supplied id); public read is deliberately anonymous and returns a **PublicProfileDto** that never carries email/account/security fields (stripped at the boundary). Handle format/reserved/uniqueness enforced server-side; live availability is UX only. Generic `ProblemDetails` via existing middleware; no secrets/stack traces to client or logs. Avatar upload validated server-side (content-type + magic-byte sniff + size cap) to prevent malicious/oversized uploads. OWASP reviewed (A01 broken access control — owner checks; A03 injection — EF parameterization; A04 insecure design — immutable handle, capped lists; A05 misconfiguration — anonymous scope explicit). | ✅ |
| II | Thin Controllers, Service-Centric | `ProfilesController` + extended `AuthController` do HTTP shaping + model validation only, delegating to `IProfileService` / `IEventActivityService` (and existing `IAuthService`); DI behind interfaces; **no repository layer** (EF Core directly); responses are DTOs mapped with Mapster. | ✅ |
| III | Disciplined Data Access (EF Core + PostgreSQL) | New entities derive from `BaseEntity` (UUIDv7 + audit interceptor); reads use `.Select`/`ProjectToType` + `AsNoTracking`; activity list is **paginated (`PaginationRequest`/`PagedResult<T>`) and capped** — no unbounded collection; avatar bytes live in a separate row so profile projections stay lean. Any bulk update sets `ModifiedDate` explicitly. | ✅ |
| IV | Secure Authentication & Session Management | Reuses Identity + JWT-in-httpOnly-cookie session unchanged. Registration extension is additive (handle) and preserves enumeration-neutrality for **email**; handle availability is intentionally public (handles are public identifiers by design), so revealing "handle taken" is not an account oracle. Owner endpoints require the JWT bearer scheme; public endpoints are explicitly `[AllowAnonymous]`. | ✅ |
| V | Environment Parity & Containerized Deployments | Same `docker-compose` stack; **no new services or secrets** — avatar stored in Postgres `bytea`, identical across local/Dev/Prod; migrations auto-apply on startup everywhere; per-service Dockerfiles unchanged. CI/CD + Terraform remain deferred (allowed by scope). | ✅ |
| VI | Consistent Conventions & Tooling | Angular components keep separate `.html`/`.css`/`.ts`; any scripts added are `.ps1` only; Tailwind styled from `DESIGN.md` tokens; wireframes in `profile-demo/` inform layout only. | ✅ |
| — | Secret & Configuration Management | No new secrets. Optional `Profile:MaxAvatarBytes` / handle-length bounds are plain config with safe defaults; no Key Vault. | ✅ |

**Result**: PASS — no violations; Complexity Tracking left empty. The avatar-in-Postgres decision is a deliberate parity-first MVP choice (research §4), not a constitution deviation; object storage is the documented future path.

## Project Structure

### Documentation (this feature)

```text
specs/003-profile/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 — resolved decisions (handle format, avatar storage, pompfe catalog, activity shape, registration neutrality)
├── data-model.md        # Phase 1 — entities, fields, relationships, validation, DTOs
├── quickstart.md        # Phase 1 — runnable end-to-end validation guide
├── contracts/
│   ├── openapi.yaml      #   /api/v1/auth/register (extended), /auth/handle-available, /api/v1/profiles/* surface
│   └── README.md
└── checklists/
    └── requirements.md  # Spec quality checklist (from /speckit-specify)
```

### Source Code (repository root)

```text
backend/                                         # .NET 10 solution (namespace JuggerHub)
├── Controllers/
│   ├── AuthController.cs                         # EXTEND — Register takes Handle; NEW GET handle-available (anonymous)
│   └── ProfilesController.cs                     # NEW — thin: me (get/update), me/avatar (put/get),
│                                                 #   {handle} (public get), {handle}/avatar (public),
│                                                 #   {handle}/activity (public, paginated)   (api/v1/profiles/*)
├── Services/
│   ├── Profile/
│   │   ├── IProfileService.cs                    # NEW — create-at-registration, get owner, update, avatar set/get, get public
│   │   ├── ProfileService.cs                     # NEW
│   │   ├── HandlePolicy.cs                       # NEW — format normalize/validate + reserved-word set
│   │   └── ProfileMapping.cs                     # NEW — Mapster config: entity → OwnerProfileDto / PublicProfileDto
│   ├── Events/
│   │   ├── IEventActivityService.cs              # NEW — recent activity (paged) for a profile
│   │   └── EventActivityService.cs               # NEW
│   ├── Auth/                                     # EXISTING — AuthService.RegisterAsync EXTENDED to create the profile atomically
│   └── Security/, Email/, EmailTemplateService/  # EXISTING — unchanged
├── Entities/
│   ├── User.cs                                   # EXISTING — gains a 1:1 nav to PlayerProfile (no scalar profile fields on Identity)
│   ├── PlayerProfile.cs                          # NEW : BaseEntity — UserId, Handle, DisplayName, Hometown, Description
│   ├── ProfilePompfe.cs                          # NEW : BaseEntity — ProfileId, Pompfe (enum)
│   ├── ProfileAvatar.cs                          # NEW : BaseEntity — ProfileId, ContentType, Bytes (bytea)
│   ├── Event.cs                                  # NEW : BaseEntity — Name, Date, Location
│   ├── EventParticipation.cs                     # NEW : BaseEntity — ProfileId, EventId, TeamLabel
│   └── Pompfe.cs                                 # NEW — enum catalog (Stab, Langpompfe, Schild, QTip, Kette, DoppelKurz, Laeufer)
├── Data/
│   ├── AppDbContext.cs                           # EXTEND — DbSets + config (unique Handle index, FKs, indexes)
│   └── Migrations/                               # NEW migration: AddProfilesAndEvents
├── Dtos/
│   └── Profile/                                  # NEW — RegisterRequest handle add; UpdateProfileRequest;
│                                                 #   OwnerProfileDto, PublicProfileDto, ActivityItemDto, HandleAvailabilityDto
├── Common/
│   └── ProfileOptions.cs                         # NEW — MaxAvatarBytes, handle min/max length (bound from config, safe defaults)
├── Program.cs                                    # EXTEND — register IProfileService/IEventActivityService; bind ProfileOptions
└── tests/JuggerHub.Api.IntegrationTests/
    └── Profile/                                  # NEW — Register+handle, immutability, owner-only edit, public-DTO-omits-email,
                                                  #   avatar validation, activity ordering/pagination

frontend/apps/web/src/app/
├── core/
│   ├── services/profile.service.ts               # NEW — owner get/update, avatar upload, public get, activity (signals)
│   └── models/profile.models.ts                  # NEW — Owner/Public profile, UpdateProfile, ActivityItem, HandleAvailability
├── shared/
│   └── pompfen.catalog.ts                        # NEW — canonical pompfen + Läufer (labels DE/EN, order)
├── features/
│   ├── auth/register/        { *.ts/.html/.css }  # EXTEND — handle field + live availability + format hint
│   ├── profile/
│   │   ├── profile-owner/    { *.ts/.html/.css }  # NEW — owner view + edit (in shell, authGuard)
│   │   ├── profile-public/   { *.ts/.html/.css }  # NEW — public page (full-screen, anonymous) at /u/:handle
│   │   └── components/pompfe-selector/ { *.ts/.html/.css }  # NEW — full-set selector (selected vs available)
│   └── (dashboard, account) # EXISTING
├── app.routes.ts                                 # EXTEND — /u/:handle (public, outside shell); /profile (owner, in shell, guarded)
└── (app.config.ts)                               # EXISTING

frontend/apps/web-e2e/src/
└── profile.spec.ts                               # NEW — register→edit→public view journey, desktop + mobile

backend/appsettings*.json / docker-compose.yml    # EXTEND (optional) — Profile section (avatar size, handle bounds) with safe defaults
```

**Structure Decision**: Web application extending the existing `backend/` and `frontend/` trees. Backend stays organized by technical type with new logic grouped under `Services/Profile/` and `Services/Events/` (mirroring the existing `Services/Auth`, `Services/Email` grouping). The public profile page is a full-screen route **outside** the shell (like the auth screens) and anonymous; the owner profile lives **inside** the shell behind `authGuard`. No new project or library is introduced.

## Complexity Tracking

> No constitution violations. No entries required.
