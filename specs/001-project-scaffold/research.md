# Research: Project Scaffold (Walking Skeleton)

Phase 0 output. The constitution fixes the stack, so research here resolves the
**how** of the open integration points rather than choosing technologies. Each
item is a concrete decision with rationale and the alternatives considered.

---

## 1. argon2 password hashing inside Microsoft Identity

**Decision**: Keep ASP.NET Core Identity for user storage and flows, but replace
its default password hasher with a custom `IPasswordHasher<User>` backed by
`Konscious.Security.Cryptography.Argon2` (Argon2id). Register it in DI so Identity
uses argon2 for any future hashing/verification.

**Rationale**: The constitution mandates argon2 (salted). Identity's default is
PBKDF2; the framework exposes `IPasswordHasher<TUser>` exactly for swapping the
algorithm without abandoning Identity's user/lockout/policy machinery. Argon2id is
the OWASP-recommended variant (resists both GPU and side-channel attacks). A random
per-password salt is generated and stored with the hash; parameters (memory, time,
parallelism) are configurable via options.

**Alternatives considered**:
- *Isopoh.Cryptography.Argon2* — equivalent; Konscious chosen for its widespread
  use and simple API.
- *BCrypt/scrypt* — rejected; constitution specifies argon2.
- *Keep PBKDF2* — rejected; violates Principle IV.

**Scope note**: No password is actually hashed in this slice (no register/login),
but the hasher is wired and unit-testable so the later auth feature inherits it.

---

## 2. JWT carried in an httpOnly cookie (validation wiring)

**Decision**: Configure `AddAuthentication().AddJwtBearer(...)` and, in
`JwtBearerEvents.OnMessageReceived`, read the token from a named httpOnly cookie
(e.g. `jh_access`) instead of the `Authorization` header. Issuance config
(signing key, issuer, audience, lifetime) is bound from configuration; an
`IJwtTokenService` encapsulates token creation for the later auth feature. The
protected sample endpoint uses `[Authorize]`, so an absent/invalid cookie yields
`401`.

**Rationale**: Constitution requires JWT **only** in secure `httpOnly` cookies
(never localStorage) to defang XSS token theft. JwtBearer doesn't read cookies by
default; `OnMessageReceived` is the standard, minimal hook to source the token
from a cookie while reusing all standard validation. Cookie flags: `HttpOnly=true`,
`SameSite=Strict` (same-origin via the `/api` proxy — see §7), `Secure` driven by
environment (false on local HTTP, true on HTTPS Dev/Prod).

**Alternatives considered**:
- *Cookie authentication scheme instead of JWT* — rejected; constitution says JWT.
- *Custom middleware to rehydrate the `Authorization` header* — works but is more
  surface area than the documented `OnMessageReceived` event.

---

## 3. UUIDv7 primary keys with EF Core + Npgsql

**Decision**: `BaseEntity.Id` defaults to `Guid.CreateVersion7()` (generated
app-side in the entity initializer). Map to PostgreSQL `uuid`. No database default
needed since the value exists before insert.

**Rationale**: `Guid.CreateVersion7()` is built into .NET 9+. App-side generation
keeps the timestamp-prefixed, append-friendly key the constitution wants (avoids
B-tree page splits) and means the entity has its identity before `SaveChanges`,
which simplifies relationships and tests. Npgsql maps `Guid` ↔ `uuid` natively.

**Alternatives considered**:
- *Database-generated `gen_random_uuid()`* — produces v4 (random), defeating the
  sequential-locality goal; rejected.
- *Postgres-side uuidv7 function* — unnecessary; .NET generates it natively.

---

## 4. Automatic audit timestamps

**Decision**: Implement `AuditFieldsInterceptor : SaveChangesInterceptor` exactly
as the constitution illustrates — set `CreatedDate` on Added, `ModifiedDate` on
Added/Modified, using `DateTime.UtcNow`. Register on the `DbContext` via
`AddInterceptors`. Document that `ExecuteUpdateAsync` paths must set
`ModifiedDate` explicitly (interceptor is bypassed).

**Rationale**: Centralizes audit population; services never set timestamps for
tracked saves. Directly satisfies Principle III.

---

## 5. EF Core migrations auto-applied on startup (all environments)

**Decision**: On startup, after building the host, resolve `AppDbContext` in a
scope and call `db.Database.MigrateAsync()` **before** the app begins serving —
in every environment including Production (explicit user decision). Wrap in
try/catch that logs a generic failure and **fails fast** (process exits non-zero)
rather than serving against a broken/half-migrated schema.

**Rationale**: Zero-step startup and environment parity (a fresh/empty DB becomes
ready automatically). Fail-fast prevents the "started but broken" edge case in the
spec. This is a recorded trade-off: auto-migrating Production on deploy is
convenient but can apply an unintended schema change; accepted now, revisit when
Prod has real data/uptime guarantees (a gated migration job is the future option).

**Operational guards**: single API instance applies migrations (no concurrent
migration race in this scaffold); EF acquires an advisory lock during migration,
so even future multi-instance rollouts are serialized.

**Alternatives considered**:
- *Manual `dotnet ef database update`* — explicitly declined by the user.
- *Dev-only auto, manual in Prod* — declined; parity preferred.

---

## 6. API versioning + OpenAPI/Scalar UI

**Decision**: Use `Asp.Versioning.Mvc` with URL-segment versioning so routes are
`/api/v{version}/...` (v1 now). Generate the OpenAPI document with the built-in
`Microsoft.AspNetCore.OpenApi` (`AddOpenApi()` / `MapOpenApi()`) and render it with
**`Scalar.AspNetCore`** (`MapScalarApiReference()`), exposed **only in Development**
at `/scalar/v1`. Integrate with `Asp.Versioning.Mvc.ApiExplorer` so versioned
groups surface correctly.

**Rationale**: Decided with the user. `Microsoft.AspNetCore.OpenApi` is the
first-party, source-generated OpenAPI pipeline for .NET 9/10 (no third-party doc
generator), and Scalar is a modern, fast, good-looking interactive API reference
that consumes that document directly. Together they're a clean,
minimal-dependency replacement for Swashbuckle/Swagger UI. URL-segment versioning
remains the clearest scheme and keeps future v2 cleanly separable. Gating to
Development avoids exposing the schema/UI in Prod.

**Alternatives considered**:
- *Swashbuckle/Swagger UI* — the previous choice; replaced at the user's request
  in favor of the first-party OpenAPI pipeline + Scalar.
- *Header/query-string versioning* — less discoverable than URL segments.

---

## 7. Cross-service cookies: same-origin via the nginx `/api` proxy (Docker-only)

**Decision**: The browser only ever talks to the frontend origin; the frontend's
**nginx** (the only way the app runs locally — see §13) serves the SPA and
`proxy_pass`es `/api/` → `http://backend:8080`. The Angular app calls **relative**
`/api/v1/...` URLs. There is **no `ng serve`/`proxy.conf.json`** dev path — local
always means the containerized nginx image.

**Rationale**: httpOnly auth cookies are simplest and safest when first-party
(same-origin). Proxying `/api` through nginx avoids cross-origin cookie/CORS
complications (`SameSite=None`, preflight, credentialed CORS) entirely and keeps
`SameSite=Strict`. The frontend needs no environment-specific API base URL.
Because the user mandated a Docker-only workflow, collapsing to a single nginx
proxy (rather than maintaining a separate dev-server proxy config) also removes a
whole class of "works under `ng serve` but not in the container" drift.

**Alternatives considered**:
- *`ng serve` + `proxy.conf.json` for dev* — **rejected**: violates the Docker-only
  constraint and creates two code paths to keep in sync.
- *Direct cross-origin calls with CORS + `SameSite=None; Secure`* — weaker CSRF
  posture and more config; rejected.

---

## 8. Health endpoint design

**Decision**: Use `Microsoft.Extensions.Diagnostics.HealthChecks` with a
PostgreSQL/`DbContext` check. Expose the result through a thin `HealthController`
→ `IHealthService` returning a `HealthDto { status, database, version, timestamp }`
at `GET /api/v1/health` (public). When the DB is unreachable the service returns
`status: "unhealthy"`, `database: "unreachable"` with HTTP `200` body describing
status (so the dashboard can render it) — never a raw exception.

**Rationale**: Satisfies FR-002/FR-004 and the graceful-degradation edge case.
Routing the health check result through the service+DTO layer keeps it consistent
with the constitution's controller/service/DTO discipline (rather than only the
raw `/health` middleware endpoint). A standard liveness `/health` mapping may also
be kept for infra probes.

---

## 9. Backend integration testing with a real database

**Decision**: xUnit + `WebApplicationFactory<Program>` with
`Testcontainers.PostgreSql` spinning up a disposable `postgres:18` per test
collection. The factory overrides the connection string to the container; the app
auto-migrates on startup against it. One sample test asserts `GET /api/v1/health`
→ `200` + `database: reachable`, and the protected endpoint → `401` without a
cookie.

**Rationale**: Tests the real wiring (EF, migrations, Npgsql, auth pipeline)
against a real Postgres — matching production behavior far better than in-memory or
SQLite providers. Requires `Program` be partial/public for the factory (standard
minimal-hosting pattern). The test itself runs **inside a container** (see §13);
Testcontainers reaches the host Docker daemon through the mounted Docker socket.

**Alternatives considered**:
- *EF InMemory / SQLite* — rejected; wouldn't exercise Npgsql, UUIDv7 storage, or
  real migrations.
- *Shared external test DB* — rejected; non-hermetic, parallel-unsafe.

---

## 10. Nx + Angular + Tailwind themed from DESIGN.md

**Decision**: Nx workspace with one Angular application `web` (standalone
components, `@nx/angular`) and a `web-e2e` Playwright project. Tailwind configured
per the Angular+Tailwind guide; `DESIGN.md` tokens are surfaced as CSS custom
properties in `styles.css` and mapped into `tailwind.config.js` `theme.extend`
(colors/spacing/radius/typography) so utilities like `bg-primary`, `rounded-lg`
reflect the design system. Each component keeps separate `.html`/`.css`/`.ts`
files (Principle VI). Unit tests run on Jest (`@nx/jest`); e2e on Playwright.

The app shell is **responsive by default** (the product is used on desktop and
mobile): a mobile-first layout where the sidebar collapses to an off-canvas/drawer
pattern under a breakpoint and expands alongside content above it, using Tailwind's
responsive utilities tied to the DESIGN.md breakpoints. No fixed-width layouts that
overflow small screens.

**Rationale**: Nx + Jest + Playwright is the mandated, well-trodden combination;
sourcing tokens from `DESIGN.md` keeps the product UI and the existing email
identity unified (DESIGN.md is the UI source of truth). Building responsively from
the first component avoids a costly retrofit later.

**Alternatives considered**:
- *Karma/Jasmine* — Nx defaults to Jest; no reason to deviate.
- *Hard-coding colors* — violates DESIGN.md as source of truth.
- *Desktop-only first, responsive later* — rejected; the product targets mobile
  from day one, so the shell is responsive from the start.

---

## 11. Global exception handling → generic problem JSON

**Decision**: `ExceptionHandlingMiddleware` wraps the pipeline, logs the full
exception server-side, and returns an RFC7231/`ProblemDetails`-shaped body
(`type/title/status/detail`) with a generic `detail` and `500` status — no stack
trace or internal text. Registered early in the pipeline.

**Rationale**: Mirrors the constitution's reference middleware; satisfies FR-009
and Principle I.

---

## 12. Local engine version alignment

**Decision**: Bump the compose Postgres image from `postgres:16-alpine` to
`postgres:18-alpine` to match the constitution's PostgreSQL 18.

**Rationale**: Environment parity (Principle V) and the constitution's stated
version. Low risk at scaffold stage (no data yet).

---

## 13. Docker-only workflow — running the app and all tests in containers

**Decision** (user mandate): There is **no host-level run path**. The app runs only
via `docker compose up`. All automated tests run in containers too, orchestrated by
a root **`docker-compose.test.yml`** layered on `docker-compose.yml`:
- **Backend tests** — a `backend-test` service built from the backend `Dockerfile`'s
  `test` stage runs `dotnet test`. The Docker socket is mounted
  (`/var/run/docker.sock`) so the Testcontainers-spawned Postgres works (Docker-in-
  Docker via the host daemon / sibling-container pattern).
- **Frontend unit tests** — a `frontend-test` service built from the frontend
  `Dockerfile`'s `test` stage runs `nx test web` (Jest) headless.
- **Frontend e2e** — a `playwright` service (based on the official
  `mcr.microsoft.com/playwright` image) runs `nx e2e web-e2e` against the running
  `frontend` container, at desktop and mobile viewports.

Each is invoked with `docker compose -f docker-compose.yml -f docker-compose.test.yml run --rm <service>` and exits with the test status (CI-friendly later).

**Rationale**: The user requires that everything works in Docker and nothing relies
on host runtimes; this guarantees parity between a developer's machine, teammates,
and future CI, and eliminates "works on my host" drift. Multi-stage Dockerfiles let
the same image definition produce both the runtime artifact and the test runner.

**Alternatives considered**:
- *Run `dotnet test` / `nx test` on the host* — **rejected**: violates the
  Docker-only mandate and assumes host SDK/Node installs.
- *A separate Dockerfile per concern* — unnecessary; multi-stage targets keep one
  Dockerfile per service.

**Implication**: Testcontainers-in-container requires the mounted Docker socket;
documented so operators understand the privilege. (CI will provide an equivalent
socket/daemon.)

---

## 14. Validating desktop + mobile usability

**Decision** (user mandate): All frontend work is validated on **both** desktop and
mobile. Concretely: the Playwright e2e config defines (at least) a desktop project
(~1280×800) and a mobile project (e.g. emulated Pixel/iPhone, ~375–390px wide); the
sample e2e asserts the dashboard renders the health status and the shell navigation
is reachable in **both**. Manual acceptance also checks no clipped content / no
horizontal scroll at mobile width.

**Rationale**: The product is used on desktop and phones; encoding multi-viewport
checks into the e2e harness makes "responsive" a verifiable gate (SC-009) rather
than an aspiration, from the very first UI.

**Alternatives considered**:
- *Single desktop viewport in e2e* — rejected; wouldn't catch mobile regressions.

---

## Resolved unknowns

All Technical Context items are resolved; **no `NEEDS CLARIFICATION` remain**. Open
product questions (team/club modeling, forum, tournaments, auth UX) are
intentionally deferred to their own future specs and do not block this scaffold.
