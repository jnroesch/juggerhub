# Contract: nginx Routes

**Feature**: `038-umami-session-recording`

Extends [033's route contract](../../033-umami-analytics/contracts/nginx-routes.md). Three
new locations in `frontend/nginx.conf.template`, plus one line added to the two existing
analytics locations.

---

## The rule that decides all three paths

The recorder computes its own endpoints from **the directory part of its own `src`**, not
from the origin ([research.md](../research.md) §4):

```js
const l = (a || "" || r.src.split("/").slice(0, -1).join("/")).replace(/\/$/, "");
const c = `${l}/api/record`;
const u = `${l}/api/websites/${i}/recorder`;
```

Serving the script at `/jh-insights/r.js` therefore puts **both** derived endpoints inside
`/jh-insights/`. Serving it at `/jh-insights-r.js` (root level) would derive
`/api/record` — inside the namespace the .NET backend already owns.

**The directory is load-bearing. Moving this script to the root breaks the backend's
namespace boundary, and it breaks it silently:** `/api/record` would fall through to
`location /api/` and be proxied to .NET, which answers 404 to a fire-and-forget POST.
Nothing errors, nothing logs, recordings simply never arrive.

---

## Routes

| Browser requests | Proxies to | Match type |
|---|---|---|
| `/jh-insights/r.js` | `<upstream>/recorder.js` | exact (`=`) |
| `/jh-insights/api/record` | `<upstream>/api/record` | exact (`=`) |
| `/jh-insights/api/websites/${JH_ANALYTICS_WEBSITE_ID}/recorder` | `<upstream>/api/websites/<id>/recorder` | exact (`=`), id substituted at container start |

No token from the blocked-name list (`analytics`, `track`, `stat`, `collect`, `beacon`,
`pixel`, `umami`, `record`… ) appears in a name a blocklist matches — `record` appears only
as a path segment on our own origin, inside a namespace whose parent (`/jh-insights`) is
already established as unmatched (033 FR-016).

### Why exact matches and not a prefix

`location /jh-insights/api/ { proxy_pass ...; }` would serve every route Umami exposes —
including `/api/auth/login` and the administrative surface — from the **application's**
origin. 033 deliberately put the dashboard on a separate hostname; a prefix here would
quietly undo that.

The cost of exact matching is that the website id must be known to nginx. It already is,
per environment — it just needs promoting out of the `JH_ANALYTICS_HEAD` string into its
own variable.

---

## New environment variable

`JH_ANALYTICS_WEBSITE_ID` — the website id on its own, so the config location can be an
exact match. Today the id exists only embedded inside `JH_ANALYTICS_HEAD`; it is used in
two places now and should be one value.

When analytics is off it is empty, and the location renders as
`/jh-insights/api/websites//recorder`. That is a valid, unreachable location — nginx
starts, and nothing requests it because no snippet is injected. Analytics-off remains the
default with no conditional configuration, exactly as 033 established.

---

## Shape

```nginx
location = /jh-insights/r.js {
    set $jh_upstream "${JH_ANALYTICS_UPSTREAM}";
    proxy_pass $jh_upstream/recorder.js;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header Cookie "";
    proxy_connect_timeout 2s;
    proxy_send_timeout 3s;
    proxy_read_timeout 3s;
    proxy_next_upstream off;
}

location = /jh-insights/api/record {
    set $jh_upstream "${JH_ANALYTICS_UPSTREAM}";
    proxy_pass $jh_upstream/api/record;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header Cookie "";
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    # The recorder fragments at ~500 KB — under nginx's 1 MB default, but not by enough
    # that the default should be what stands between a payload and a silent 413.
    client_max_body_size 2m;
    proxy_connect_timeout 2s;
    proxy_send_timeout 3s;
    proxy_read_timeout 3s;
    proxy_next_upstream off;
}

location = /jh-insights/api/websites/${JH_ANALYTICS_WEBSITE_ID}/recorder {
    set $jh_upstream "${JH_ANALYTICS_UPSTREAM}";
    proxy_pass $jh_upstream/api/websites/${JH_ANALYTICS_WEBSITE_ID}/recorder;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header Cookie "";
    proxy_connect_timeout 2s;
    proxy_send_timeout 3s;
    proxy_read_timeout 3s;
    proxy_next_upstream off;
}
```

Every constraint inherited from 033 and unchanged: the upstream is reached through a
**variable** so nginx does not resolve it at startup and refuse to boot when Umami is
absent; timeouts are short so a wedged Umami cannot tie up worker connections;
`proxy_next_upstream off` because retrying a dropped beacon is the amplification
Principle VII prohibits.

---

## `proxy_set_header Cookie "";` — also added to 033's two locations

Verified ([research.md](../research.md) §6): the recorder hardcodes `credentials: "omit"`
and the tracker defaults to it, so the sign-in cookie does **not** reach Umami today. This
line is not a fix — it is the thing that keeps that true if a future version changes its
default.

It belongs on `/jh-insights.js` and `/jh-insights/e` as well. Those inherit the same
exposure from 033 and the same one-line protection; adding it in three places and not five
would leave the older, higher-traffic path unprotected for no reason.

The auth cookie is `httpOnly`, `SameSite=Strict`, `path=/`, so a same-origin POST is
exactly the shape of request that would carry it. Cost: one line. Failure it prevents:
platform session cookies in a third-party application's request logs, discovered later.

---

## Ordering

nginx matches exact (`=`) locations before prefix locations regardless of file order, so
these cannot be shadowed by `location /` (the SPA fallback) or `location /api/` (the
backend). Placing them beside 033's analytics locations keeps the analytics surface
readable in one block; it is not what makes them win.
