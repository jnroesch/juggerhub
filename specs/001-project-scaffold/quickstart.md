# Quickstart & Validation: Project Scaffold (Walking Skeleton)

A runnable guide to prove the scaffold works end-to-end. It validates the spec's
success criteria — it is **not** an implementation guide (see `tasks.md` for that).

## Prerequisites

- **Docker + Docker Compose only.** Everything — the app and every test — runs in
  containers; no host-level .NET SDK or Node install is required, and there is no
  `ng serve`/host dev-server path.
- A local `.env` created from the sample:
  ```pwsh
  Copy-Item .env.sample .env   # then review values; defaults work for local
  ```
- (Optional, authoring only) A host .NET SDK + `dotnet-ef` is needed *only* if you
  want to scaffold a **new** EF migration; running the app applies existing
  migrations automatically.

> Scripts added by this project are PowerShell (`.ps1`) only. Commands below use
> PowerShell where a shell is needed.

## 1. Bring up the full stack (FR-001, SC-001)

```pwsh
docker compose up -d --build
docker compose ps          # backend, frontend, database (postgres:18), mailpit all Up
```

Expected: all services healthy. The backend **auto-applies EF migrations on
startup** against the (initially empty) database — no manual migration step
(FR-018, SC-005).

## 2. Verify the end-to-end health slice (FR-002, FR-003, SC-002)

Open the app and confirm the dashboard shows a healthy status:

```pwsh
Start-Process http://localhost:3000      # dashboard renders; status = "healthy"
```

Or hit the API directly (through the frontend's same-origin proxy):

```pwsh
curl http://localhost:3000/api/v1/health
# { "status":"healthy", "database":"reachable", "version":"1.0.0", "timestamp":"..." }
```

## 3. Verify graceful DB-down behavior (FR-004, SC-002)

```pwsh
docker compose stop database
curl http://localhost:3000/api/v1/health
# 200 with { "status":"unhealthy", "database":"unreachable", ... } — no stack trace
docker compose start database
```

## 4. Verify the security boundary (FR-005, FR-006, SC-003)

```pwsh
curl -i http://localhost:3000/api/v1/diagnostics/whoami
# HTTP/1.1 401 Unauthorized
# application/problem+json body: { "type":..., "title":..., "status":401, "detail":"..." }
```

Confirm the public health endpoint still succeeds without credentials (contrast).
In the browser, navigating to a guarded route while unauthenticated redirects
toward sign-in rather than showing protected content (FR-008).

## 5. Verify no internal detail leaks (FR-009, SC-004)

Any error response (e.g. the `401` above, or a forced `500`) contains only generic
`ProblemDetails` — no stack trace, exception text, connection string, or secret.

## 6. Verify developer-experience conventions (FR-011, FR-014, SC-008)

```pwsh
# Scalar API reference (Development only) lists & invokes the versioned endpoints:
Start-Process http://localhost:8080/scalar/v1
```

Confirm routes are under `/api/v1`, and the app shell shows the top-nav + sidebar
styled from DESIGN.md tokens (FR-015, FR-016).

## 7. Verify desktop + mobile usability (FR-025, FR-026, SC-009)

```pwsh
# Playwright e2e runs at desktop AND mobile viewports (see step 8 for the command).
```

Also eyeball it: open `http://localhost:3000`, then use the browser devtools device
toolbar to switch to a mobile width (~375px). The sidebar collapses to a mobile
pattern, navigation stays reachable, and there is no clipped content or horizontal
scrolling. Repeat at desktop width (~1280px).

## 8. Run the test harnesses — all in Docker (FR-021, FR-022, FR-027, SC-006, SC-010)

No host runtimes needed; tests run in containers via the test compose overlay.

```pwsh
$test = "docker compose -f docker-compose.yml -f docker-compose.test.yml"

# Backend integration test (real Postgres via Testcontainers; Docker socket mounted):
iex "$test run --rm backend-test"
#   HealthEndpointTests: /api/v1/health → 200 + database reachable; whoami → 401

# Frontend unit tests (Jest):
iex "$test run --rm frontend-test"

# Frontend e2e (Playwright) at desktop + mobile viewports, against the running stack:
iex "$test run --rm playwright"
#   sample: dashboard loads & shows health status on desktop AND mobile projects
```

## 9. Confirm reproducibility / parity (FR-017, FR-020)

A fresh clone + `Copy-Item .env.sample .env` + `docker compose up -d --build`
reaches a running, healthy app with no extra steps (SC-001). The same image runs
in Dev/Prod with only configuration/secrets differing.

## Success = all of the above

| Check | Success Criteria |
|-------|------------------|
| Steps 1–2 | SC-001, SC-002 |
| Step 3 | SC-002 |
| Step 4 | SC-003 |
| Step 5 | SC-004 |
| Step 6 | SC-008 |
| Step 7 | SC-009 (desktop + mobile usability) |
| Step 8 | SC-006, SC-010 (tests run entirely in Docker) |
| Step 9 | SC-001 |
| Shared primitives used by the slice | SC-007 |
| Migrations auto-applied (step 1) | SC-005 |

## Teardown

```pwsh
docker compose down          # add -v to also drop the database volume
```
