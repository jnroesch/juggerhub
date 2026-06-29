---
description: "Task list for Project Scaffold (Walking Skeleton)"
---

# Tasks: Project Scaffold (Walking Skeleton)

**Input**: Design documents from `/specs/001-project-scaffold/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Test harnesses and sample tests are an explicit deliverable of this feature (FR-021, FR-022, FR-026), so test tasks are included.

**Organization**: Tasks are grouped by user story. Note that a walking skeleton is inherently integrative — the stories here are **layered increments** (each adds a testable capability on the shared foundation) rather than fully orthogonal features. US1 (health slice) is the MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US4, mapping to the spec's user stories
- All paths are repo-relative. Backend `.csproj` is at `backend/` root (namespace `JuggerHub`); frontend is an Nx workspace under `frontend/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization — get both stacks scaffolded and the toolchain in place.

- [X] T001 Scaffold the backend project: `backend/JuggerHub.sln` + `backend/JuggerHub.Api.csproj` (`net10.0`, `RootNamespace=JuggerHub`, nullable + implicit usings enabled) with a minimal runnable `backend/Program.cs`, `backend/appsettings.json`, and `backend/appsettings.Development.json`
- [X] T002 [P] Add backend NuGet references to `backend/JuggerHub.Api.csproj`: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Mapster`, `Asp.Versioning.Mvc`, `Asp.Versioning.Mvc.ApiExplorer`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`, `Konscious.Security.Cryptography.Argon2`, `AspNetCore.HealthChecks.NpgSql`
- [X] T003 [P] Initialize the Nx workspace in `frontend/` with a standalone Angular app `web` (`frontend/apps/web/`) and a Playwright e2e project `web-e2e` (`frontend/apps/web-e2e/`); commit `frontend/nx.json`, `frontend/package.json`, `frontend/tsconfig.base.json`
- [X] T004 [P] Configure Tailwind and map DESIGN.md tokens (colors, spacing, rounded, typography) into `frontend/apps/web/tailwind.config.js` and expose them as CSS variables in `frontend/apps/web/src/styles.css`
- [X] T005 [P] Align infra config: bump `docker-compose.yml` Postgres image `postgres:16-alpine` → `postgres:18-alpine`; extend `.env.sample` with DB + JWT keys (signing key, issuer, audience, lifetime) and confirm `docker-compose.yml` passes them to the `backend` service env
- [X] T006 [P] Add `backend/.dockerignore` excluding `tests/`, `bin/`, `obj/`

**Checkpoint**: Both projects exist, build, and boot in isolation; dependencies resolved.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared base every user story builds on — data layer, shared API conventions, frontend app composition, and the test-harness skeletons.

**⚠️ CRITICAL**: No user story work begins until this phase is complete.

- [X] T007 [P] Create `backend/Entities/BaseEntity.cs` — `Guid Id = Guid.CreateVersion7()`, `DateTime CreatedDate`, `DateTime ModifiedDate`
- [X] T008 [P] Create `backend/Entities/User.cs` — `User : IdentityUser<Guid>` (no custom profile fields yet)
- [X] T009 Create `backend/Data/AuditFieldsInterceptor.cs` — `SaveChangesInterceptor` setting `CreatedDate` on Added and `ModifiedDate` on Added/Modified (UTC)
- [X] T010 Create `backend/Data/AppDbContext.cs` — `IdentityDbContext<User, IdentityRole<Guid>, Guid>`, registering the audit interceptor (depends on T007–T009)
- [X] T011 Register data + mapping services in `backend/Program.cs` — `AddDbContext` (Npgsql, connection string from config), ASP.NET Core Identity, and Mapster config in `backend/Common/MappingConfig.cs` (depends on T010)
- [X] T012 Generate the initial EF Core migration in `backend/Data/Migrations/` (Identity schema with `uuid` keys) (depends on T011)
- [X] T013 Auto-apply migrations on startup with fail-fast (log generic error + exit non-zero on failure) in `backend/Program.cs` (depends on T012)
- [X] T014 [P] Create shared primitives `backend/Dtos/PaginationRequest.cs` and `backend/Dtos/PagedResult.cs` (normalization + max page size per constitution)
- [X] T015 Create `backend/Common/ExceptionHandlingMiddleware.cs` (RFC7231 `ProblemDetails`, generic detail, no leaks) and register it early in the `backend/Program.cs` pipeline
- [X] T016 Configure URL API versioning (`/api/v{n}`, default v1) via `Asp.Versioning` in `backend/Program.cs`
- [X] T017 Frontend app composition: `frontend/apps/web/src/app/app.config.ts`, `app.routes.ts`, `main.ts`, `index.html`, plus `frontend/nginx.conf` serving the SPA and `proxy_pass`ing `/api/` → `http://backend:8080`
- [X] T018 Stand up test-harness skeletons: `backend/tests/JuggerHub.Api.IntegrationTests/` (xUnit + `WebApplicationFactory` + `Testcontainers.PostgreSql`, make `Program` partial/public, one trivial passing test); Jest config for `web` plus a placeholder smoke spec (upgraded to a real sample in T034); and `frontend/playwright.config.ts` with **desktop + mobile** projects and one trivial passing spec

**Checkpoint**: App boots against a real (auto-migrated) Postgres; shared conventions and empty test harnesses exist. User stories can begin.

---

## Phase 3: User Story 1 — Prove the stack end-to-end (Priority: P1) 🎯 MVP

**Goal**: A complete frontend → API → PostgreSQL round trip: the dashboard shows a health status reflecting live DB connectivity, degrading gracefully when the DB is down.

**Independent Test**: `docker compose up`, open the dashboard → status "healthy"; stop the DB → status "unhealthy" (no crash/raw error). Maps to SC-001, SC-002.

### Tests for User Story 1

- [X] T019 [P] [US1] Integration test in `backend/tests/JuggerHub.Api.IntegrationTests/HealthEndpointTests.cs`: `GET /api/v1/health` → 200 with `database: reachable` against the Testcontainers Postgres
- [X] T020 [P] [US1] e2e test in `frontend/apps/web-e2e/src/health.spec.ts`: dashboard loads and renders the health status

### Implementation for User Story 1

- [X] T021 [P] [US1] Create `backend/Dtos/HealthDto.cs` (`status`, `database`, `version`, `timestamp`)
- [X] T022 [US1] Create `backend/Services/Health/IHealthService.cs` + `HealthService.cs` — checks DB connectivity (health check / `CanConnectAsync`), returns `HealthDto` (`version` from the API assembly's informational version); never throws on DB-down (FR-004)
- [X] T023 [US1] Create thin `backend/Controllers/HealthController.cs` → `GET /api/v1/health` (public, delegates to `IHealthService` and returns the `HealthDto` directly — Mapster is registered for the DTO pattern, but trivial read models like this are constructed directly rather than mapped)
- [X] T024 [P] [US1] Create `frontend/apps/web/src/app/core/services/health.service.ts` calling relative `/api/v1/health`
- [X] T025 [US1] Create `frontend/apps/web/src/app/features/dashboard/dashboard.component.{ts,html,css}` displaying the health status and route it as a default page (depends on T024)

**Checkpoint**: US1 is independently demoable — the walking skeleton's core slice works end-to-end.

---

## Phase 4: User Story 2 — Prove the security boundary (Priority: P1)

**Goal**: The auth/authorization pipeline is enforced server-side — a protected endpoint returns 401 without a valid httpOnly-cookie JWT, errors never leak internals, and the frontend guards/interceptor are wired — all without any auth UI/endpoints.

**Independent Test**: Call `/api/v1/diagnostics/whoami` without a cookie → 401 with `ProblemDetails`; public health still succeeds; guarded route redirects toward sign-in. Maps to SC-003, SC-004.

### Tests for User Story 2

- [X] T026 [P] [US2] Integration test in `backend/tests/JuggerHub.Api.IntegrationTests/DiagnosticsEndpointTests.cs`: `GET /api/v1/diagnostics/whoami` → 401 without auth; body is `ProblemDetails` with no stack trace/secret

### Implementation for User Story 2

- [X] T027 [P] [US2] Implement argon2id `IPasswordHasher<User>` in `backend/Services/Security/Argon2PasswordHasher.cs` and register it in DI (overrides Identity default)
- [X] T028 [P] [US2] Implement `backend/Services/Security/IJwtTokenService.cs` + `JwtTokenService.cs` (issue/validate config) and `backend/Common/AuthCookieDefaults.cs` (cookie name `jh_access`, HttpOnly/SameSite/Secure options)
- [X] T029 [US2] In `backend/Program.cs`: configure Identity password + lockout policy (per constitution), add JwtBearer auth reading the token from the httpOnly cookie (`OnMessageReceived`), and enable the authentication/authorization pipeline (depends on T027, T028)
- [X] T030 [US2] Create `backend/Dtos/WhoAmIDto.cs` and the `[Authorize]` `backend/Controllers/DiagnosticsController.cs` → `GET /api/v1/diagnostics/whoami` (depends on T029)
- [X] T031 [P] [US2] Create `frontend/apps/web/src/app/core/interceptors/auth.interceptor.ts` (`withCredentials`, 401 → refresh → redirect-to-sign-in) + `frontend/apps/web/src/app/core/services/auth.service.ts` (stub: token/refresh/sign-out surface, no endpoints yet); register the interceptor in `app.config.ts`. Use "sign-in" consistently (not "login") across guard, interceptor, and service.
- [X] T032 [P] [US2] Create `frontend/apps/web/src/app/core/guards/auth.guard.ts` redirecting unauthenticated access toward sign-in, and apply it to one guarded sample route

**Checkpoint**: The security boundary is enforced and demonstrable; future auth feature only adds endpoints + UI.

---

## Phase 5: User Story 3 — A foundation features can be added to (Priority: P2)

**Goal**: The consistent, documented foundation is visibly in place — a responsive app shell themed from DESIGN.md, browsable API docs (Scalar), versioned routes, and the shared primitives demonstrated — validated on desktop and mobile.

**Independent Test**: App shows the nav + sidebar shell in the brand identity and stays usable at ~375px and ~1280px; Scalar lists/invokes the versioned endpoints in Development. Maps to SC-008, SC-009.

### Tests for User Story 3

- [X] T033 [P] [US3] Responsive e2e in `frontend/apps/web-e2e/src/responsive.spec.ts`: dashboard renders and nav stays reachable with no clipped content / horizontal scroll at both desktop (~1280×800) and mobile (~375px) projects
- [X] T034 [P] [US3] Replace the placeholder Jest smoke spec (from T018) with a real sample unit test in `frontend/apps/web/src/app/...` (e.g. `health.service` or a shell component) proving the unit harness runs against real code

### Implementation for User Story 3

- [X] T035 [US3] Expose the OpenAPI document + **Scalar** UI (Development-only) at `/scalar/v1` integrated with the versioning ApiExplorer, in `backend/Program.cs`
- [X] T036 [P] [US3] Build the responsive app shell — `frontend/apps/web/src/app/layout/shell/`, `layout/top-nav/`, `layout/sidebar/` (each `.ts`/`.html`/`.css`) — sidebar collapses to a drawer below a breakpoint; styled from DESIGN.md tokens
- [X] T037 [US3] Mount the dashboard inside the shell and set it as the default authed route (depends on T036, and on T025)

**Checkpoint**: The shell + conventions are demonstrable and responsive; new features have a clear pattern to follow.

---

## Phase 6: User Story 4 — Reproducible, low-friction environment (Priority: P2)

**Goal**: The whole stack and the entire test suite run container-only, schema auto-applies on an empty DB, and behavior is identical across environments by config alone.

**Independent Test**: Fresh clone → `.env` from sample → `docker compose up` reaches a healthy app with no manual DB step; all tests run via the Docker test overlay. Maps to SC-005, SC-006, SC-010.

### Implementation for User Story 4

- [X] T038 [US4] Flesh out `backend/Dockerfile` as multi-stage: `build` → `test` target (runs `dotnet test`) → `runtime` (ENTRYPOINT `JuggerHub.Api.dll`); ensure it builds from `backend/` context with the root `.csproj`
- [X] T039 [US4] Flesh out `frontend/Dockerfile` as multi-stage: Nx `build` → `nginx` runtime (with `nginx.conf`); plus a `test` target (Jest) and a Playwright stage (official Playwright image) for e2e
- [X] T040 [US4] Add root `docker-compose.test.yml` overlay with `backend-test` (mounts `/var/run/docker.sock` for Testcontainers), `frontend-test` (Jest), and `playwright` (e2e vs the running `frontend`, desktop+mobile) services
- [X] T041 [US4] Validate container-only run + auto-migrate against an empty DB per quickstart steps 1–3 (single-command up; healthy; DB-down degrades) and record results
- [X] T042 [US4] Run the full suite entirely via the Docker overlay (`backend-test`, `frontend-test`, `playwright`) and confirm green — no host runtimes used (SC-006, SC-010)

**Checkpoint**: Reproducible, Docker-only environment proven; all tests green in containers.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, conventions, and final end-to-end validation.

- [ ] T043 [P] Update `README.md` setup/run/test sections to the Docker-only workflow and Scalar (remove any Swagger / `ng serve` references)
- [ ] T044 [P] Verify no host-only/`.sh` project scripts were added by this feature; any new tooling scripts are PowerShell `.ps1` (Principle VI)
- [ ] T045 Run the full `quickstart.md` validation (all 9 steps) end-to-end and record pass/fail per success criterion
- [ ] T046 [P] Run the changed diff through `/code-review` (or `/speckit-analyze` for spec↔code drift) and address findings
- [ ] T047 Trigger the Graphify rebuild, record a claude-mem note of the key scaffolding decisions, and update the Backlog item status

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories**.
- **User Stories (Phases 3–6)**: all depend on Foundational. Recommended order is priority order (US1 → US2 → US3 → US4); US1 and US2 are both P1 (US1 first as the MVP slice). US3 and US4 build on US1/US2 being present (shell mounts the dashboard; US4 runs the stories' tests in Docker).
- **Polish (Phase 7)**: after the desired stories are complete.

### Story-level notes (walking skeleton = layered, not fully orthogonal)

- **US1 (P1)**: only needs Foundational. The true MVP.
- **US2 (P1)**: only needs Foundational; independent of US1 at the API level (different endpoints/files).
- **US3 (P2)**: depends on US1 (mounts the dashboard in the shell — T037 ⇠ T025) and reuses the auth guard from US2 for the guarded route.
- **US4 (P2)**: exercises the test suites authored across US1–US3 inside Docker; do it after those stories so there are real tests to run.

### Within each story

- Tests are written before/with implementation and must pass before the story is considered done.
- Models → services → controllers/endpoints → frontend wiring.

### Parallel Opportunities

- Setup: T002, T003, T004, T005, T006 can run in parallel (distinct files/areas).
- Foundational: T007 and T008 in parallel; T014 in parallel with the data-context chain; frontend T017 in parallel with backend tasks.
- US1: T019/T020 (tests) parallel; T021 and T024 parallel; T022→T023 sequential; T025 after T024.
- US2: T026 (test) and T027/T028/T031/T032 are largely parallel; T029 needs T027+T028; T030 needs T029.
- US3: T033, T034, T036 parallel; T035 independent (backend); T037 after T036/T025.
- Different developers can own US1, US2 in parallel once Foundational is done.

---

## Parallel Example: User Story 1

```bash
# Tests for US1 together:
Task: "Integration test GET /api/v1/health in backend/tests/JuggerHub.Api.IntegrationTests/HealthEndpointTests.cs"
Task: "e2e dashboard health in frontend/apps/web-e2e/src/health.spec.ts"

# Then parallel implementation pieces:
Task: "HealthDto in backend/Dtos/HealthDto.cs"
Task: "health.service.ts in frontend/apps/web/src/app/core/services/health.service.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & VALIDATE** (`docker compose up`, dashboard healthy, DB-down degrades) → demo the walking skeleton.

### Incremental Delivery

Foundation → US1 (MVP, end-to-end slice) → US2 (security boundary) → US3 (shell + conventions, responsive) → US4 (Docker-only repro + all tests green) → Polish. Each increment is independently testable and adds value without breaking the previous.

---

## Notes

- `[P]` = different files, no incomplete dependencies.
- `[Story]` labels map tasks to spec user stories for traceability.
- This is a scaffold: prefer the constitution's existing patterns; do not introduce abstractions beyond what the spec/plan call for.
- Commit after each task or logical group; keep auth endpoints, Teams/Tournaments/Forum, CI/CD, Terraform, real-time, and seed data **out of scope**.
- Verify story tests pass before marking a story done; run `quickstart.md` at the end.
