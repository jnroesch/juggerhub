# Contract: nginx Routing

**Feature**: `033-umami-analytics`

Defines what the frontend nginx proxies where. This is a contract because the path names are load-bearing — changing one silently breaks measurement rather than producing an error.

---

## Existing routes (unchanged)

| Path | Upstream | Notes |
|---|---|---|
| `/api/` | `backend:8080` | **Already taken by the .NET backend.** This is why the Umami dashboard cannot live on this origin — see [research.md](../research.md) §1. |
| `/hubs/` | `backend:8080` | SignalR; long timeouts, upgrade headers. |
| `/` | static SPA | `try_files … /index.html`. |

## New routes

| Public path | Proxies to | Purpose |
|---|---|---|
| `/jh-insights.js` | `umami:3000/**script.js**` | Tracker script. |
| `/jh-insights/e` | `umami:3000/**api/send**` | Collection endpoint. `POST`. |

**nginx does the renaming — the upstream paths are Umami's real, unrenamed ones.** This was verified empirically and is not what the documentation implies ([research.md](../research.md) §2):

- `TRACKER_SCRIPT_NAME` **has no effect** in the Docker image. Umami always serves the script at `/script.js`.
- `COLLECT_API_ENDPOINT` **does** work at runtime, but only by rewriting the *contents* of `script.js`. It creates no server route — Umami still accepts beacons at `/api/send` only.

So the chain is:

```text
browser → GET  juggerhub.com/jh-insights.js  → nginx → umami:3000/script.js
          (script body contains "/jh-insights/e", written by COLLECT_API_ENDPOINT)
browser → POST juggerhub.com/jh-insights/e   → nginx → umami:3000/api/send
```

**`COLLECT_API_ENDPOINT` must stay set**, and its value must equal the public collection path above. Without it the tracker posts to `/api/send` **on this origin** — which is already proxied to the .NET backend. That is a real collision, not a hypothetical one.

A mismatch anywhere in this chain produces a 404 on a fire-and-forget beacon: no console error anyone will notice, no failed page, just silently missing data. T016 checks the status codes explicitly for exactly this reason.

**Use exact-match locations** (`location = /jh-insights.js`) so these cannot be shadowed by the SPA fallback in `location /`.

### The upstream must be reached through a variable

```nginx
resolver ${JH_ANALYTICS_RESOLVER} valid=30s ipv6=off;

location = /jh-insights.js {
    set $jh_upstream "${JH_ANALYTICS_UPSTREAM}";
    proxy_pass $jh_upstream/script.js;
}
```

**Not** `proxy_pass ${JH_ANALYTICS_UPSTREAM}/script.js;`. nginx resolves a *literal* `proxy_pass` host at **startup** and aborts with `host not found in upstream` if it does not exist. Since the substitution happens before nginx parses the file, a placeholder there is still a literal.

That would mean:

- **the default local stack cannot start at all** — analytics is off, so no `umami` container exists (FR-019, SC-009); and
- in-cluster, a missing or not-yet-created analytics Service would take down **the entire frontend**.

The second is the serious one: it inverts the guarantee the rest of this contract is built around. Short timeouts and `proxy_next_upstream off` stop a *degraded* Umami from harming the app, but none of it matters if an *absent* Umami stops nginx from booting. Constitution Principle VII.

Routing through an nginx variable defers resolution to request time, which requires an explicit `resolver`. `JH_ANALYTICS_RESOLVER` is `127.0.0.11` (Docker's embedded DNS) locally and the cluster DNS ClusterIP in Kubernetes. It has no safe empty default — an unset value renders `resolver ;`, which is a config error — so every environment must set it.

Verified empirically, not reasoned about: with a literal upstream and no `umami`, `nginx -t` fails; with the variable form it succeeds.

---

## Naming constraint

These names are chosen to evade blocklist rules and must keep avoiding these tokens:

```
analytics  analytic  track  tracker  telemetry
stat  stats  collect  beacon  pixel
umami  plausible  matomo  ga  gtag
```

`jh-insights` contains none. A site-specific name is what makes a general filter rule impractical to write.

---

## Proxy behaviour requirements

Both analytics locations:

- **Short, explicit timeouts** (`proxy_connect_timeout`, `proxy_send_timeout`, `proxy_read_timeout` — a few seconds). Constitution Principle VII: nothing waits forever. A wedged Umami must not tie up frontend worker connections, which would turn an analytics outage into an application outage.
- **No retry to the upstream** (`proxy_next_upstream off`). Retrying a dropped beacon is the amplification Principle VII prohibits.
- **No `Upgrade`/`Connection` headers.** Neither route is a WebSocket; only `/hubs/` needs those.
- The script route may be cached; the collection route must not.

---

## Tracker injection

`location /` gains:

```nginx
sub_filter '</head>' '${JH_ANALYTICS_HEAD}</head>';
sub_filter_once on;
```

- `sub_filter_types` defaults to `text/html`, which is what we want — the SPA's JS bundles must not be scanned.
- When `JH_ANALYTICS_HEAD` is empty the replacement is a no-op, so **analytics-off is the natural default** with no conditional configuration. This is how local development gets no tracker unless opted in.
- `sub_filter` runs before gzip in the filter chain, so compression needs no special handling.

---

## Template mechanism

`frontend/nginx.conf` becomes `frontend/nginx.conf.template`, copied to `/etc/nginx/templates/default.conf.template`. The nginx image's own `/docker-entrypoint.d/20-envsubst-on-templates.sh` renders it to `/etc/nginx/conf.d/default.conf` at container start.

**We add no shell script** — the image supplies the processor. This is what keeps constitution Principle VI (no `.sh` files) intact; see [research.md](../research.md) §3.

Placeholders: `JH_ANALYTICS_HEAD`, `JH_ANALYTICS_UPSTREAM`, `JH_ANALYTICS_RESOLVER`.

**Every placeholder is prefixed `JH_`.** `envsubst` substitutes any name present in the environment, so an unprefixed placeholder colliding with an nginx runtime variable (`$host`, `$uri`, `$remote_addr`, `$scheme`, `$http_upgrade`, `$proxy_add_x_forwarded_for`) would corrupt the rendered config in a way that is hard to read back. The prefix makes collision impossible rather than merely unlikely.

---

## Dashboard ingress (cluster only)

Not an nginx concern — a separate Kubernetes Ingress rule on the existing controller:

| Environment | Host | TLS |
|---|---|---|
| Dev | `analytics-dev.juggerhub.com` | cert-manager, existing `ClusterIssuer` |
| Prod | `analytics.juggerhub.com` | cert-manager, existing `ClusterIssuer` |

Routes `/` → `umami:3000`. **Requires one manually-created DNS A record per environment**, pointing at the existing static public IP. The certificate is automatic; the DNS record is not.

Locally the dashboard is simply a published container port — no ingress, no hostname.
