# Phase 0 Research: Umami Session Recording

**Feature**: `038-umami-session-recording` | **Date**: 2026-08-01

Everything below was established by reading the assets and source actually in use — the
recorder and tracker served by the Dev environment, the live per-website configuration
endpoint, and Umami v3.2.0's own migrations and route handlers — rather than from
documentation. Where a finding contradicts what the feature request assumed, that is
called out, because two of them do.

---

## §1 — What the supplied snippet actually is

The snippet in the request is the one Umami's own dashboard generates. `WebsiteReplaySettings.tsx`
builds it verbatim:

```ts
const recorderCode = `<script defer src="${recorderUrl}" data-website-id="${websiteId}"></script>`;
```

So it is copy-out-of-the-dashboard, not something hand-written — which is why it points at
the dashboard hostname and carries the Dev website id. Neither is a decision anyone made
about JuggerHub's delivery, and both are wrong for this repository (§4).

**Decision**: treat the snippet as a *statement of which two things the browser needs* —
the recorder script and a website id — and re-derive the delivery from 033's contract.

---

## §2 — The recorder is configured on the server, not in the page

The recorder takes exactly two inputs from the DOM (`data-website-id`, optional
`data-host-url`). Everything about its behaviour comes from a per-website endpoint it
fetches at startup:

```js
const e = await fetch(u, { credentials: "omit" });
const t = await e.json();
if (!t?.enabled) return;
b = true === t.replayEnabled;        // record sessions
S = true === t.heatmapEnabled;       // heatmaps — separate feature
v = t.sampleRate;                    // default 0.15
k = t.maskLevel;                     // default "moderate"
x = t.maxDuration;                   // default 300000 ms
M = t.blockSelector;                 // default ""
```

The live Dev endpoint answers:

```json
{"enabled":true,"replayEnabled":true,"heatmapEnabled":false,"sampleRate":1,
 "heatmapSampleRate":0.15,"maskLevel":"moderate","maxDuration":300000,"blockSelector":""}
```

**Two findings that change the plan:**

1. **Recording is already armed in Dev.** `replayEnabled: true` with `sampleRate: 1`.
   Nothing is being recorded only because no page loads the recorder script. This also
   means 033's release gate ("session replay stays OFF", `quickstart.md` scenario 7) has
   already been turned off in the Dev dashboard — see §8.
2. **The behaviour that matters for privacy is deployed state, not code.** Masking level
   and sample rate live in a JSONB column and can be changed by anyone with dashboard
   access, without a release, with no review and no trace in git. A spec requirement about
   masking (FR-006) is therefore only as strong as the mechanism that keeps that column
   correct — which is why §6 makes it configuration-as-code rather than a dashboard step.

**Decision**: seed `website.recorder_enabled` and `website.replay_config` from SQL, the
way 033 already seeds the website row, and verify the endpoint's response as an
acceptance test rather than trusting the dashboard.

**Alternatives considered**: set it once in each dashboard by hand (rejected — invisible
to review, silently drifts between environments, and 033 already rejected the same
pattern for website ids); a Terraform provider for Umami (does not exist).

---

## §3 — Masking: what the two levels actually do

`maskLevel` is `'strict' | 'moderate'` and nothing else (`src/lib/recorder.ts`). It
resolves to rrweb options at record time:

```js
...(e = k, "strict" === e ? { maskAllInputs: true, maskTextSelector: "*" }
                          : { maskAllInputs: true })
```

| Level | Input values | Rendered text |
|---|---|---|
| `moderate` (default) | **masked** | captured |
| `strict` | **masked** | **masked** |

**`maskAllInputs: true` is set at both levels**, so FR-005 (no passwords) and FR-006 (every
input value masked, in the browser, before transmission) are satisfied by the product at
either setting and cannot be switched off through this configuration surface. That is a
stronger guarantee than the spec assumed it was buying.

The owner's decision — "mask input values only" — is therefore exactly `maskLevel:
"moderate"`, which is both the default and what Dev already holds. The declined
alternative ("mask all text everywhere") is exactly `"strict"`, a **one-word change to one
JSONB field**.

**Decision**: `maskLevel: "moderate"`, set explicitly rather than left to the default, so
the choice is visible in the repository instead of being the absence of a value.

**Consequence for FR-006a**, recorded because it is the feature's widest exposure: at
`moderate`, chat message history rendered on screen is captured. `blockSelector` is the
lever that would exclude it — a CSS selector applied at record time, so blocked regions
never enter the payload. Not used now (owner declined), but it means the mitigation is a
selector string in one config field, not a redesign. Both escape hatches from the riskiest
decision in this feature cost a single field change.

---

## §4 — Delivery: why the snippet's URL cannot be used, and what replaces it

The recorder derives its own endpoints from where it was loaded from:

```js
const i = o("website-id");                 // data-website-id
const a = o("host-url");                   // data-host-url
const l = (a || "" || r.src.split("/").slice(0, -1).join("/")).replace(/\/$/, "");
const c = `${l}/api/record`;                          // POST target
const u = `${l}/api/websites/${i}/recorder`;          // config endpoint
```

With no `data-host-url`, the base is **the directory of the script's own URL** — not its
origin. That single detail decides the whole nginx design.

Using the dashboard URL as supplied would mean: a cross-origin request to a hostname
containing `analytics` (defeating 033 FR-016 and every blocklist decision in that
feature), Dev's hostname and website id baked into a build that also ships to Prod
(033 FR-020), and the recorder loaded outside the DNT/GPC guard (033 FR-007).

Serving it first-party at the root — `/jh-insights-r.js` — derives a base of `https://<origin>`,
which sends recording traffic to **`/api/record` on the application origin**. `/api/` there
already proxies to the .NET backend, so this collides with the platform's own API namespace
and would need exact-match locations carved out of it.

**Decision**: serve the recorder from **inside a directory**, at **`/jh-insights/r.js`**.
The derived base becomes `https://<origin>/jh-insights` — the namespace 033 already owns —
and the two endpoints land at:

| Browser requests | nginx proxies to |
|---|---|
| `/jh-insights/r.js` | `<umami>/recorder.js` |
| `/jh-insights/api/record` | `<umami>/api/record` |
| `/jh-insights/api/websites/{id}/recorder` | `<umami>/api/websites/{id}/recorder` |

Nothing touches `/api/`, no new hostname, no blocklist-matching token in any URL, and
`data-host-url` stays unset — consistent with 033's contract, which rejected it as a value
that can drift.

**Alternatives considered**: root-level script plus exact-match `/api/record` carve-outs
(works, but permanently entangles analytics with the backend's namespace — one careless
`location /api/` edit later silently breaks either measurement or the API);
`data-host-url` pointing at the dashboard (cross-origin, blocklist-matched, and duplicates
a value that can drift).

---

## §5 — Scope of the nginx proxy: deliberately two paths, not a prefix

`/jh-insights/api/record` is a fixed path. The config endpoint contains the website id, so
it is *not* fixed, which invites a prefix location like `location /jh-insights/api/`.

**Rejected.** That would proxy Umami's entire API surface — including `/api/auth/login`
and every administrative route — to the application's own origin, undoing 033's decision
to keep the dashboard on a separate hostname.

**Decision**: both locations are exact matches, and the website id is substituted into the
config location at container start by the `envsubst` mechanism the frontend image already
uses:

```nginx
location = /jh-insights/api/websites/${JH_ANALYTICS_WEBSITE_ID}/recorder { ... }
```

This requires promoting the website id to its own environment variable —
`JH_ANALYTICS_WEBSITE_ID` — which today exists only embedded inside the
`JH_ANALYTICS_HEAD` string. That is a small, welcome tidy-up: the id is used in two places
and should be one value.

**Alternative considered**: a regex location matching a UUID shape. Works, and avoids the
new variable, but reaches any website id the instance hosts rather than only the one this
environment measures — a wider surface for no gain.

---

## §6 — The sign-in cookie does not reach the analytics service

Checked because same-origin delivery makes it a real risk: the auth cookie is
`httpOnly`, `SameSite=Strict`, `path=/`, so a same-origin POST would ordinarily carry it.

Both senders opt out explicitly. The recorder hardcodes it:

```js
fetch(c, { keepalive: o, method: "POST", body: n,
           headers: { "Content-Type": "application/json", "x-umami-cache": s },
           credentials: "omit" })
```

and the tracker defaults `data-fetch-credentials` to `"omit"`.

**Decision**: no cookie-stripping needed for correctness — but `proxy_set_header Cookie "";`
is added to the analytics locations anyway. It costs one line, it is the only thing that
keeps the guarantee true if a future version changes that default, and the failure it
prevents is silent (a session cookie sitting in a third-party application's request logs).
This is the same defence-in-depth reasoning 033 applied when it kept `data-do-not-track`
alongside its own guard.

---

## §7 — Retention: nothing exists to configure, so it has to be built

`prisma/migrations/19_add_session_replay` and `20_add_heatmap` define the storage:

```sql
CREATE TABLE "session_replay" (
  "replay_id" UUID PRIMARY KEY, "website_id" UUID, "session_id" UUID, "visit_id" UUID,
  "chunk_index" INTEGER, "events" BYTEA, "event_count" INTEGER,
  "started_at" TIMESTAMPTZ, "ended_at" TIMESTAMPTZ, "created_at" TIMESTAMPTZ DEFAULT now()
);
CREATE TABLE "session_replay_saved" (
  "saved_replay_id" UUID PRIMARY KEY, "name" VARCHAR(100), "website_id" UUID,
  "visit_id" UUID, "created_at" TIMESTAMPTZ, "updated_at" TIMESTAMPTZ,
  UNIQUE ("website_id", "visit_id")
);
```

There is **no expiry column, no retention setting, and no cleanup job** anywhere in Umami
v3.2.0 — consistent with GH #106, which records that no automated deletion runs anywhere
on this platform. FR-012's 30 days is therefore new capability.

**Decision**: a Kubernetes `CronJob` running a single scheduled `DELETE`, with the
identical operation available locally under the `analytics` compose profile so the
mechanism is the same in all three environments (constitution V). It runs as the scoped
`umami` role that already exists — no new credential, and no reach into the application
database.

**Two design points the schema forces:**

- **Recordings are chunked.** One session is many `session_replay` rows sharing a
  `session_id`. Deletion is by `created_at`, which is per chunk, so a session spanning the
  cutoff loses its oldest chunks first and would replay from the middle. Deleting by
  `session_id` where the *newest* chunk is older than the cutoff avoids producing
  half-recordings — the more correct and barely more expensive statement.
- **Saved replays are a second thing.** `session_replay_saved` marks a visit the owner
  deliberately kept. Deleting the underlying chunks on schedule leaves a saved entry
  pointing at nothing. Saving must not become an unbounded retention bypass either, so the
  30 days applies to saved replays too and the saved row goes with the chunks. **This
  needs stating in the policy**: "kept 30 days" must be true without an asterisk.

**Alternatives considered**: `pg_cron` (not installed in the `postgres:18.3-alpine` image
and adds an extension to the shared instance for one `DELETE`); a `TTL`-style partition
drop (correct at much larger volume, disproportionate here); asking the owner to delete
by hand (fails FR-012's "without anyone remembering to run it", and is exactly what #106
is about).

---

## §8 — This feature reverses an explicit 033 release gate

033's plan lists under its security posture:

> **Session replay and web vitals stay OFF.** Umami v3 introduced session replay; on an
> authenticated-only platform it would capture member data and destroy every privacy
> property this spec claims. Treated as a release gate, not a preference.

and carries it as a risk row — *"Session replay enabled by accident … Severe privacy
breach"* — with `quickstart.md` scenario 7 as the check.

This feature deliberately reverses that decision with the owner's agreement, so the gate
must be **rewritten, not quietly failed**. Leaving it in place would mean 033's own
verification script fails on a system that is behaving as designed, and the next person
to run it cannot tell whether they are looking at an accident or a decision.

**Decision**: 033's `quickstart.md` scenario 7 is amended in this feature to check the
*intended* state — replay on, heatmaps still off, `maskLevel` as specified — with a
pointer to this spec for why it changed. Recorded as spec drift in [plan.md](./plan.md).

**Note**: heatmaps (`heatmapEnabled`, the `heatmap_event` table) stay off. They are a
separate capture mechanism, out of scope per the spec, and currently `false` in Dev.

---

## §9 — Resilience posture (constitution VII)

The recorder is fire-and-forget in the same way the tracker is, and better bounded than
expected:

- `fetch(...).catch(() => {})` — failures are swallowed, **no retry**, so a failing
  analytics service cannot amplify (FR-029).
- It waits for the tracker's session cache with a bounded poll — 50 attempts at 100 ms,
  then gives up. Not an unbounded wait.
- Batches flush at 100 events or 2 s; `keepalive` is used only under 60 KB, so a large
  final flush is dropped rather than held.
- `maxDuration` (5 min) bounds how long any one session records.

**Decision**: keep 033's short proxy timeouts and `proxy_next_upstream off` on the new
locations. Add an explicit `client_max_body_size` — the recorder fragments at ~500 KB,
which is under nginx's 1 MB default but close enough that the default should not be the
thing standing between a payload and a silent `413`.

No circuit breaker: browser hop, no server-to-server call, already fails open (033 §
"Resilience posture" reasoning applies unchanged).

---

## §10 — Corrections to what the spec assumed

Two findings contradicted the feature request as written, and the spec has been updated:

| Assumed | Actual | Effect |
|---|---|---|
| The recorder writes to session/local storage | **No storage API is used at all** — no cookie, no local/session storage, no IndexedDB, in either the recorder or the tracker | The device-storage consent rule stays unengaged; the policy's "no cookie banner" section survives (spec FR-021, FR-023) and the rewrite is much narrower than planned |
| Every session is recorded | `sampleRate` defaults to **0.15**; Dev is explicitly set to **1** | The spec's "all sessions" assumption holds, but only because a value was set — so it must be seeded, not defaulted (§2) |

The first correction came from reading the file rather than a summary of it. It is the
difference between rewriting the platform's cookie-banner position and keeping it.
