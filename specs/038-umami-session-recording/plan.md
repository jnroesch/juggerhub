# Implementation Plan: Umami Session Recording

**Branch**: `038-umami-session-recording` | **Date**: 2026-08-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/038-umami-session-recording/spec.md`

## Summary

Load Umami v3.2.0's rrweb recorder alongside the existing tracker, delivered the same way
033 delivers measurement: **same-origin, inside the existing DNT/GPC guard, configured per
environment at container start, no Angular source change**.

The design turns on one detail found by reading the recorder ([research.md](./research.md) §4):
it derives its own endpoints from **the directory its own `src` sits in**. Serving it at
**`/jh-insights/r.js`** therefore places its config and collection endpoints inside the
`/jh-insights` namespace 033 already owns — so nothing touches `/api/` (which belongs to the
.NET backend), no new hostname is needed, and no URL carries a blocklist-matching token.

Two findings changed the shape of the work relative to what was requested:

- **Nothing is written to the visitor's device.** Neither recorder nor tracker uses any
  client-side storage API. The consent rule about device storage stays unengaged, so the
  privacy policy's "no cookie banner" section **survives** and the legal rewrite is one
  claim, not a section (§10, spec FR-017/FR-018/FR-021/FR-023).
- **The recorder's privacy behaviour is server-side state**, not page configuration —
  masking level, sample rate, and blocked regions live in a JSONB column that can be
  changed with no release and no review. So they are seeded as code (§2), the way 033
  seeds website ids.

**Retention is the real build.** Umami has no expiry, no retention setting, and no cleanup
job; the platform has none either (GH #106). FR-012's 30 days is a new CronJob — and spec
FR-012a makes recording contingent on it working, so it is not optional scope.

**No backend changes. No Angular source changes.** The change surface is nginx config,
Terraform, compose, seed SQL, and the legal i18n catalogues.

## Technical Context

**Language/Version**: No application language change. Umami v3.2.0 is consumed as a
pre-built image. This repository's change surface is nginx configuration, Terraform (HCL),
Docker Compose, SQL, and three i18n JSON catalogues.

**Primary Dependencies**: `docker.umami.is/umami-software/umami:3.2.0` (unchanged, already
deployed), the existing `postgres:18.3-alpine` StatefulSet, the existing
`nginx:1.31.3-alpine` frontend runtime. No new image, no new package, in either project.

**Storage**: Umami's existing `umami` database. Recording adds `session_replay` (chunked,
`events BYTEA`) and `session_replay_saved`, both created by migrations already applied with
the 3.2.0 image. No application-database change, no new PVC, no EF entity, no migration in
`backend/`.

**Testing**: No unit-test surface in application code. Verification is
[quickstart.md](./quickstart.md), run against the local `analytics` compose profile first
and then Dev. Two checks are release gates rather than tests: the masked-input audit
(SC-003/SC-003a) and the device-storage audit (FR-021). The existing Playwright suite must
be confirmed unaffected — `index.html` gains one more injected script.

**Target Platform**: AKS (Dev, Prod) and Docker Compose (local, opt-in `analytics` profile).

**Project Type**: Web application — infrastructure, frontend-shell, and legal-content
change only.

**Performance Goals**: No measurable change to first render (SC-007). The recorder is
appended `async` after the tracker and starts only once the tracker has a session.

**Constraints**: Must never delay render or degrade the app (FR-028, Principle VII); must
not exhaust the shared database (FR-030); one build serves every environment (FR-025);
recording must be switchable independently of measurement (FR-024).

**Scale/Scope**: A community platform in early life. At `sampleRate: 1` every session is
recorded, bounded by `maxDuration` at 5 minutes per session — the input to the storage
ceiling in §"Storage growth" below.

## Constitution Check

*GATE: evaluated before Phase 0 and re-evaluated after Phase 1 design.*

| # | Gate | Verdict | Notes |
|---|------|---------|-------|
| 1 | **Architecture** (thin controllers, DI services, no repository layer, EF projections) | **N/A** | No backend code. |
| 2 | **Data access** (pagination, projections, `AsNoTracking`, `BaseEntity`) | **N/A** | No EF entities. Umami owns its schema in its own database. The retention job is SQL against that database, not EF. |
| 3 | **Security review** (OWASP, never trust the client, no leaked secrets) | **PASS, with attention** | Same-origin delivery of a third-party asset that captures the DOM. Controls in "Security posture" below. The sign-in cookie was specifically checked and does **not** reach Umami ([research.md](./research.md) §6). |
| 4 | **Auth** (httpOnly cookies, backend-sourced password policy) | **N/A** | Untouched. Recording adds no auth surface; the dashboard's own login is unchanged from 033. |
| 5 | **Conventions** (separate `.html`/`.css`/`.ts`; `.ps1` scripts only) | **PASS** | No new frontend component. No `.sh` added — the retention job is a SQL file invoked by a CronJob command, and any local helper is `.ps1`. |
| 6 | **Environment parity** (identical across local/Dev/Prod) | **PASS** | Same resources in all three, differing only in configuration: recorder on/off, website id, and the retention CronJob's schedule. The local `analytics` profile gains the same job. |
| 7 | **UI/Design compliance** (DESIGN.md + UI review checklist) | **PASS, narrow** | Ships no new UI: the injected script renders nothing, and FR-020 (owner decision) means no in-app notice. The **privacy policy page gains a section**, which is existing prose in an existing layout — DESIGN.md's Long-form content section already governs it. A `checklists/ui-review.md` is instantiated for that text only. |
| 8 | **Resilience** (Principle VII) | **PASS** | Fire-and-forget, no retry, bounded poll, bounded recording duration, short proxy timeouts, `proxy_next_upstream off` ([research.md](./research.md) §9). |

### Security posture (Gate 3 detail)

The new risk is not the transport — it is that a DOM capture of an authenticated platform
is now flowing into, and sitting in, a third-party application's database.

- **Input values never leave the browser.** `maskAllInputs` is set at both available mask
  levels and cannot be disabled through this configuration surface ([research.md](./research.md) §3).
  This is the one guarantee that holds even if the recording store is later compromised.
- **The sign-in cookie is not transmitted.** Both senders use `credentials: "omit"`;
  `proxy_set_header Cookie "";` is added anyway, because the failure it prevents is silent.
- **Only two Umami paths are exposed same-origin**, both exact matches — not a prefix.
  A `location /jh-insights/api/` would have proxied Umami's entire admin API to the app
  origin ([research.md](./research.md) §5).
- **Objection is honoured before any request exists.** The recorder is appended inside
  033's existing DNT/GPC guard, so an objecting visitor never requests it and never hits
  the config endpoint (FR-009, FR-010).
- **Recordings expire.** 30 days, enforced by a job, including saved replays
  ([research.md](./research.md) §7).
- **Heatmaps stay off.** A second capture mechanism with its own table, out of scope.
- **Dashboard access is unchanged** — recordings are viewable only there (FR-013).

### Post-Phase 1 re-evaluation

Re-checked after design. **No new violations.** Gate 7 moved from "N/A" to "PASS, narrow"
once it was clear the policy text is a UI-bearing change; a UI review checklist is
instantiated for it. Gate 3 tightened during design — the prefix-location shortcut was
rejected in favour of two exact matches. Complexity Tracking stays empty.

## Project Structure

### Documentation (this feature)

```text
specs/038-umami-session-recording/
├── plan.md              # This file
├── research.md          # Phase 0 — ten findings, two of which corrected the spec
├── data-model.md        # Phase 1 — Umami's replay schema + the config/retention contract
├── quickstart.md        # Phase 1 — verification, including the two release gates
├── contracts/
│   ├── nginx-routes.md      # The three new paths and why the directory placement matters
│   ├── recorder-snippet.md  # The injected snippet, inside the existing guard
│   ├── recorder-config.md   # website.replay_config — the privacy-bearing settings
│   └── retention.md         # The 30-day deletion contract
├── checklists/
│   ├── requirements.md  # From /speckit-specify
│   └── ui-review.md     # Phase 1 — for the privacy policy text (Gate 7)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
frontend/
└── nginx.conf.template          # + 3 locations: /jh-insights/r.js, /jh-insights/api/record,
                                 #   /jh-insights/api/websites/${JH_ANALYTICS_WEBSITE_ID}/recorder
                                 #   + Cookie-stripping on all analytics locations

frontend/apps/web/public/i18n/legal/
├── de.json                      # AUTHORITATIVE — recording section; one corrected claim
├── en.json                      # informational
└── es.json                      # informational

infra/
├── modules/app/
│   ├── analytics.tf             # + JH_ANALYTICS_WEBSITE_ID + recorder line in analytics_head;
│   │                            #   + session-replay retention CronJob
│   └── variables.tf             # + umami_replay_retention_days
├── envs/dev.tfvars              # retention days
└── envs/prod.tfvars             # retention days

scripts/
├── umami-seed-website.sql       # + recorder_enabled + replay_config seeding
└── umami-replay-retention.sql   # NEW — the 30-day delete, chunk-safe

docker-compose.yml               # + JH_ANALYTICS_WEBSITE_ID, recording flag,
                                 #   + retention job under the `analytics` profile
.env.sample                      # + documented recording variables

specs/033-umami-analytics/quickstart.md   # scenario 7 amended (gate reversal, research §8)
```

**Structure Decision**: Same shape as 033 — an infrastructure-and-shell change with no
Angular source touched, which is what keeps the recorder out of the application bundle and
guarantees it cannot delay first render. The one addition beyond 033's footprint is the
retention CronJob, which exists because nothing in the platform or the product provides it.

## Key design decisions

Full reasoning in [research.md](./research.md); the load-bearing ones:

1. **`/jh-insights/r.js`, inside a directory** (§4) — the recorder derives its endpoints
   from its own script directory, so this one placement decision keeps recording traffic
   out of `/api/` and inside the namespace 033 already owns. Root placement would have
   forced permanent carve-outs from the backend's namespace.
2. **Two exact-match locations, never a prefix** (§5) — a prefix would expose Umami's whole
   API, including its admin login, on the application origin.
3. **`replay_config` seeded as code** (§2) — masking level and sample rate are the
   privacy-bearing settings, and they are dashboard-editable JSONB. If they are not seeded,
   the spec's masking requirement is enforced by nothing.
4. **`maskLevel: "moderate"`, set explicitly** (§3) — the owner's decision, written down
   rather than inherited from a default. `"strict"` and `blockSelector` are the two
   one-field escape hatches if the chat exposure is reconsidered.
5. **Retention as a CronJob deleting whole sessions** (§7) — deleting by chunk age would
   leave sessions replaying from the middle; saved replays expire too, so "kept 30 days"
   is true without an asterisk.
6. **033's release gate is rewritten, not failed** (§8) — this feature reverses a decision
   033 recorded as a gate, and the reversal must be visible to whoever runs that check next.

## Storage growth (FR-030)

The bound comes from the settings, not from an estimate: every session recorded
(`sampleRate: 1`), each capped at 5 minutes (`maxDuration`), each chunk flushed at 100
events or 2 s, fragmented above ~500 KB, retained 30 days. `events` is `BYTEA` in the
shared Postgres instance.

The plan does not guess the resulting volume — it **measures it in Dev before Prod**
(quickstart scenario 9) and states the ceiling from that measurement. Two controls exist
if the measurement is unwelcome: `sampleRate` below 1, and a shorter retention. Both are
configuration.

This is the one number this plan deliberately leaves to observation rather than
prediction, because 033 already established that this instance shares a database with the
application and FR-030 forbids analytics degrading it.

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| **Chat message history in recordings** — the FR-006a decision. Captures member-to-member communication the other party cannot object to. | **Certain** (it is the decision) | **High** — the least defensible part of the feature | Recorded in the spec, not hidden. Two one-field reversals available: `maskLevel: "strict"` or a `blockSelector` for the chat surface. Revisit before Prod. |
| Retention job never built, or built and silently stops | Medium | **High** — a published 30-day promise the system does not keep | FR-012a makes recording contingent on it. Quickstart verifies deletion by clock manipulation, not by trusting the schedule. |
| `replay_config` changed in the dashboard, drifting from the repository | Medium | Masking silently weakens with no trace in git | Seeded from SQL on deploy; quickstart asserts the live config endpoint's response, so drift fails a check rather than going unnoticed. |
| Recording payload volume degrades the shared Postgres | Medium | App slowdown (FR-030) | Measure in Dev before enabling in Prod; `sampleRate` and retention are the levers. |
| A future recorder version starts writing to device storage | Low | The "no cookie banner" position fails | FR-021 makes this a verified check, not an inherited assumption; quickstart scenario 4 re-runs it. |
| SPA navigation splits one journey across replays | Medium | Feature is less useful than expected (SC-002) | Unverified against the Angular router — same open question 033 had for pageviews. Quickstart scenario 2 settles it before anything else is built on it. |
| Website id must exist before the config endpoint answers | Certain | Bootstrap friction | Already solved by 033's seeding; this feature extends the same seed row. |

## Spec drift

Two items, both recorded rather than silently absorbed:

- **033's session-replay release gate is reversed.** 033's plan states replay "stays OFF …
  Treated as a release gate, not a preference", carries it as a severe-impact risk row, and
  checks it in `quickstart.md` scenario 7. This feature turns it on with the owner's
  agreement. 033's scenario 7 is **amended in this feature** to assert the intended state
  (replay on, heatmaps off, `maskLevel: "moderate"`) with a pointer to this spec. Leaving it
  would make 033's own verification fail against a correctly behaving system.
- **The spec's device-storage premise was wrong and has been corrected.** Spec FR-021 and
  FR-023 originally amended and withdrew 033's no-device-storage position; reading the
  actual recorder showed no storage API is used at all. Both requirements were rewritten to
  uphold 033 instead, and FR-017/FR-018 narrowed accordingly — the policy's cookie-banner
  section survives. Corrected in the spec on 2026-08-01, before planning completed.

Additionally, **recording is already enabled in the Dev dashboard** (`replayEnabled: true`,
`sampleRate: 1`) — server-side state that predates this plan and is currently inert only
because no page loads the recorder. Not drift in the specs, but it means Dev starts
recording the moment the snippet ships, which is why FR-019 (policy first) is sequenced
ahead of the delivery work in [tasks](./quickstart.md).

## Complexity Tracking

> No constitution violations. Table intentionally empty.
