# Quickstart & Verification: Self-Hosted Umami Analytics

**Feature**: `033-umami-analytics` | **Date**: 2026-07-28

Runnable verification for this feature. Scenario 0 **gates everything else** — it answers the one question that could invalidate the whole design, and it takes ten minutes.

Prerequisites: Docker Desktop, repo checked out on `033-umami-analytics`, a populated `.env` (see `.env.sample`).

---

## Scenario 0 — PostgreSQL 18 compatibility (BLOCKING, do first)

**Why first**: Umami v3 ships Prisma 6.18. Upstream confirmation of PostgreSQL 18 support exists only for Prisma 7.2 ([research.md](./research.md) §6). If this fails, the shared-database decision cannot stand and the owner has a call to make — so it must be answered before any Terraform is written.

Run as a **throwaway spike with no repository changes** — the compose `analytics` profile does not exist yet (that is T005), and the whole point is to get the answer before writing code.

```powershell
docker network create umami-spike
docker run -d --name spike-db --network umami-spike `
  -e POSTGRES_PASSWORD=spike -e POSTGRES_DB=umami postgres:18.3-alpine
docker run -d --name spike-umami --network umami-spike -p 3001:3000 `
  -e DATABASE_URL=postgresql://postgres:spike@spike-db:5432/umami `
  -e APP_SECRET=spike-only-not-a-real-secret `
  docker.umami.is/umami-software/umami:postgresql-v3.2.0   # tag per T002
docker logs -f spike-umami
```

**Expect**: Prisma migrations apply cleanly and Umami reports listening on 3000.

**Failure looks like**: Prisma protocol or `pg_catalog` errors during migration. If so, **stop** — do not proceed to Terraform. Try a different pinned Umami tag; if none works, escalate to the owner, because every fallback reverses their shared-instance decision.

While it is up, harvest the two answers T003 and T004 need — the probe path and the seeded hash format:

```powershell
docker exec spike-umami wget -qO- http://localhost:3000/api/heartbeat   # T003: confirm or find the real path
docker exec spike-db psql -U postgres -d umami -c "SELECT username, password FROM \"user\";"   # T004: bcrypt cost/format
docker exec spike-db psql -U postgres -c "SELECT version();"            # confirm the server actually under test
```

Tear down when done — nothing here should survive into the repository:

```powershell
docker rm -f spike-umami spike-db; docker network rm umami-spike
```

---

## Scenario 1 — Local stack stays lightweight (FR-019, SC-009)

```powershell
docker compose down
docker compose up -d
docker compose ps
```

**Expect**: no `umami` container. Container count and memory match pre-feature. A plain start must be unaffected by this feature existing.

---

## Scenario 2 — Rendered nginx config is correct (FR-020)

The `envsubst` collision trap is the thing to catch here.

```powershell
docker compose --profile analytics up -d
docker compose exec frontend cat /etc/nginx/conf.d/default.conf
```

**Expect**:
- `${JH_ANALYTICS_*}` placeholders are substituted.
- nginx runtime variables survive **verbatim**: `$host`, `$remote_addr`, `$proxy_add_x_forwarded_for`, `$scheme`, `$http_upgrade`, `$uri`. If any of these are empty or mangled, an environment variable collided with an nginx variable name — see [nginx-routes.md](./contracts/nginx-routes.md).

```powershell
docker compose exec frontend nginx -t
```

---

## Scenario 3 — Page views recorded, including SPA navigation (FR-001, US1)

Bootstrap first: open `http://localhost:3000` (Umami), sign in, create a website for `localhost`, copy its ID into `.env` as `UMAMI_WEBSITE_ID`, rebuild `JH_ANALYTICS_HEAD`, and restart the frontend.

Then browse the app: land on the home page, then navigate **within the app** (no reload) across several screens.

**Expect**: each screen appears as its own page view in the dashboard.

**This is [research.md](./research.md) open item 2.** Umami auto-tracks by default, but the docs do not state whether it hooks `history.pushState`, and Angular's router navigates without a document load. If only the first page is recorded, auto-tracking does not follow the Angular router and the fallback (an explicit `umami.track()` on `NavigationEnd`) becomes real work touching Angular source — size it before committing.

```powershell
# Confirm the beacon is actually reaching the collection endpoint
docker compose logs frontend | Select-String "jh-insights"
```

---

## Scenario 4 — Measurement survives a content blocker (US3, SC-002)

In a browser with uBlock Origin (or Brave Shields) enabled, browse several pages.

**Expect**: views still recorded. Also confirm in DevTools → Network that `/jh-insights.js` loads and `/jh-insights/e` returns 2xx, both on the app's own origin.

**If blocked**: the chosen path names match a filter rule. Rename, avoiding the token list in [nginx-routes.md](./contracts/nginx-routes.md).

---

## Scenario 5 — Do Not Track and Global Privacy Control (FR-007, SC-004)

DNT: enable "Send Do Not Track" in the browser, browse, confirm **zero** new events.

GPC: with DevTools open before page load —

```js
Object.defineProperty(navigator, 'globalPrivacyControl', { value: true });
```

then reload and browse.

**Expect**: **no request to `/jh-insights.js` at all** — not merely an ignored one. The guard runs before injection ([tracker-snippet.md](./contracts/tracker-snippet.md)), so the network tab should show nothing.

---

## Scenario 6 — Privacy properties of stored data (FR-005, FR-006, SC-003, SC-005)

Browser storage, after a full session:

```
DevTools → Application → Cookies / Local Storage / Session Storage
```

**Expect**: zero entries set by analytics.

Stored rows — confirm the IP is not persisted rather than assuming it:

```powershell
docker compose exec database psql -U postgres -d umami -c "\d website_event"
docker compose exec database psql -U postgres -d umami -c "SELECT * FROM website_event ORDER BY created_at DESC LIMIT 5;"
docker compose exec database psql -U postgres -d umami -c "\d session"
```

**Expect**: no column holding a full IP address; no column linking to a platform member. Page paths **will** appear verbatim including `/players/<handle>` — that is FR-008, the owner's decision, and it is the one expected exception.

---

## Scenario 7 — Session replay is OFF (release gate)

In the dashboard, check website settings for session replay and web vitals.

**Expect**: disabled. On an authenticated-only platform, replay would capture member data wholesale and invalidate every privacy claim in the spec. This is a gate, not a preference.

---

## Scenario 8 — Analytics failure is invisible (FR-011, FR-013, SC-006)

```powershell
docker compose stop umami
```

Browse the app.

**Expect**: every page loads normally, within its usual budget. No visible error, no console error the user would notice, no hang. Confirm in DevTools that the failed beacon is **not retried**.

```powershell
docker compose start umami
```

---

## Scenario 9 — Database isolation (FR-025, US5)

```powershell
# Should SUCCEED
docker compose exec database psql "postgresql://umami:$env:UMAMI_DB_PASSWORD@localhost:5432/umami" -c "SELECT 1;"

# Should FAIL — this is the assertion that makes FR-025 true
docker compose exec database psql "postgresql://umami:$env:UMAMI_DB_PASSWORD@localhost:5432/appdb" -c "SELECT 1;"
```

**Expect**: the second is refused. If it succeeds, the `REVOKE CONNECT` did not apply — PostgreSQL grants `CONNECT` to `PUBLIC` by default, so this is the failure mode to expect, not an unlikely edge case.

```powershell
# Confirm the role is not over-privileged
docker compose exec database psql -U postgres -c "\du umami"
```

**Expect**: no `Superuser`, no `Create DB`, no `Create role`.

---

## Scenario 10 — Existing suites unaffected

The injected snippet changes `index.html`, so the E2E suite must be confirmed clean.

```powershell
docker compose -f docker-compose.test.yml up --abort-on-container-exit
docker compose -f docker-compose.e2e.yml up --abort-on-container-exit
```

---

## Deployed verification (Dev, after Terraform)

DNS is a **manual prerequisite** — create the A record for `analytics-dev.juggerhub.com` pointing at the existing static public IP before applying, or cert-manager's HTTP-01 challenge will fail.

```powershell
terraform workspace select dev
terraform plan -var-file=envs/dev.tfvars -var image_tag=<sha>
```

After apply:

```powershell
kubectl -n juggerhub get pods -l app=umami
kubectl -n juggerhub logs -l app=umami -c db-init      # initContainer provisioning
kubectl -n juggerhub get ingress
kubectl -n juggerhub get certificate                    # expect Ready=True
```

**Default credentials must fail (FR-022, SC-010)** — attempt `admin` / `umami` against the dashboard and confirm refusal. If it succeeds, the post-deploy Job did not run or ran before Umami seeded the account.

**Datastore stays unexposed (FR-024)**:

```powershell
kubectl -n juggerhub get svc postgres    # expect ClusterIP None; no LoadBalancer, no NodePort
```

**Dashboard without an ingress** — always available, and the fallback if the owner would rather not run an internet-facing login:

```powershell
kubectl -n juggerhub port-forward svc/umami 3000:3000
```

Then repeat scenarios 3–8 against Dev, and confirm Dev events land under the **Dev** website only (FR-018, SC-008).
