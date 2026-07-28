# Implementation Plan: Self-Hosted Umami Analytics

**Branch**: `033-umami-analytics` | **Date**: 2026-07-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/033-umami-analytics/spec.md`

## Summary

Run Umami v3 (PostgreSQL build) as an in-cluster workload beside the existing app, with its data in a **new `umami` database on the existing Postgres StatefulSet** under its own scoped role.

The exposure is **split**, and this is the plan's one significant departure from what the spec assumed:

- **Measurement is same-origin.** The tracker script and the collection endpoint are proxied through the existing frontend nginx at renamed paths. This is what actually defeats blocklists, and it needs no new hostname. **Only `COLLECT_API_ENDPOINT` is a working runtime variable; `TRACKER_SCRIPT_NAME` has no effect in the image** (T001–T004, [research.md](./research.md) §2) — so **nginx** does the script renaming, and `TRACKER_SCRIPT_NAME` must not be set.
- **The dashboard gets its own hostname** (`analytics.juggerhub.com` / `analytics-dev.juggerhub.com`), because Umami's `BASE_PATH` — the only supported way to serve the dashboard from a subdirectory — is a **build-time** variable that would force us to fork and rebuild a third-party Next.js application. See [research.md](./research.md) §1.

The owner's stated preference ("the tracker should work and not get blocked but I don't really care where the URL of the dashboard is") is satisfied exactly by this split: the part they cared about is same-origin, and the part they didn't care about is where it is cheap and safe to put it.

Per-environment configuration reaches the already-built frontend image at **container start**, via the official nginx image's own `envsubst` template mechanism plus `sub_filter` — no rebuild, and no shell script added to the repository.

**No backend changes.** This feature does not touch `backend/` at all.

## Technical Context

**Language/Version**: No application language changes. Umami v3.x is a Next.js 15 / Node 22 application consumed as a pre-built container image; the repository's own change surface is Terraform (HCL), nginx configuration, Docker Compose, and one HTML template line.

**Primary Dependencies**: `docker.umami.is/umami-software/umami:postgresql-v3.x` (pinned to major per constitution "Dependency Management"), the existing `postgres:18.3-alpine` StatefulSet, the existing `nginx:1.31.3-alpine` frontend runtime, existing ingress-nginx + cert-manager.

**Storage**: A new `umami` database on the existing in-cluster Postgres instance. No new PersistentVolumeClaim. Owned by a new non-superuser role that cannot reach the application database.

**Testing**: No unit-test surface — this feature adds no application code paths. Verification is the [quickstart.md](./quickstart.md) scenario list, run against the local compose profile first and then against Dev. The existing Playwright suite must be confirmed unaffected (the injected snippet changes `index.html`).

**Target Platform**: AKS (Dev, Prod) and Docker Compose (local, opt-in profile).

**Project Type**: Web application — infrastructure and frontend-shell change only.

**Performance Goals**: Tracker adds ≤100 ms to p95 page load (SC-007). Umami is a low-traffic internal tool; 1 replica in Dev, 2 in Prod.

**Constraints**: Measurement must never block render or delay the app (FR-011, FR-012); analytics must not be able to starve the shared database (FR-014); the same frontend image must serve every environment (FR-020).

**Scale/Scope**: A community platform in early life — page views per day in the hundreds to low thousands. Umami handles this comfortably on ~256 Mi.

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1 design.*

| # | Gate | Verdict | Notes |
|---|------|---------|-------|
| 1 | **Architecture** (thin controllers, DI services, no repository layer, EF projections) | **N/A** | No backend code. |
| 2 | **Data access** (pagination, projections, `AsNoTracking`, `BaseEntity`) | **N/A** | No EF entities. Umami owns its own schema via Prisma, in its own database. |
| 3 | **Security review** (OWASP, never trust the client, no leaked secrets) | **PASS, with attention** | Adds an internet-facing admin login (User Story 5). Mitigations: default credentials neutralised at deploy time, `APP_SECRET` from GitHub Environments, scoped DB role, datastore stays ClusterIP. See "Security posture" below. |
| 4 | **Auth** (httpOnly cookies, backend-sourced password policy) | **N/A, separate realm** | Umami runs its own JWT session auth. It is deliberately *not* integrated with platform Identity — see [research.md](./research.md) §7. |
| 5 | **Conventions** (separate `.html`/`.css`/`.ts`; `.ps1` scripts only) | **PASS** | No new frontend component. **No `.sh` file is added** — the nginx image already ships `/docker-entrypoint.d/20-envsubst-on-templates.sh` and we only supply a `.template` config. Verified against the image. |
| 6 | **Environment parity** (identical across local/Dev/Prod; `.env` local, GitHub Environments deployed) | **PASS** | Same resource set in all three; only sizing and hostnames differ, via `envs/<env>.tfvars`. Local runs the same image under an opt-in compose profile. |
| 7 | **UI/Design compliance** (DESIGN.md + UI review checklist) | **N/A** | Ships no UI. The only frontend change is an injected `<script>` in the document head; nothing renders. No `checklists/ui-review.md` is instantiated. |
| 8 | **Resilience** (Principle VII — bounded waits, transient-only retry, stop conditions) | **PASS** | The tracker is fire-and-forget: no retry, no user-visible failure, no blocking. This is the *correct* reading of Principle VII here — see "Resilience posture" below. |

### Security posture (Gate 3 detail)

The genuine new risk is an internet-reachable administrative login on a security-first platform. Controls, all mandatory:

- **Default credentials neutralised as part of deployment**, not as a remembered manual step — Umami seeds `admin`/`umami` on first migration and offers no environment variable to change it ([research.md](./research.md) §4). A post-deploy Job sets the hash from a secret.
- **`APP_SECRET` from GitHub Environments** via `TF_VAR_umami_app_secret`, never in tfvars. Umami signs session tokens with it; a default or leaked value forges sessions.
- **Scoped database role** — `umami` owns only the `umami` database and is explicitly revoked from the application database (FR-025). It is not a superuser.
- **Datastore unchanged and unexposed** — the Postgres Service stays headless/ClusterIP (FR-024).
- **Session replay and web vitals stay OFF.** Umami v3 introduced session replay; on an authenticated-only platform it would capture member data and destroy every privacy property this spec claims. Treated as a release gate, not a preference.
- **`DISABLE_TELEMETRY=1`** so the instance makes no outbound call-home (FR-009: nothing leaves for a third party).

### Resilience posture (Gate 8 detail)

Principle VII requires bounded waits and forbids retry without a stop condition. For measurement the correct application is **no retry at all**:

- The tracker is loaded `async` and never blocks render (FR-012).
- A failed beacon is dropped silently. Retrying analytics would be exactly the amplification Principle VII prohibits — the payload is worthless and the volume is per-pageview.
- nginx proxy timeouts on the analytics locations are set short and explicit, so a hung Umami cannot tie up frontend worker connections. This is the "nothing waits forever" clause applied to a proxy hop.
- The pod carries readiness/liveness probes so a wedged instance is replaced rather than left absorbing traffic.

No circuit breaker is warranted: there is no server-to-server call, and the browser hop already fails open.

### Post-Phase 1 re-evaluation

Re-checked after design. **No new violations.** Gate 5 was the one at risk (a container entrypoint script would have had to be `.sh`, which the constitution forbids repository-wide); the nginx image's built-in template mechanism removes the need to add one, which is why that mechanism was chosen over a custom entrypoint. No entries in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/033-umami-analytics/
├── plan.md              # This file
├── research.md          # Phase 0 — the eight decisions that shape this design
├── data-model.md        # Phase 1 — Umami's schema boundary + the DB/role provisioning contract
├── quickstart.md        # Phase 1 — end-to-end verification scenarios
├── contracts/
│   ├── nginx-routes.md      # Path contract: what is proxied where, and why the names matter
│   ├── tracker-snippet.md   # The injected head snippet + its DNT/GPC guard
│   └── configuration.md     # Every env var / tfvar / secret, and where each comes from
├── checklists/
│   └── requirements.md  # From /speckit-specify
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
infra/
├── modules/app/
│   ├── main.tf              # + umami Deployment, Service, ingress rule, DB-provisioning
│   │                        #   initContainer, admin-password Job; + analytics env on the
│   │                        #   frontend container
│   ├── variables.tf         # + umami image/tag, replicas, hostname, website id, secrets
│   └── outputs.tf           # + dashboard URL
├── envs/
│   ├── dev.tfvars           # analytics-dev.juggerhub.com, 1 replica, dev website id
│   └── prod.tfvars          # analytics.juggerhub.com,     2 replicas, prod website id
├── variables.tf             # + root passthrough vars
└── main.tf                  # + wire new vars into module "app"

frontend/
├── nginx.conf               # DELETED — becomes the template below
├── nginx.conf.template      # NEW: existing config + analytics locations + sub_filter
└── Dockerfile               # COPY target changes to /etc/nginx/templates/default.conf.template

docker-compose.yml           # + umami service under the `analytics` profile; frontend env vars
.env.sample                  # + documented analytics variables

.github/workflows/deploy.yml # + TF_VAR_umami_* secret plumbing (both dev and prod jobs)
```

**Structure Decision**: Infrastructure-and-shell change. `backend/` is untouched. `frontend/` changes only in its nginx runtime layer and `Dockerfile` — **no Angular source file changes**, which keeps the tracker out of the application bundle and guarantees it cannot delay first render (FR-012).

## Key design decisions

Full reasoning in [research.md](./research.md); the load-bearing ones:

1. **Split exposure** (§1) — tracker same-origin, dashboard on its own hostname. Avoids forking Umami to get a build-time `BASE_PATH`.
2. **Renamed measurement paths** (§2) — `COLLECT_API_ENDPOINT` is a runtime variable and does the collection-path rename; `TRACKER_SCRIPT_NAME` is **not** effective, so nginx renames the script via `proxy_pass` path mapping. Names must avoid the tokens blocklists match (`analytics`, `track`, `stat`, `umami`, `collect`).
3. **Runtime frontend config via nginx templates + `sub_filter`** (§3) — the one mechanism that satisfies FR-020 without a rebuild, without an Angular change, and without adding a `.sh` file.
4. **DB provisioning by initContainer, not initdb** (§5) — `/docker-entrypoint-initdb.d/` runs **only on an empty data directory**. Dev and Prod are already initialised, so that route is a silent no-op. This is the single most likely way to get this wrong.
5. **Admin password set by a post-deploy Job** (§4) — Umami exposes no environment variable for it.
6. **Prisma 6.18 against PostgreSQL 18 is unverified upstream** (§6) — the top technical risk; gated behind local verification before any cluster work.

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| **Umami v3's Prisma 6.18 does not support PostgreSQL 18.** Upstream confirmation exists only for Prisma 7.2. | Medium | **Blocks the feature** | Verify first, in the local compose profile, against the identical `postgres:18.3-alpine` image, before any Terraform work. This is exactly what the local-parity requirement buys. If it fails: pin Umami to a release whose Prisma supports PG18, or escalate the shared-instance decision. |
| `envsubst` mangles nginx runtime variables (`$host`, `$uri`, …) in the template. | Medium | Broken proxying | The nginx entrypoint only substitutes names present in the environment. All placeholders are prefixed `JH_` so they cannot collide. Verified by diffing the rendered config in quickstart. |
| Chosen tracker paths still match a blocklist rule. | Low | Silent under-measurement | Avoid the matched token set; verify with a real blocker enabled (quickstart scenario 4), and re-check against SC-002 using the nginx access log. |
| Analytics write load degrades the app database. | Low | App slowdown | Low volume; Postgres resource limits already bound the StatefulSet. Revisit if page views grow an order of magnitude. |
| Session replay enabled by accident on an authenticated-only platform. | Low | **Severe privacy breach** | Explicit release gate in quickstart; verified as OFF per website. |
| Website IDs must be created by hand in the dashboard before measurement works. | Certain | Bootstrap friction | Documented as an ordered bootstrap in quickstart. IDs are **not secret** (they ship in page source), so they live in `envs/*.tfvars`, not in GitHub secrets. |

## Spec drift

One requirement is amended by this plan; [spec.md](./spec.md) has been updated to match:

- **FR-015** previously read that analytics must be served from the application's own address "requiring no additional domain name, DNS record, or certificate". Discovering that `BASE_PATH` is build-time makes the dashboard half of that clause purchasable only by forking Umami. FR-015 is now scoped to **measurement** — which is what the requirement was protecting (FR-016, SC-002) — and the dashboard hostname is stated as an accepted cost. **One DNS A record per environment**, pointing at the existing static IP, is a manual prerequisite; the certificate is automatic via the existing cert-manager issuers.

One requirement needs more than the product provides:

- **FR-007** requires honouring Do Not Track **and** Global Privacy Control. Umami's `data-do-not-track` covers DNT only ([research.md](./research.md) §8). GPC is handled by the injected guard in [contracts/tracker-snippet.md](./contracts/tracker-snippet.md). No spec change needed — the requirement is met, just not entirely by the product.

## Complexity Tracking

> No constitution violations. Table intentionally empty.
