# Quickstart: Verifying Umami Session Recording

**Feature**: `038-umami-session-recording`

Run locally first, then against Dev. Two scenarios are **release gates**, not tests —
scenario 4 (masking) and scenario 5 (device storage). Neither may be waived, because each
is the sole evidence for a statement the published privacy policy will make.

**Sequencing note**: the Dev website already has `replayEnabled: true, sampleRate: 1` set
server-side, so Dev begins recording the moment the snippet reaches it. FR-019 requires the
policy text to be published **first**. Scenario 8 checks that ordering before anything else
goes to Dev.

---

## Prerequisites

```powershell
# Local stack with analytics (opt-in profile — a plain `docker compose up` starts none of it)
docker compose --profile analytics up -d

# Dashboard: http://localhost:3001  (admin / umami)
```

---

## Scenario 1 — A recording exists and plays back (FR-001, SC-001)

Browse the local app: sign in, move through two or three pages, click things.

Open the dashboard → the website → **Replays**.

**Expect**: the session is listed, and playing it shows the pages as rendered with pointer
movement, clicks, and scrolling visible.

**If nothing appears**, check in this order — each is a real failure mode, not a guess:

1. `GET /jh-insights/api/websites/<id>/recorder` returns `enabled: true` (scenario 3)
2. `/jh-insights/r.js` returns JavaScript, not the SPA's `index.html` (a missed nginx
   location falls through to the SPA fallback and returns HTML with status 200)
3. `POST /jh-insights/api/record` returns 2xx, not 404 (a 404 means the path derivation is
   wrong — see [contracts/nginx-routes.md](./contracts/nginx-routes.md))
4. The tracker loaded at all — the recorder cannot start without it

---

## Scenario 2 — One journey is one replay (FR-002, SC-002)

Navigate between several in-app pages **without a full page reload**, then replay.

**Expect**: all of those pages appear in a single continuous replay.

This is the same unverified assumption 033 carried for page views — whether Umami's session
handling follows the Angular router. **Settle it here before anything is built on it.** If
navigations split into separate replays, say so plainly: the feature still works but is
markedly less useful than SC-002 promises, and that is a spec conversation, not a bug fix.

---

## Scenario 3 — The configuration the browser actually obeys (FR-006, sampling)

```powershell
curl http://localhost:3000/jh-insights/api/websites/$env:UMAMI_WEBSITE_ID/recorder
```

**Expect** (`sampleRate` excepted — see below):

```json
{"enabled":true,"replayEnabled":true,"heatmapEnabled":false,"sampleRate":0.15,
 "heatmapSampleRate":0.15,"maskLevel":"moderate","maxDuration":300000,"blockSelector":""}
```

Check the **endpoint**, never the seed file — the seed is not what the browser obeys, and
`getRecorderConfig` silently discards invalid keys. `sampleRate` is the one that fails
quietly: nothing in this repository writes it, so if nobody set it in the dashboard the
environment records 15% of sessions and looks perfectly healthy. Locally the seeded website
starts that way — raise it in website settings if you want every session.

`heatmapEnabled` must be `false`. It is a separate capture mechanism with its own
disclosure obligation and is out of scope.

---

## Scenario 4 — RELEASE GATE: nothing typed is captured (FR-005, FR-006, SC-003, SC-003a)

In one session, deliberately type a **distinctive marker value** into each of:

- a password field
- an email field in settings
- a message composer

Use values you can grep for, e.g. `zzmarkerpw1`, `zzmarkeremail@example.com`, `zzmarkermsg`.

**Then check both sides:**

```powershell
# 1. In flight — DevTools ▸ Network ▸ the POSTs to /jh-insights/api/record
#    Search the request payloads for each marker.

# 2. At rest — in the umami database
docker compose exec database psql -U umami -d umami -c `
  "SELECT count(*) FROM session_replay WHERE encode(events,'escape') LIKE '%zzmarker%';"
```

**Expect**: zero occurrences, in both. Masking happens in the browser before transmission,
so a hit in the network payload and a hit in the database are the same failure found at
different distances.

**Also confirm the replay is still useful**: the masked fields are visibly present, and you
can see them being interacted with. Masking hides the value, not the interaction (FR-007).

**And confirm the known exposure is what was decided, not a surprise**: at
`maskLevel: "moderate"`, message history **rendered on screen** is visible in the replay.
That is FR-006a. If it is not visible, something is masking more than intended — also worth
knowing.

---

## Scenario 5 — RELEASE GATE: nothing is written to the device (FR-021, FR-010, SC-004)

With recording active, in a fresh profile: DevTools ▸ Application ▸ Storage.

**Expect**: no cookie, no Local Storage entry, no Session Storage entry, no IndexedDB
database from analytics or recording. The only cookie present is the platform's own
sign-in cookie.

**Why this is a gate**: it is the single fact holding up the platform's "no cookie banner"
position (FR-018, FR-023). If a future recorder version starts writing to the device, that
position fails and recording must be switched off until it is reassessed — so this is
re-verified on every version bump, not inherited.

Also confirm the sign-in cookie is **not** being sent to the analytics paths: the POSTs to
`/jh-insights/api/record` carry no `Cookie` header.

---

## Scenario 6 — Objection means no request at all (FR-009, FR-010, SC-004)

Browse with Global Privacy Control enabled, then again with Do Not Track enabled.

**Expect, in each case**: **zero** requests to `/jh-insights.js`, `/jh-insights/r.js`,
`/jh-insights/api/record`, or the config endpoint. Not blocked requests — absent ones. And
no recording afterwards.

The recorder implements **neither** signal itself — the strings appear nowhere in it, and
it has no equivalent of the tracker's `data-do-not-track`. The injected guard is the entire
mechanism ([contracts/recorder-snippet.md](./contracts/recorder-snippet.md)). Verify by
watching the network tab, not by checking that no data arrived.

---

## Scenario 7 — RELEASE GATE: retention actually deletes (FR-012, FR-012a, SC-006)

Two parts. The first catches the bug; the second catches the schedule.

**Part 1 — the statement is correct.** Seed three cases with backdated `created_at`:

| Case | Setup | Expected after the job |
|---|---|---|
| Expired session | all chunks older than 30 days | **gone**, with its `session_replay_saved` row |
| Straddling session | oldest chunks older than 30 days, newest inside | **wholly present** — not truncated |
| Recent session | all chunks inside 30 days | untouched |

Run the retention statement, then re-check. The straddling case is the one that fails if
deletion is written by row instead of by session — and it fails invisibly, producing
replays that start in the middle.

**Part 2 — the schedule fires.**

```powershell
kubectl get cronjob,job -n <ns> | Select-String replay
```

**Expect**: the CronJob exists **and** a Job has completed. An existing CronJob that has
never run is not evidence of anything.

---

## Scenario 8 — RELEASE GATE: the policy is published first (FR-019, SC-005)

Before the snippet reaches any environment, open `/privacy` **in that environment**.

**Expect**:

- The recording section is present, in all three languages, describing what is captured,
  that message content on screen is included (FR-016a), the 30-day retention, who can view
  recordings, and how to object.
- The claim *"Nothing in it says who was doing the browsing"* is **gone or corrected** — it
  is the one statement recording makes false.
- The claim *"stores nothing on your device"* is **still there, unweakened** — it remains
  true (scenario 5), and softening a true statement misdescribes the system in the other
  direction.
- The *"Why there's no cookie banner"* section is **still there** — its premise survives.
- `lastUpdated` has changed (FR-020: the policy page is the only notice members get).

Dev is already armed server-side, so this ordering is the difference between disclosed
processing and undisclosed processing.

---

## Scenario 9 — Storage growth is measured, not guessed (FR-030)

After a period of real Dev traffic:

```powershell
docker compose exec database psql -U umami -d umami -c `
  "SELECT pg_size_pretty(pg_total_relation_size('session_replay')) AS replay_size,
          count(*) AS chunks, count(DISTINCT session_id) AS sessions FROM session_replay;"
```

Record the per-session average and project it to 30 days at Prod's session volume. State
the ceiling in the plan from **this measurement**.

**Expect**: a figure the shared Postgres instance can carry without affecting the
application. If it cannot, the levers are `sampleRate` below 1 or shorter retention — both
configuration, neither a redesign.

---

## Scenario 10 — Failure stays invisible (FR-028, FR-029, SC-007, SC-008)

```powershell
docker compose stop umami
```

Browse the app.

**Expect**: every page loads normally and within its usual budget. No visible error, no
hang, no console error a member would notice. Confirm in DevTools that failed recording
POSTs are **not retried** — the recorder swallows failures by design, and a retry here
would be the amplification Principle VII prohibits.

Then compare first render with recording on and off: no measurable difference (SC-007).

---

## Scenario 11 — Recording is switchable on its own (FR-024, FR-026, SC-009)

1. Turn recording off, leaving analytics on. Browse.
   **Expect**: page views still recorded; no recorder requested; no new replays.
2. Turn analytics off entirely. Browse.
   **Expect**: neither tracker nor recorder requested.
3. `docker compose up` with **no** profile.
   **Expect**: no analytics container, no recording, exactly as before this feature existed.

---

## Amendment to 033's quickstart

033 `quickstart.md` **scenario 7** currently reads:

> **Session replay is OFF (release gate)** … On an authenticated-only platform, replay would
> capture member data wholesale and invalidate every privacy claim in the spec. This is a
> gate, not a preference.

That gate is **deliberately reversed by this feature** with the owner's agreement. Amend it
to assert the intended state — replay **on**, heatmaps **off**, `maskLevel: "moderate"` —
with a pointer to `specs/038-umami-session-recording/spec.md`.

Leaving it unamended would make 033's own verification fail against a correctly behaving
system, and the next person to run it could not tell an accident from a decision.
