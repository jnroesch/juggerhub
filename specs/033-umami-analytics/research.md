# Phase 0 Research: Self-Hosted Umami Analytics

**Feature**: `033-umami-analytics` | **Date**: 2026-07-28

Eight decisions. Sources are linked at the bottom; every claim about Umami behaviour below was checked against the official documentation or against the running image rather than recalled.

---

## 1. Exposure: split — measurement same-origin, dashboard on its own hostname

**Decision**: Proxy the **tracker script and collection endpoint** through the existing frontend nginx on the application's own origin. Serve the **dashboard** from `analytics.juggerhub.com` (Prod) / `analytics-dev.juggerhub.com` (Dev) via a second ingress rule on the existing controller and static IP.

**Rationale**: The spec asked for everything same-origin. Research overturned the assumption that made this cheap:

> **`BASE_PATH` is a build-time variable.** Umami's docs classify it under "Build-Time Variables … set before application build". Serving the dashboard from `juggerhub.com/analytics` therefore requires `docker build --build-arg BASE_PATH=/analytics` against Umami's source — i.e. **forking and continuously rebuilding a third-party Next.js application**.

By contrast `TRACKER_SCRIPT_NAME` and `COLLECT_API_ENDPOINT` are **runtime** variables. So the half of the feature that actually matters for FR-016 and SC-002 — measurement not being dropped by blocklists — is fully achievable with the stock image and zero new hostnames. The dashboard's location has **no effect whatsoever** on measurement completeness: a blocklist entry for `analytics.juggerhub.com` would only inconvenience the owner viewing their own dashboard.

This matches the owner's stated preference precisely: *"the tracker should work and not get blocked but I don't really care where the URL of the dashboard is."*

**A second, independent blocker** to a same-origin dashboard: Umami's dashboard calls its own API under `/api/…`, which on this origin is already the .NET backend ([frontend/nginx.conf](../../frontend/nginx.conf) proxies `/api/` to `backend:8080`). Even setting `BASE_PATH` aside, hosting the dashboard at the app origin means colliding with a path the platform already owns. `COLLECT_API_ENDPOINT` renames only the collection endpoint, not the dashboard's API surface.

**Alternatives considered**:

- *Fork and rebuild Umami with `BASE_PATH`* — rejected. Adds a build pipeline for a third-party app, ongoing responsibility for tracking upstream releases and security patches, and image drift, all to relocate a URL the owner explicitly does not care about. Poor trade for a solo-maintained project.
- *Dashboard not exposed at all; `kubectl port-forward` only* — genuinely the most secure option and it needs no DNS record. Rejected as the default because it makes routine use require cluster credentials. **It remains available at any time with no configuration change**, and is the recommended fallback if the owner would rather not run an internet-facing admin login. Documented in quickstart.
- *Everything on the subdomain, including the tracker* — rejected: it is the option that loses measurement, which is the whole point.

**Cost accepted**: one DNS A record per environment pointing at the existing static public IP. The TLS certificate is automatic — the existing cert-manager `ClusterIssuer`s already handle HTTP-01 for this ingress class.

---

## 2. Defeating blocklists: nginx renames, `COLLECT_API_ENDPOINT` keeps the script in sync

> **REVISED after empirical verification (T001 spike).** The original plan relied on both variables working at runtime, as the documentation classifies them. Only one does, and not in the way the docs suggest. The design still works — nginx carries more of it.

**Verified behaviour of the official `postgresql-latest` image (Umami 3.2.0):**

| Variable | Documented | Actual |
|---|---|---|
| `TRACKER_SCRIPT_NAME` | runtime | **No effect.** `/script.js` still serves; the configured path 404s. The image is a Next.js *standalone* build with no `next.config.js` on disk — the rewrites were baked at build time. Effectively build-time, like `BASE_PATH`. |
| `COLLECT_API_ENDPOINT` | runtime | **Works, but only halfway.** `scripts/update-tracker.js` runs at container start and rewrites the *contents* of `public/script.js`, substituting the value for `/api/send`. The server still routes `/api/send` only — **no route is created at the new path**. |

**Decision**: nginx owns the renaming, via `proxy_pass` path mapping:

```nginx
location = /jh-insights.js { proxy_pass http://umami:3000/script.js; }
location = /jh-insights/e  { proxy_pass http://umami:3000/api/send; }
```

The script served at our `/jh-insights.js` contains `/jh-insights/e` as its collection URL (because `COLLECT_API_ENDPOINT` rewrote its contents), and nginx maps that back to Umami's real `/api/send`. Both names are ours; neither appears in a blocklist rule.

**`COLLECT_API_ENDPOINT` remains mandatory, and this is the important part.** Without it the tracker would POST to `/api/send` **on our own origin** — which [frontend/nginx.conf](../../frontend/nginx.conf) routes to the .NET backend. That is a live collision: measurement would be fired at the application API. Setting it is what moves the beacon off a path we already own.

This arrangement is also strictly better than the original: nginx renaming means the paths can change without touching Umami's configuration or restarting it.

**Rationale**: Serving first-party is necessary but **not sufficient**. Mainstream blocklists match on path as well as host — Umami's defaults `/script.js` and `/api/send` are both matched by generic rules regardless of origin. Umami provides these two runtime variables for exactly this purpose; the docs describe `TRACKER_SCRIPT_NAME` as being there "to help you avoid some ad blockers".

Names must avoid the tokens that rules key on: `analytics`, `analytic`, `track`, `tracker`, `stat`, `stats`, `telemetry`, `collect`, `beacon`, `pixel`, `umami`, `plausible`, `matomo`. `jh-insights` carries none of them and is site-specific, which is what makes it hard to write a general rule against.

**Note**: `data-host-url` is **not** needed. Umami's default is to send data to wherever the script is hosted — and the script is hosted on our origin, so the default is already correct. One less attribute to keep in sync.

**Alternatives considered**: keeping the defaults and accepting the loss (rejected — silently undermines SC-002 and every conclusion drawn from the data); rotating the paths periodically (rejected — breaks caching and buys little at this scale).

---

## 3. Per-environment config without rebuilding: nginx templates + `sub_filter`

**Decision**: Rename `frontend/nginx.conf` → `frontend/nginx.conf.template`, copy it to `/etc/nginx/templates/default.conf.template`, and inject the tracker snippet into `index.html` with `sub_filter` on `</head>`. All placeholders are prefixed `JH_`.

**Rationale**: FR-020 requires one built artifact per release working in every environment. The frontend is a **static SPA baked into an nginx image** ([frontend/Dockerfile](../../frontend/Dockerfile)) and the repository has **no `environment.ts` files at all** — there is no existing runtime-configuration mechanism to extend. Something new was required.

Both halves of this mechanism are already present in the pinned image, verified directly:

```
$ docker run --rm nginx:1.31.3-alpine nginx -V
  ... --with-http_sub_module          # sub_filter is compiled in
$ ls /docker-entrypoint.d/
  20-envsubst-on-templates.sh         # the image's own template processor
```

This matters for **constitution Principle VI**, which forbids adding `.sh` scripts anywhere in the repository. A custom container entrypoint would have had to be a shell script — on Linux nginx, PowerShell is not an option — putting the obvious approach in direct conflict with the constitution. Using the image's built-in mechanism means **we add a `.template` file and no script at all**, so the rule holds without an exception.

**The envsubst collision trap**: nginx's entrypoint substitutes only variable names that exist in the environment, so nginx runtime variables (`$host`, `$uri`, `$remote_addr`, `$proxy_add_x_forwarded_for`, `$scheme`, `$http_upgrade`) normally survive untouched. "Normally" is doing work there — any environment variable that happens to share a name with an nginx variable would corrupt the config. Prefixing every placeholder `JH_` makes collision impossible rather than unlikely. The rendered config is diffed in quickstart to confirm.

**Disabled-by-default falls out naturally**: the injected snippet is carried in a single variable `JH_ANALYTICS_HEAD`. Empty means `sub_filter` replaces `</head>` with `</head>` — a no-op. No conditional nginx configuration, and local development gets no tracker unless the developer opts in.

**Alternatives considered**:

- *`APP_INITIALIZER` fetching `/config.json`* — rejected: adds a network round-trip before the app bootstraps, which risks FR-012, and touches Angular source for a concern that is not the app's.
- *Build one image per environment* — rejected: directly violates FR-020, and doubles build/push time.
- *Custom `/docker-entrypoint.d/` script* — rejected: would add a `.sh` file, violating Principle VI.

---

## 4. Neutralising the default administrator credential

**Decision**: A post-deploy Kubernetes Job updates the seeded `admin` account's password hash from `TF_VAR_umami_admin_password_hash` (a bcrypt hash held in GitHub Environments).

**Rationale**: FR-022 requires the shipped default to be unusable once deployment completes. Investigation found:

- Umami seeds `admin` / `umami` on first migration, and the docs say only *"Change the default password immediately after your first login."*
- **There is no environment variable for the admin password.** The documented environment-variable list has no such entry. The supported paths are the dashboard UI or direct database access.

A manual "remember to log in and change it" step is not a control — it is an intention, and it fails silently and invisibly. Encoding it as a deployment step makes it deterministic and re-asserted on every deploy.

The hash is **pre-generated once by the owner** and stored as a secret. The plaintext never enters the repository, Terraform state, or the cluster — only the bcrypt hash does. The Job is idempotent (setting the same hash twice is harmless) and must run **after** Umami's migration has seeded the account, so it depends on the Deployment becoming ready.

**Alternatives considered**: `DISABLE_LOGIN=1` (rejected — disables the login page entirely, which is for embedding scenarios and would leave the dashboard either unusable or unauthenticated); seeding a second admin and deleting `admin` (rejected — more moving parts, same outcome, and the delete is the step that would get skipped).

---

## 5. Provisioning the database and role — and why `initdb` will not work

**Decision**: An **initContainer** on the Umami Deployment runs an idempotent `psql` script against the existing Postgres service, creating the role and database if absent and revoking cross-database access.

**Rationale**: This is the most likely thing to get quietly wrong.

> The stock `postgres` image only executes `/docker-entrypoint-initdb.d/*` **when initialising an empty data directory**. Dev and Prod are already running with populated PVCs, so mounting an init script there would be a **silent no-op** — the deployment would appear to succeed and Umami would fail to connect, with nothing pointing at the cause.

An initContainer runs on every pod start, is naturally ordered before Umami, and is idempotent by construction. `CREATE DATABASE` cannot run inside a transaction or `DO` block, so the database is created with the `\gexec` guard idiom while the role uses a `DO` block; both are detailed in [data-model.md](./data-model.md).

**FR-025 (analytics cannot reach application data)** is enforced here: the `umami` role is not a superuser, owns only its own database, and is explicitly `REVOKE`d from connecting to the application database. Explicit revocation is used rather than relying on defaults, because PostgreSQL grants `CONNECT` to `PUBLIC` by default — the intuitive assumption that a fresh role "just can't see" another database is wrong.

**Alternatives considered**:

- *Terraform `postgresql` provider* — rejected: Terraform runs in GitHub Actions and the Postgres Service is ClusterIP-only, so it has no route to the database. Exposing it to make Terraform work would violate FR-024.
- *A separate `Job`* — workable, but Jobs are immutable in Terraform and need `replace` semantics when the script changes; the initContainer avoids that lifecycle entirely.
- *Running Umami as the `postgres` superuser* — rejected outright: violates FR-025 and would let a dashboard compromise reach every member record.

---

## 6. PostgreSQL 18 compatibility — **RESOLVED, and it was a false alarm**

**Status**: ✅ **Verified working.** All 20 Prisma migrations applied cleanly against `postgres:18.3-alpine` (PostgreSQL 18.3).

**What the risk was based on, and why it was wrong**: the GitHub release notes for v3.2.0 say Prisma was upgraded to **6.18.0**, and upstream confirmation of PostgreSQL 18 support exists only for **Prisma 7.2**. That gap is what made this the top risk.

Inspecting the actual image contradicts the release notes:

```text
umami            3.2.0
prisma           7.8.0      ← not 6.18.0
@prisma/client   7.8.0
node             v22.23.1
next             16.2.6
```

7.8.0 is comfortably past the 7.2 threshold. The release-notes summary was describing a state the shipped image had already moved past — which is exactly why this was sequenced as an empirical check rather than a documentation question.

**Lesson worth keeping**: the risk was real *as assessed from the available reading*, and cost ten minutes to retire. Reading further would not have settled it; only running it did.

---

## 7. Dashboard identity stays separate from platform Identity

**Decision**: Umami's own accounts, managed manually. No integration with Microsoft Identity or the `Admin__Emails` allowlist.

**Rationale**: Constitution Principle IV governs *platform* authentication; Umami is a self-contained third-party application with its own session model, and it is not subject to that principle any more than the Postgres console is. Wiring platform Identity into it would mean either running an OIDC provider or synchronising credentials — substantial machinery for a tool with exactly one user.

The spec already records that dashboard access is owner-only and deliberately not tied to the platform admin allowlist: not every platform admin needs usage figures, and coupling an external tool to the platform's authorisation model creates a dependency that is hard to unpick later.

**Consequence to hold onto**: this is a *second* credential store with a *second* set of session rules on an internet-facing surface. That is the price, and it is why §4's controls are mandatory rather than advisory.

---

## 8. Do Not Track is covered; Global Privacy Control is not

**Decision**: Set `data-do-not-track="true"` on the tracker **and** add a short inline guard that also checks `navigator.globalPrivacyControl`, only injecting the tracker when neither signal is present.

**Rationale**: Umami's `data-do-not-track` attribute is documented as "Respect user's Do Not Track browser setting" — DNT only. FR-007 requires DNT **and** GPC. GPC is a distinct, more recent signal exposed as `navigator.globalPrivacyControl`, and Umami has no attribute for it.

Placing the check in the injected head snippet rather than in Angular has three benefits: it runs before the tracker is ever requested (so a GPC user generates no network call at all, not merely an ignored one); it keeps the concern out of the application bundle; and it keeps everything analytics-related in one reviewable place. The guard is a few lines — see [contracts/tracker-snippet.md](./contracts/tracker-snippet.md).

**Alternatives considered**: relying on `data-do-not-track` alone (rejected — leaves FR-007 half-met); an Angular service (rejected — the tracker would already have loaded by then, and it puts analytics logic into application source for no gain).

---

## Open items — ALL RESOLVED by the T001 spike (2026-07-28)

| # | Item | Resolution |
|---|---|---|
| 1 | Does Umami v3 work on PostgreSQL 18? | ✅ **Yes.** All 20 migrations applied against PostgreSQL 18.3. The image ships Prisma **7.8.0**, not the 6.18.0 the release notes state. See §6. |
| 2 | Does the tracker auto-record Angular route changes? | ✅ **Yes — mechanism confirmed present.** The served tracker hooks both `pushState` and `replaceState`, which is what Angular's router uses. **T018 (the Angular fallback) is very unlikely to be needed**; still confirm end-to-end against the real app in T017, since presence of the hook is not proof of correct behaviour with this router. |
| 3 | Readiness/liveness probe path for v3 | ✅ **`/api/heartbeat`** → HTTP 200. (`/api/health` and `/heartbeat` both 404.) Unaffected by `COLLECT_API_ENDPOINT`, which renames only `/api/send`. The probe is pod-internal, so it never traverses our nginx and cannot collide with the app's `/api/`. |
| 4 | Bcrypt cost/format for the admin-password Job | ✅ **`$2b$`, cost 10.** Column `public."user"."password"`, `varchar(60)` — note `user` is a reserved word and must be quoted. The seeded default hash is `$2b$10$BUli0c.muyCW1ErNJc3jL.vFRFtFJWrT8/GcR4A.sUdCznaXiqFXa` (= `umami`), which is what T032 must confirm no longer works. |

### Also learned, unprompted

- **GPC is definitively absent from the tracker.** The served script contains `doNotTrack` but no `globalPrivacyControl` — confirming §8 by inspection rather than inference. The guard is required, not belt-and-braces.
- **Umami v3 ships session replay *and* heatmap** (migrations `19_add_session_replay`, `20_add_heatmap`). The privacy release gate in T038 must cover heatmaps too, not just replay — a heatmap on an authenticated-only platform records interaction positions on member data.
- The tracker is ~4.7 KB, so its page-weight cost against SC-007 is negligible.

## Sources

- [Umami — Environment Variables](https://docs.umami.is/docs/environment-variables) — the build-time vs runtime split (§1, §2, §3)
- [Umami — Installation](https://docs.umami.is/docs/install) — image name, minimum PostgreSQL, default credentials (§4, §6)
- [Umami — Tracker Configuration](https://docs.umami.is/docs/tracker-configuration) — `data-*` attributes, DNT wording (§2, §8)
- [Umami — Releases](https://github.com/umami-software/umami/releases) — v3 line, Prisma 6.18, Node 22, session replay (§6)
- [Prisma — PostgreSQL 18 Support discussion](https://github.com/prisma/prisma/discussions/28937) — Prisma 7.2 verified against PG18 (§6)
- Local verification: `docker run --rm nginx:1.31.3-alpine nginx -V` (§3)
