---

description: "Task list for 033-umami-analytics"
---

# Tasks: Self-Hosted Umami Analytics

**Input**: Design documents from `/specs/033-umami-analytics/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: No unit-test tasks. This feature adds **no application code** — no backend, no Angular source (unless T018 fires). Verification is the scenario list in [quickstart.md](./quickstart.md), referenced per task. The existing Jest and Playwright suites are confirmed unaffected in T055.

**Organization**: Grouped by user story. One deliberate deviation: the **same-origin plumbing lives in Foundational, not in US3**, because the renamed proxy paths are shared by US1 (the tracker must reach Umami) and US3 (blocker survival). Splitting them would create a cross-story dependency. US3 then owns the naming discipline and the blocker verification, which *is* independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)

## Path Conventions

Infrastructure-and-shell feature. Real paths: `infra/`, `frontend/`, `docker-compose.yml`, `.env.sample`, `.github/workflows/`. **`backend/` is not touched.**

---

> ### Owner decision: the dashboard is publicly reachable from the first deploy
>
> The dashboard Ingress ships as part of **US1**, not as a later hardening step, and `kubectl port-forward` is not part of the routine workflow. The owner's reasoning: a freshly-deployed Umami holds no data, so the realistic worst case of a brief default-credential window is an attacker reaching an empty analytics instance.
>
> The window is closed anyway, at no cost: the **admin-password Job (T028) ships alongside the Ingress (T030)**, so the seeded `admin` / `umami` credential is overwritten *by the deployment* rather than by remembering to log in. This matters because cert-manager issuing the certificate publishes the new hostname to **Certificate Transparency logs**, which scanners watch precisely to find newly-provisioned hosts — so a manual "change it right after" would be racing automation rather than beating it.
>
> Net effect: public dashboard from day one, no `kubectl` in the loop, and no default-credential window at all.

---

## Phase 1: Setup & Risk Gate (BLOCKING)

**Purpose**: Answer the questions that could invalidate the design, before any code is written.

- [X] T001 Verify Umami v3 runs against `postgres:18.3-alpine` as a throwaway spike — **PASSED**. All 20 Prisma migrations applied cleanly against PostgreSQL 18.3. The risk was a false alarm: the image ships **Prisma 7.8.0**, not the 6.18.0 the release notes state, which is past the 7.2 threshold where PG18 support was confirmed ([research.md](./research.md) §6)
- [X] T002 Pin the Umami image to a specific tag (major pinned per constitution "Dependency Management") — **resolved: Umami 3.2.0, Prisma 7.8.0, Node 22.23.1, Next 16.2.6**. **Correction made in T005**: the tag is `docker.umami.is/umami-software/umami:**3.2.0**`, not `postgresql-v3.2.0`. v3 dropped both the `postgresql-` prefix and the `v` that every 1.x/2.x tag carried, so the expected-looking tag does not exist and fails at pull with a bare `not found`
- [X] T003 [P] Determine the readiness/liveness probe path for Umami v3 — **resolved: `/api/heartbeat` → 200** (`/api/health` and `/heartbeat` both 404). Recorded in [research.md](./research.md) open items
- [X] T004 [P] Determine the bcrypt format for the admin-password Job — **resolved: `$2b$`, cost 10, column `public."user"."password"` `varchar(60)`; `user` is a reserved word and must be quoted**. Recorded in [research.md](./research.md) open items. **T028 depends on this**

**Checkpoint**: PostgreSQL 18 compatibility proven. Image pinned. Probe path and hash format known rather than guessed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Local stack, the nginx template refactor, and the shared same-origin plumbing. No user-visible behaviour changes yet.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Add the `umami` service to `docker-compose.yml` under the `analytics` profile (mirroring the existing `spam` profile used by `spamd`), with `DATABASE_URL`, `APP_SECRET`, `COLLECT_API_ENDPOINT`, `DISABLE_TELEMETRY=1`, `DISABLE_UPDATES=1` per [contracts/configuration.md](./contracts/configuration.md), plus a healthcheck on `/api/heartbeat`. **Do not set `TRACKER_SCRIPT_NAME`** — verified to have no effect in the image. **Two corrections**: (a) the image tag is `3.2.0`, see T002; (b) `APP_SECRET` uses a local default, **not** the `:?` required-variable guard used for `JWT_SIGNING_KEY` — compose interpolates the whole file *regardless of profiles*, so `:?` aborted a plain `docker compose up` for anyone whose `.env` predates this feature, breaking FR-019 for a service that never starts for them. Verified healthy: migrations applied, `Updated tracker endpoint: /jh-insights/e`
- [X] T006 [P] Add the analytics variables to `.env.sample` with `JH_ANALYTICS_HEAD` **empty by default** so a plain start ships no tracker, per [contracts/configuration.md](./contracts/configuration.md)
- [X] T007 Rename `frontend/nginx.conf` → `frontend/nginx.conf.template` as a **pure refactor with zero behaviour change**, and update `frontend/Dockerfile` to `COPY nginx.conf.template /etc/nginx/templates/default.conf.template`
- [X] T008 Verify the refactor is inert: `docker compose up -d` (no profile), then `docker compose exec frontend nginx -t` and diff the rendered `/etc/nginx/conf.d/default.conf` against the previous `nginx.conf` — confirm `$host`, `$uri`, `$remote_addr`, `$proxy_add_x_forwarded_for`, `$scheme`, `$http_upgrade` all survive **verbatim** ([quickstart.md](./quickstart.md) Scenario 2). A mangled nginx variable here means an envsubst name collision — **PASSED: rendering the pre-route template produced a byte-identical file (same md5), so envsubst is a pure pass-through and all six variables survive. `nginx -t` in the running container succeeds; `/`, `/api/v1/health` and `/players` all still 200**
- [X] T009 Add the two analytics proxy locations to `frontend/nginx.conf.template` as **exact-match** locations that **rename via `proxy_pass` path mapping** — `= /jh-insights.js` → `/script.js` and `= /jh-insights/e` → `/api/send` — with short explicit `proxy_connect_timeout`/`proxy_send_timeout`/`proxy_read_timeout` and `proxy_next_upstream off`, per [contracts/nginx-routes.md](./contracts/nginx-routes.md). No `Upgrade`/`Connection` headers. Exact-match so `location /` cannot shadow them. **Deviation from the contract, since corrected there**: the upstream is reached through an nginx *variable* plus an explicit `resolver`, not a literal. nginx resolves a literal `proxy_pass` host at **startup** and aborts with `host not found in upstream`, so the contract's literal form meant the frontend could not boot whenever Umami was absent — which is the normal local state (FR-019) and would let a missing analytics Service take down the whole frontend in-cluster (constitution VII). Adds `JH_ANALYTICS_RESOLVER`, which has no safe empty default. Both failure and fix verified by rendering
- [X] T010 [P] Write the idempotent database provisioning SQL per [data-model.md](./data-model.md) §3 — role via `DO` block, database via the `\gexec` guard idiom (`CREATE DATABASE` cannot run in a transaction), password re-set on every run so a rotated secret takes effect, then revoke connect rights on `appdb`. **Two bugs found by running it, both of which would have failed inside the initContainer at deploy time**: (a) psql does **not** interpolate `:variables` inside a dollar-quoted `DO $$…$$` body, so `CREATE ROLE … PASSWORD :umami_password` failed with `syntax error at or near ":"` — the role is now created without a password and given one by the `ALTER` that has to run every time anyway; (b) **the isolation control did not work**: `REVOKE … FROM umami` reported `REVOKE` and changed nothing, because `umami` never held a direct grant — it reached `appdb` through the implicit grant to **PUBLIC**, which a role-scoped revoke leaves intact. Now `REVOKE CONNECT ON DATABASE appdb FROM PUBLIC`. Verified: three consecutive runs including a password rotation, then `umami` → `appdb` **refused**, `umami` → `umami` and app-superuser → `appdb` both still fine
- [X] T011 Wire the frontend service in `docker-compose.yml` with `JH_ANALYTICS_HEAD` and `JH_ANALYTICS_UPSTREAM` environment variables (the service currently has no `environment` block) — plus `JH_ANALYTICS_RESOLVER` per T009
- [X] T012 Confirm a plain `docker compose up -d` starts **no** `umami` container and the container count and memory match pre-feature ([quickstart.md](./quickstart.md) Scenario 1 — satisfies FR-019, SC-009) — **PASSED: default service set is `backend, database, frontend, mailpit, redis`, identical to pre-feature; `umami` appears only under `--profile analytics`**

**Checkpoint**: Local stack runs Umami on demand, the frontend is template-driven and provably unchanged, and the same-origin routes exist. Nothing is injected yet.

---

## Phase 3: User Story 1 - The owner can see what people actually use (Priority: P1) 🎯 MVP

**Goal**: Real page views, from the real application, visible in a dashboard at `analytics-dev.juggerhub.com` — no `kubectl` required.

**Independent Test**: Browse several pages of the running app, then open the dashboard and confirm those exact views appear with correct paths, counts, and timestamps.

### Local

- [X] T013 [US1] Add the `sub_filter '</head>' '${JH_ANALYTICS_HEAD}</head>'` injection with `sub_filter_once on` to `location /` in `frontend/nginx.conf.template` per [contracts/nginx-routes.md](./contracts/nginx-routes.md) — empty variable must be a no-op. **Verified both ways, and it exposed a defect in the snippet contract**: `sub_filter`'s argument is **single-quoted**, so the contract's single-quoted JavaScript terminated it early and nginx refused to start with `unexpected "1"` — i.e. enabling analytics would have taken the application down. The snippet must use **double quotes throughout**; [contracts/tracker-snippet.md](./contracts/tracker-snippet.md) and `.env.sample` corrected. Empty value renders `sub_filter '</head>' '</head>'` and the served page contains no tracker reference
- [X] T014 [US1] Compose the tracker snippet value exactly as specified in [contracts/tracker-snippet.md](./contracts/tracker-snippet.md) — `async` + `defer`, `data-website-id`, `data-do-not-track="true"`, the DNT/GPC guard, **no** `data-host-url`, **no** `identify()`, **no** `data-tag`. Lives in `docker-compose.yml` (not `.env`) so it is version-controlled and its **mandatory double-quoting** cannot be got wrong by hand; Terraform composes the deployed equivalent (T024)
- [X] T015 [US1] ~~Bootstrap locally~~ — **no longer applicable, and that is the point**. The two-phase bootstrap was removed: `docker compose --profile analytics up` provisions the database, role and website row itself, so there is nothing to sign in for, create, or copy. Verified from a dropped database and role. Enabling the tracker is a single variable, wired into the **"Docker Compose + Analytics"** VS Code terminal profile so it is scoped to that launch rather than leaking into a plain `docker compose up`
- [X] T016 [US1] Verify a full page load is recorded and that `/jh-insights.js` loads and `/jh-insights/e` returns 2xx, both on the app's own origin (DevTools Network) — **PASSED via HTTP**: `/jh-insights.js` → 200 `application/javascript` with `/jh-insights/e` baked into its body, `POST /jh-insights/e` → 200 and the row lands in `website_event`. `GET` on it → 405, which confirms it reaches Umami's `/api/send` rather than the SPA fallback or the .NET backend. **Not yet confirmed in a real browser's DevTools** — that is T017's run
- [X] T017 [US1] Verify **SPA navigation** produces one page view per screen without a document reload ([quickstart.md](./quickstart.md) Scenario 3 — FR-001, US1 scenario 2) — **PASSED in a real browser** (headless Chromium via Playwright against the running stack): 3 in-app navigations → **3 stored page views, 0 document reloads**, tracker script fetched **once**. The strongest evidence was incidental: the initial load recorded **two** views, `/` followed by `/sign-in?returnUrl=%2F`, because the **Angular auth guard's own `router.navigate` redirect** was captured — so this is the real router, not just a synthetic `pushState`. Source inspection confirms the tracker wraps `history.pushState`/`replaceState`, which is what Angular's default `PathLocationStrategy` uses
- [X] T018 [US1] ~~Conditional — only if T017 fails: explicit `umami.track()` on `NavigationEnd`~~ — **DID NOT FIRE.** T017 passed, so no Angular source is touched and the feature keeps its "no application code" property intact
- [X] T018a [US2] **Raised by T017 — owner decided: query strings are NOT recorded.** `url_query` was stored alongside `url_path`, so `/sign-in` kept `returnUrl=%2F`, and `returnUrl` carries deep links such as `/players/<handle>`. FR-008 covered *paths* only. Fixed with **`data-exclude-search="true"`**, which blanks `URL.search` **client-side before the beacon is sent**, so the value never leaves the browser rather than being discarded on arrival. Verified against the same Playwright run that found it: the beacon now carries `/sign-in` with no query and every stored `url_query` is empty, with page-view counting unaffected. Narrows the FR-008 personal-data surface to paths alone — relevant to GH #92
- [ ] T019 [US1] Verify the dashboard reports visitors, visits, page popularity, referrers, and device/browser/OS/country breakdowns over a selected range (FR-002, US1 scenarios 3–4)

### Terraform workload

- [ ] T020 [US1] Add the Umami Deployment to `infra/modules/app/main.tf` — image and tag from T002, replicas from a variable, `envFrom` the new ConfigMap and Secret, readiness/liveness probes on the T003 path, resource requests/limits (~256Mi/512Mi)
- [ ] T021 [US1] Add the `db-init` **initContainer** to the Umami Deployment in `infra/modules/app/main.tf`, running the T010 SQL as the superuser from the existing `postgres-secrets` Secret. **Not** `/docker-entrypoint-initdb.d/` — that only runs on an empty data directory and would be a silent no-op on the already-initialised Dev and Prod volumes ([research.md](./research.md) §5)
- [ ] T022 [US1] Add the `umami` ClusterIP Service (port 3000) to `infra/modules/app/main.tf`
- [ ] T023 [US1] Add the Umami ConfigMap and Secret to `infra/modules/app/main.tf` per [contracts/configuration.md](./contracts/configuration.md), composing `DATABASE_URL` from the DB password variable
- [ ] T024 [US1] Add the analytics environment variables to the **frontend** container spec in `infra/modules/app/main.tf` so the deployed image renders its template with the deployed values (FR-020) — `JH_ANALYTICS_HEAD` (**composed by Terraform** from `umami_website_id`, so the snippet is never hand-pasted and its mandatory double-quoting cannot be got wrong), `JH_ANALYTICS_UPSTREAM`, and `JH_ANALYTICS_RESOLVER`. For the resolver, prefer a `data` source reading the `kube-dns` Service ClusterIP in `kube-system` over a hardcoded IP; it has no safe empty default
- [ ] T025 [US1] Declare the new variables in `infra/modules/app/variables.tf` and pass them through `infra/variables.tf` and `infra/main.tf` per [contracts/configuration.md](./contracts/configuration.md)
- [ ] T026 [US1] Add `TF_VAR_umami_app_secret`, `TF_VAR_umami_db_password` and `TF_VAR_umami_admin_password_hash` to **both** the dev and prod jobs in `.github/workflows/deploy.yml` — missing one fails that environment at apply with an unset-variable error
- [ ] T027 [US1] Set the Dev values in `infra/envs/dev.tfvars` (1 replica, `analytics-dev.juggerhub.com`, and a **real, chosen `umami_website_id` from the start** — generate a UUID and commit it; it is not a secret and ships in page source). **No longer "initially empty"**: the website row is provisioned from this value (T028a), so the first apply measures immediately, per [contracts/configuration.md](./contracts/configuration.md)

### Public dashboard, with no default-credential window

- [ ] T028 [US1] Add the post-deploy Job to `infra/modules/app/main.tf` that writes the bcrypt hash from `umami_admin_password_hash` over the seeded `admin` account, using the T004 format. Must run **after** Umami has migrated and seeded the account, and must be idempotent ([research.md](./research.md) §4). **Ships with T030 so the credential is set by the deploy, not by hand**
- [ ] T028a [US1] Extend that same post-deploy Job to run `scripts/umami-seed-website.sql`, provisioning the tracked website row with the **chosen** `umami_website_id` (T027/T045). Same Job because it needs the same ordering — the `website` and `user` tables do not exist until Prisma has migrated and seeded, so it cannot go in the `db-init` initContainer. This is what removes the two-phase bootstrap; see [contracts/configuration.md](./contracts/configuration.md). Verified locally against a dropped database: provisions on first run, `INSERT 0 0` on re-run, and the resulting ID accepts beacons
- [ ] T029 [US1] Generate the bcrypt hash once and store it as `UMAMI_ADMIN_PASSWORD_HASH` in both GitHub Environments. The plaintext must never enter the repository, Terraform state, or the cluster
- [ ] T030 [US1] Add the dashboard Ingress and `analytics_hostname` variable to `infra/modules/app/main.tf`, with the cert-manager `ClusterIssuer` annotation. **Create the DNS A record pointing at the existing static public IP before applying**, or the HTTP-01 challenge fails
- [ ] T031 [US1] Apply to Dev and verify: pods ready, `kubectl -n juggerhub logs -l app=umami -c db-init` shows provisioning succeeded, `kubectl -n juggerhub get certificate` reports `Ready=True`, and the dashboard is reachable at `analytics-dev.juggerhub.com`
- [ ] T032 [US1] Verify `admin` / `umami` is **refused** immediately after the first apply (FR-022, SC-010, US5 scenario 1). If it succeeds, T028 did not run or ran before seeding — fix before proceeding, since the host is now public
- [ ] T033 [US1] Confirm Dev page views appear **after the first apply, with no second apply and no manual dashboard step** — the website row is provisioned by T028a from the ID committed in T027. If this needs a manual step, T028a did not run

**Checkpoint**: MVP. Real usage from Dev is visible on a public dashboard with a real certificate, reached in a browser, with no default credential ever having been live.

---

## Phase 4: User Story 2 - Visitors are measured without being identified (Priority: P2)

**Goal**: Prove the privacy properties hold in practice rather than in principle.

**Independent Test**: Complete a browsing session, then inspect browser storage and stored rows for anything identifying the viewer; repeat with DNT and with GPC.

- [ ] T034 [P] [US2] Verify zero cookies, localStorage and sessionStorage entries are set by analytics after a full session ([quickstart.md](./quickstart.md) Scenario 6 — FR-006, SC-003)
- [ ] T035 [P] [US2] Verify Do Not Track produces zero recorded events ([quickstart.md](./quickstart.md) Scenario 5 — FR-007, SC-004)
- [ ] T036 [US2] Verify Global Privacy Control produces **no request to `/jh-insights.js` at all** — not merely an ignored one. The guard must run before injection ([quickstart.md](./quickstart.md) Scenario 5)
- [ ] T037 [US2] Inspect the stored `website_event` and `session` rows and confirm no column holds a full IP address and none links to a platform member ([quickstart.md](./quickstart.md) Scenario 6 — FR-005, SC-005). Confirm rather than assume; page paths appearing verbatim is the one expected exception (FR-008) — **and `url_query` is a second one that FR-008 did not anticipate, see T018a**
- [ ] T038 [US2] **Release gate**: confirm session replay, **heatmaps** and web vitals are OFF for every website ([quickstart.md](./quickstart.md) Scenario 7). Umami v3 ships all three (migrations `19_add_session_replay`, `20_add_heatmap`). On an authenticated-only platform each would capture member data and invalidate every privacy claim in the spec
- [ ] T039 [US2] Confirm `DISABLE_TELEMETRY=1` is in effect and the instance makes no outbound call-home (FR-009)

**Checkpoint**: The privacy claims in the spec are evidenced, not asserted.

---

## Phase 5: User Story 3 - Measurement that isn't silently thrown away (Priority: P2)

**Goal**: The numbers are broadly complete, so conclusions drawn from them are safe.

**Independent Test**: With a mainstream content blocker enabled, browse several pages and confirm the views still appear.

- [X] T040 [P] [US3] Audit the chosen path names against the forbidden token list in [contracts/nginx-routes.md](./contracts/nginx-routes.md) (`analytics`, `track`, `stat`, `collect`, `beacon`, `pixel`, `umami`, …) and confirm neither contains one — **PASSED**: `/jh-insights.js` and `/jh-insights/e` contain none, and the served `script.js` body was scanned for leaked default paths (`send`/`collect`/`track`) with no hits, so nothing the browser requests carries a blocked token. Whether that is *sufficient* against real blocklists is T041
- [ ] T041 [US3] Verify measurement survives uBlock Origin and Brave Shields ([quickstart.md](./quickstart.md) Scenario 4 — US3 scenario 1). If blocked, rename and repeat
- [ ] T042 [P] [US3] Confirm **measurement** required no additional domain, DNS record, or certificate (FR-015 as amended, US3 scenario 2) — the dashboard hostname is the accepted exception and does not affect measurement
- [ ] T043 [US3] Verify analytics failure is invisible: stop Umami, browse, confirm normal load times, no visible error, and **no retry** of the failed beacon ([quickstart.md](./quickstart.md) Scenario 8 — FR-011, FR-013, SC-006, constitution Principle VII)
- [ ] T044 [US3] Establish the SC-002 measurement method — compare recorded page views against the nginx access log for the same window and confirm ≥90% capture. **Must account for Umami's bot filtering, or this will read as data loss that isn't there**: Umami runs `isbot` over the User-Agent and answers a detected bot with **HTTP 200 and `{"beep":"boop"}` while storing nothing**. A 200 is therefore *not* evidence a view was captured — discovered in T017, where five beacons all returned 200 and the database stayed empty until the headless UA was overridden. nginx logs every one of those requests, so crawler traffic inflates the denominator

**Checkpoint**: Measurement is trustworthy and its failure mode is harmless.

---

## Phase 6: User Story 4 - Development traffic never pollutes the real numbers (Priority: P3)

**Goal**: Local and Dev traffic never appears in the figures used to make decisions.

**Independent Test**: Generate traffic locally and in Dev, then confirm production figures show none of it.

- [ ] T045 [US4] Set the Prod values in `infra/envs/prod.tfvars` — `analytics.juggerhub.com`, 2 replicas, and a **distinct chosen `umami_website_id`** (a different UUID from Dev is what keeps the environments separate — FR-018, SC-008). Only sizing, hostname and website ID differ from Dev; the resource set is identical (constitution Principle V)
- [ ] T046 [US4] Confirm the **same released frontend image** renders different analytics configuration in Dev and Prod with no rebuild (FR-020, US4 scenario 4)
- [ ] T047 [US4] Verify a separate website ID per environment and that Dev events land only under the Dev website (FR-018, SC-008, US4 scenario 1)

**Checkpoint**: Environments are cleanly separated and the build artifact is genuinely environment-agnostic.

---

## Phase 7: User Story 5 - The dashboard is not a new way into the platform (Priority: P3)

**Goal**: Confirm the exposed login surface cannot reach member data. The credential control itself shipped in US1 (T028–T032); what remains is proving the blast radius is contained.

**Independent Test**: Confirm the analytics credentials cannot read application data and the datastore is unreachable from outside the cluster.

- [ ] T048 [P] [US5] Verify the `umami` role can connect to its own database and is **refused** on `appdb` ([quickstart.md](./quickstart.md) Scenario 9 — FR-025, US5 scenario 3). Expect this to be the failure mode if the explicit `REVOKE` was omitted, since PostgreSQL grants `CONNECT` to `PUBLIC` by default
- [ ] T049 [P] [US5] Verify `\du umami` shows no `Superuser`, no `Create DB`, no `Create role`, and that `kubectl -n juggerhub get svc postgres` remains headless/ClusterIP with no LoadBalancer or NodePort (FR-024, US5 scenario 4)
- [ ] T050 [P] [US5] Confirm no analytics secret appears in any version-controlled file — tfvars, compose, workflows, templates (US5 scenario 2)
- [ ] T051 [US5] Confirm dashboard sessions expire rather than remaining valid indefinitely (FR-026)

**Checkpoint**: A compromise of the dashboard cannot reach member data.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T052 [P] Document the analytics stack in `README.md` — the opt-in `analytics` compose profile, the two-phase website-ID bootstrap, and the dashboard hostnames
- [ ] T053 [P] Document the DNS A record per environment as a manual deployment prerequisite in `infra/README.md`
- [ ] T054 [P] Confirm `.env.sample` documents every new variable and that a fresh clone starts cleanly with analytics off
- [ ] T055 Run the existing suites and confirm the injected snippet broke nothing: `docker compose -f docker-compose.test.yml up --abort-on-container-exit` and `docker compose -f docker-compose.e2e.yml up --abort-on-container-exit` ([quickstart.md](./quickstart.md) Scenario 10)
- [ ] T056 Run `terraform fmt -check` and `tflint` against `infra/` per the existing `terraform-ci.yml` gate
- [ ] T057 Run the full [quickstart.md](./quickstart.md) scenario list end to end against Dev
- [ ] T058 Apply to Prod and confirm measurement (SC-001, SC-002) — **one apply**, since the Prod website ID is committed in T045 and provisioned by T028a
- [ ] T059 Update [research.md](./research.md) open items 1–4 with their resolved answers so the record reflects what was learned, not what was assumed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup & Risk Gate)**: no dependencies. **T001 blocks everything** — if PostgreSQL 18 is incompatible, the design changes.
- **Phase 2 (Foundational)**: depends on Phase 1. Blocks all user stories.
- **Phase 3 (US1)**: depends on Phase 2. The MVP, and it ships the public dashboard.
- **Phases 4–7 (US2, US3, US4, US5)**: all depend on Phase 3, and are independent of each other — fully parallelisable.
- **Phase 8 (Polish)**: T058 (Prod) depends on US2's release gate (T038) and US5 completing.

### Critical path

```text
T001 (PG18 gate) → T005–T012 (foundational) → T013–T033 (US1: local → Terraform → public dashboard)
                                                  ├→ US2 (T034–T039) ─┐
                                                  ├→ US3 (T040–T044) ─┤
                                                  ├→ US4 (T045–T047) ─┼→ T057 → T058 (Prod)
                                                  └→ US5 (T048–T051) ─┘
```

### Within US1

- **T004 → T028**: the Job cannot be written without knowing the hash format.
- **T028 + T029 → T030**: the Ingress goes up *with* the password control in place, so no default credential is ever publicly reachable. This costs nothing — the Job was always planned — and removes the need to react quickly after a certificate hits the Certificate Transparency logs.
- **T031 → T032**: verify the credential is dead immediately after the first apply, before moving on.
- Local verification (T013–T019) before deployed work (T020+): the local stack runs the *identical* Postgres image, so it answers questions faster and more cheaply.
- T018 fires only if T017 fails.

### Parallel Opportunities

- **Phase 1**: T003 and T004 in parallel once T001 passes.
- **Phase 2**: T006 and T010 in parallel with T005/T007.
- **Phase 4**: T034 and T035 in parallel.
- **Phase 5**: T040 and T042 in parallel.
- **Phase 7**: T048, T049, T050 all in parallel.
- **Phase 8**: T052, T053, T054 in parallel.
- **Across stories**: once Phase 3 lands, US2 / US3 / US4 / US5 are independent.

---

## Parallel Example: Phase 7 (US5)

```bash
# All three audits are independent reads against a deployed system:
Task: "T048 Verify umami role is refused on appdb (quickstart Scenario 9)"
Task: "T049 Verify role privileges and that postgres Service stays ClusterIP"
Task: "T050 Confirm no analytics secret is in any version-controlled file"
```

---

## Implementation Strategy

### MVP (Phase 1 → 2 → 3)

Delivers real usage data from Dev on a public, certificate-backed dashboard. **Stop and validate here.** This is where the design either works or reveals that it doesn't.

### Real delivery increments

Story order is the filing structure; the *delivery* path is environmental:

1. **Local works** (T001–T019) — the cheapest place to be wrong.
2. **Dev works, publicly reachable, credential already rotated** (T020–T033).
3. **Verified** (US2, US3, US4, US5) — privacy, reach, separation and blast radius proven on a running system.
4. **Prod** (T057, T058).

### Where this is likely to go wrong

- **T001** is the real risk. Ten minutes here saves discovering an incompatibility after the Terraform is written.
- **T008** catches the envsubst collision class of bug, which is otherwise diagnosed by confusing proxy failures.
- **T021** — reaching for `/docker-entrypoint-initdb.d/` is the intuitive move and it fails silently on already-initialised volumes.
- **T032** — if this fails, the host is already public. Fix before continuing rather than filing it.
- **T048** — a fresh Postgres role is *not* isolated by default.
- **T017** may turn a one-line task into an Angular change (T018). Do not assume auto-tracking follows the Angular router.

---

## Notes

- `[P]` = different files, no dependencies on incomplete tasks.
- No test tasks: this feature adds no application code. T055 confirms the existing suites still pass.
- No `checklists/ui-review.md`: this feature ships no UI (constitution Gate 7 recorded N/A in [plan.md](./plan.md)).
- Commit after each task or logical group; small commits per CLAUDE.md.
- Every task that says "verify" has a named quickstart scenario — none of them are "look at it and see if it seems fine".
