# Implementation Plan: Project Scaffold (Walking Skeleton)

**Branch**: `001-project-scaffold` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-project-scaffold/spec.md`

## Summary

Build the JuggerHub walking skeleton: a single end-to-end vertical slice (Angular dashboard → `GET /api/v1/health` → PostgreSQL connectivity) plus a protected sample endpoint that proves the authentication/authorization pipeline is enforced, together with the shared foundations every later feature will reuse. The approach honours the constitution exactly — a layered ASP.NET Core (.NET 10) API with thin controllers over DI'd services (no repository layer), EF Core + Npgsql against PostgreSQL 18, `BaseEntity`/UUIDv7/audit interceptor, Mapster DTO mapping, Microsoft Identity with an argon2 password hasher and JWT carried in httpOnly cookies, `/api/v1` versioning, OpenAPI (via `Microsoft.AspNetCore.OpenApi`) surfaced with **Scalar** UI in Development, global exception middleware, shared pagination primitives, an Nx + Angular + Tailwind front end themed from `DESIGN.md` with a **responsive** app shell + AuthGuard + auth interceptor, EF migrations auto-applied on startup in all environments, and xUnit/Testcontainers + Jest/Playwright test harnesses. **Everything — running the app and running every test — happens through Docker; there is no host-level dev-server workflow (no `ng serve`).** The whole stack comes up via the existing `docker-compose`. All frontend work is validated for both desktop and mobile usability. Auth endpoints/UI, the Teams/Tournaments/Forum domains, CI/CD, Terraform, real-time, and seed data are explicitly out of scope.

## Technical Context

**Language/Version**: Backend — C# 13 on .NET 10 (ASP.NET Core, EF Core 10). Frontend — TypeScript on Angular (latest stable, standalone components) in an Nx workspace.

**Primary Dependencies**:
- Backend: `Microsoft.AspNetCore.App`, `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Mapster`, `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi` (built-in OpenAPI document) + `Scalar.AspNetCore` (interactive API reference UI), an Argon2 implementation (`Konscious.Security.Cryptography.Argon2`) behind a custom `IPasswordHasher<User>`, `Microsoft.Extensions.Diagnostics.HealthChecks` (+ Npgsql health check).
- Frontend: `@angular/*`, `@nx/angular`, `tailwindcss`, `@angular/router`, RxJS; `jest` (unit, via `@nx/jest`), `@playwright/test` (e2e, multi-viewport desktop + mobile).

**Storage**: PostgreSQL 18 (`postgres:18-alpine` in compose; aligned up from the current `16-alpine`).

**Testing**: Backend — xUnit + `WebApplicationFactory` + Testcontainers (`Testcontainers.PostgreSql`) for a real-database integration test. Frontend — Jest unit tests + Playwright e2e (run at desktop **and** mobile viewports), one sample each. **All tests execute inside containers** (see research §13) — no host runtimes assumed.

**Target Platform**: Linux containers via Docker; orchestrated locally by `docker-compose`. The product UI targets **both desktop and mobile browsers** (responsive web). (Azure App Services is the deploy target, configured in a later feature.)

**Project Type**: Web application — separate `backend/` (.NET) and `frontend/` (Nx/Angular) trees.

**Performance Goals**: Walking skeleton — no domain throughput targets. Health round-trip should feel instant under local load; the slice exists to prove wiring, not performance.

**Constraints**: Security-first / OWASP / never-trust-the-client; JWT only in httpOnly cookies; no stack traces or secrets to the client; environment parity (local/Dev/Prod differ only by config + secrets); all configuration/secrets from `.env` (local) — never hard-coded; PowerShell (`.ps1`) only for any project tooling added; auto-apply migrations on startup in every environment (deliberate trade-off, see research); **Docker-only workflow — the app and every test run through containers; no host-level dev server (`ng serve`) or host runtime dependency**; **responsive UI required (desktop + mobile), validated at multiple viewports**.

**Scale/Scope**: Foundation only — one API service, one Angular app, one `User` identity entity, two endpoints (one public health, one protected sample). Sized so future features add modules without reshaping the skeleton.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | How this plan complies | Verdict |
|---|-----------|------------------------|---------|
| I | Security-First, Never Trust the Client | Global exception middleware emits generic RFC7231 problem JSON (no stack traces/secrets); `[Authorize]`-protected sample endpoint enforced server-side; JWT in `httpOnly` cookies only; secrets from `.env`/env vars. | ✅ |
| II | Thin Controllers, Service-Centric | `HealthController`/sample controller are thin, delegate to `IHealthService` etc.; DI throughout with interfaces; **no repository layer**; responses are DTOs mapped via Mapster. | ✅ |
| III | Disciplined Data Access (EF Core + PostgreSQL) | `BaseEntity` with `Guid.CreateVersion7()` + audit timestamps via `AuditFieldsInterceptor`; reads use `AsNoTracking`/projections; shared `PaginationRequest`/`PagedResult<T>` provided and demonstrated. | ✅ |
| IV | Secure Authentication & Session Management | Microsoft Identity + `User : IdentityUser`; custom argon2 `IPasswordHasher<User>`; JWT issuance config + Bearer reading token from the httpOnly cookie; Identity password/lockout policy configured. Sign-up/sign-in/forgot endpoints intentionally deferred (documented). | ✅ (flows deferred by design) |
| V | Environment Parity & Containerized Deployments | Per-service Dockerfiles fleshed out; single `docker-compose` brings up the stack; identical build across envs; migrations auto-apply on startup everywhere. CI/CD + Terraform deferred to a dedicated feature (allowed by scope). | ✅ (infra deferred) |
| VI | Consistent Conventions & Tooling | Angular + Nx + Tailwind with separate `.html`/`.css`/`.ts` per component; any scripts added are `.ps1` only. | ✅ |
| — | Secret & Configuration Management | Single local `.env` (sample committed, real ignored); no Key Vault; no secrets in code/specs. | ✅ |
| — | Transactional Email | Reuses existing `EmailTemplates` + `EmailTemplateService`; Mailpit wired locally. No email sent in this slice. | ✅ |

**Result**: PASS — no violations. Complexity Tracking left empty. Two scope-driven deferrals (auth endpoints; CI/CD + Terraform) are explicit in the spec and not constitution violations.

> **Pre-existing note (not introduced by this feature):** the `.githooks/` scripts are `bash`. They are git plumbing that must run in git's POSIX hook environment and predate this feature; Principle VI governs *project tooling scripts*, which this feature adds only as `.ps1`. Flagged for transparency, not as a new violation.

## Project Structure

### Documentation (this feature)

```text
specs/001-project-scaffold/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output — resolved technical decisions
├── data-model.md        # Phase 1 output — entities & shared data shapes
├── quickstart.md        # Phase 1 output — runnable validation guide
├── contracts/           # Phase 1 output — API contracts
│   ├── openapi.yaml      #   health + protected sample endpoint
│   └── README.md
└── checklists/
    └── requirements.md  # Spec quality checklist (from /speckit-specify)
```

### Source Code (repository root)

```text
backend/                                  # .NET 10 solution (root namespace JuggerHub)
├── JuggerHub.sln
├── JuggerHub.Api.csproj                  # single layered API project (csproj at root → matches existing Dockerfile COPY *.csproj)
├── Program.cs                            # composition root: DI, auth, versioning, OpenAPI+Scalar, middleware, auto-migrate
├── appsettings.json
├── appsettings.Development.json
├── Controllers/
│   ├── HealthController.cs               # GET /api/v1/health  (public)
│   └── DiagnosticsController.cs          # GET /api/v1/diagnostics/whoami (protected sample → 401 unauth)
├── Services/
│   ├── Health/ { IHealthService.cs, HealthService.cs }
│   ├── Security/ { Argon2PasswordHasher.cs, IJwtTokenService.cs, JwtTokenService.cs }
│   └── EmailTemplateService/             # EXISTING — unchanged
├── Entities/
│   ├── BaseEntity.cs                     # Guid Id = CreateVersion7(); CreatedDate; ModifiedDate
│   └── User.cs                           # : IdentityUser<Guid>  (identity foundation only)
├── Data/
│   ├── AppDbContext.cs                   # : IdentityDbContext<User, IdentityRole<Guid>, Guid>
│   ├── AuditFieldsInterceptor.cs
│   └── Migrations/                       # generated EF migrations
├── Dtos/
│   ├── HealthDto.cs
│   ├── PaginationRequest.cs              # shared primitive (constitution)
│   └── PagedResult.cs                    # shared primitive (constitution)
├── Common/
│   ├── ExceptionHandlingMiddleware.cs    # RFC7231 generic problem JSON
│   ├── MappingConfig.cs                  # Mapster registration
│   └── AuthCookieDefaults.cs             # cookie name/options constants
├── EmailTemplates/                       # EXISTING — unchanged
├── Dockerfile                            # EXISTING — multi-stage: `test` target (runs dotnet test) + runtime; ENTRYPOINT JuggerHub.Api.dll
├── .dockerignore                         # exclude tests/ from runtime stage, bin/, obj/
└── tests/
    └── JuggerHub.Api.IntegrationTests/   # xUnit + WebApplicationFactory + Testcontainers.PostgreSql
        ├── JuggerHub.Api.IntegrationTests.csproj
        └── HealthEndpointTests.cs        # health 200 + db reachable; protected endpoint 401

frontend/                                 # Nx workspace
├── nx.json, package.json, tsconfig.base.json
├── nginx.conf                            # container image: serve SPA + proxy /api → backend:8080 (same-origin cookies)
├── Dockerfile                            # EXISTING — multi-stage: build (Nx) → nginx runtime; plus `test` target (jest) + Playwright stage
├── playwright.config.ts                  # e2e against the dockerized stack; desktop + mobile projects/viewports
├── apps/
│   ├── web/
│   │   ├── project.json, tailwind.config.js
│   │   └── src/
│   │       ├── styles.css                # Tailwind + DESIGN.md tokens as CSS variables
│   │       ├── index.html, main.ts
│   │       └── app/
│   │           ├── app.routes.ts, app.config.ts
│   │           ├── core/
│   │           │   ├── guards/auth.guard.ts
│   │           │   ├── interceptors/auth.interceptor.ts
│   │           │   └── services/{auth.service.ts, health.service.ts}
│   │           ├── layout/
│   │           │   ├── shell/ { shell.component.ts/.html/.css }   # top nav + sidebar
│   │           │   ├── top-nav/ {…}
│   │           │   └── sidebar/ {…}
│   │           └── features/dashboard/ { dashboard.component.ts/.html/.css }  # calls /api/v1/health
│   └── web-e2e/                          # Playwright e2e (one sample)
└── libs/                                 # (empty now; design-tokens/ui libs added per future feature)
```

**Structure Decision**: Web application with the existing sibling `backend/` (.NET) and `frontend/` (Nx) trees. The backend is a **single layered API project with its `.csproj` at `backend/` root**, which (a) matches the existing `Dockerfile`'s `COPY *.csproj`, and (b) keeps the existing `EmailTemplates/` and `Services/EmailTemplateService/` in place untouched. Folders are organized by technical type (Controllers/Services/Entities/Data/Dtos/Common) per the agreed layered style. The backend test project lives under `backend/tests/` and is excluded from the Docker runtime stage via `.dockerignore`. The frontend is a standard Nx workspace with one Angular app (`web`) and its Playwright e2e companion (`web-e2e`).

**Docker-only workflow**: the app and **all** tests run through containers — there is no `ng serve`/host-runtime path. A root **`docker-compose.test.yml`** (composed with `docker-compose.yml`) provides ephemeral test execution: a backend-test service that runs `dotnet test` (Testcontainers reaches the Docker daemon via the mounted socket) and a frontend-test service that runs Jest, plus a Playwright service that runs e2e against the running stack at desktop and mobile viewports. Because everything is same-origin in containers, the frontend's `nginx.conf` `proxy_pass`es `/api` → `backend:8080`, so httpOnly auth cookies stay first-party without any dev-server proxy. The Angular app always calls relative `/api/v1/...` URLs.

## Complexity Tracking

> No constitution violations. No entries required.
