---

description: "Task list for 038-umami-session-recording"
---

# Tasks: Umami Session Recording

**Input**: Design documents from `/specs/038-umami-session-recording/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: No automated test tasks are generated — this feature adds no application code path. The one existing automated test it touches is 036's i18n key-parity spec (T042). Verification is [quickstart.md](./quickstart.md), whose four release gates are tasks in their own right.

**Organization**: Grouped by user story. Every story here is independently verifiable, but note the deployment rule in "Implementation Strategy": **recording is not enabled in any environment until US2 and US3 are done** (FR-012a, FR-019).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US6)
- Exact file paths in every task

## Path Conventions

Infrastructure-and-shell change, matching 033. No `backend/` change, no Angular source change.

- nginx: `frontend/nginx.conf.template`
- Terraform: `infra/modules/app/`, `infra/envs/`
- Local stack: `docker-compose.yml`, `.env.sample`
- SQL: `scripts/`
- Legal prose: `frontend/apps/web/public/i18n/legal/`

---

## ⚠️ Read before starting

**Dev is already armed.** The Dev website has `replayEnabled: true, sampleRate: 1` set server-side ([research.md](./research.md) §2). Recording is inert only because no page loads the recorder. **The moment the snippet reaches Dev with recording enabled, real member sessions are recorded.** There is no deploy-time off switch by design — the dashboard toggle is the runtime control — so the sequencing in "Implementation Strategy" is what keeps recording from reaching Dev before the four release gates pass (T061).

---

## Phase 1: Setup (Baseline & Verification Harness)

**Purpose**: Establish the local stack and capture the "before" state, so later changes are attributable.

- [ ] T001 Start the local analytics stack with `docker compose --profile analytics up -d` and confirm the dashboard at `http://localhost:3001` and page-view measurement both work exactly as 033 left them
- [ ] T002 [P] Record the pre-change baseline in `specs/038-umami-session-recording/quickstart.md` notes: first-render timing with analytics on and recording absent (the comparison point for SC-007 / T053)
- [X] T003 [P] Confirm the local `umami` database already has `session_replay`, `session_replay_saved`, and `website.recorder_enabled` / `website.replay_config` — they ship with the pinned `umami:3.2.0` image and need **no migration from us** (see [data-model.md](./data-model.md))
- [ ] T004 [P] Confirm `curl http://localhost:3000/jh-insights/api/websites/$env:UMAMI_WEBSITE_ID/recorder` currently 404s or returns `{"enabled":false}` — the "before" state for T012

**Checkpoint**: Local stack healthy, baseline captured.

---

## Phase 2: Foundational (Delivery Plumbing — Recording OFF by default)

**Purpose**: Everything needed to serve and configure the recorder, built so that **nothing is recorded until a flag is deliberately turned on**.

**⚠️ CRITICAL**: Blocks all user stories. Every task here ships with recording disabled.

### Configuration variable plumbing

- [X] T005 Add `JH_ANALYTICS_WEBSITE_ID` as its own environment variable in `docker-compose.yml` (frontend service), sourced from the existing `UMAMI_WEBSITE_ID` — today the id exists only embedded inside the `JH_ANALYTICS_HEAD` string, and the nginx config location needs it separately ([contracts/nginx-routes.md](./contracts/nginx-routes.md))
- [X] T006 [P] Document recording in `.env.sample` alongside the existing analytics block: nothing to set, recording comes with the `analytics` profile, and the dashboard toggle is the runtime switch. State plainly what is captured
- [X] T007 [P] Add `umami_replay_sample_rate` (number, default `1`) and `umami_replay_retention_days` (number, default `30`) to `infra/modules/app/variables.tf`. **No on/off variable** — recording is on wherever analytics is on, and the dashboard toggle is the runtime kill switch, so an apply must not be needed to stop it
- [X] T008 [P] Add the matching root passthrough variables in `infra/variables.tf` and wire them into `module "app"` in `infra/main.tf`
- [X] T009 Set `umami_replay_sample_rate` in both `infra/envs/dev.tfvars` (`1`) and `infra/envs/prod.tfvars` (`0.1`), written out rather than left to the default so the difference is visible in review. Confirm the Prod figure against T054 before the first Prod apply

### nginx routes

- [X] T010 Add `JH_ANALYTICS_WEBSITE_ID` to the frontend container's environment in `infra/modules/app/analytics.tf` and `docker-compose.yml` so `envsubst` can substitute it into the config location
- [X] T011 Add the three exact-match locations to `frontend/nginx.conf.template` per [contracts/nginx-routes.md](./contracts/nginx-routes.md): `= /jh-insights/r.js` → `<upstream>/recorder.js`, `= /jh-insights/api/record` → `<upstream>/api/record`, and `= /jh-insights/api/websites/${JH_ANALYTICS_WEBSITE_ID}/recorder`. **The directory in `/jh-insights/r.js` is load-bearing** — the recorder derives its endpoints from its own script directory, so a root-level path would derive `/api/record` and collide with the .NET backend, failing silently. Keep 033's short timeouts, `proxy_next_upstream off`, variable upstream, and add `client_max_body_size 2m` on the record location
- [ ] T012 Verify the rendered config, not the template: `docker compose exec web cat /etc/nginx/conf.d/default.conf`, confirm the website id was substituted, then confirm `/jh-insights/r.js` returns JavaScript and **not** the SPA's `index.html` (a missed location returns HTML with status 200 — the failure looks like success)
- [X] T013 Add `proxy_set_header Cookie "";` to all five analytics locations in `frontend/nginx.conf.template` — the three new ones **and** 033's existing `/jh-insights.js` and `/jh-insights/e`. Not a fix (both senders already use `credentials: "omit"`, verified in [research.md](./research.md) §6) but the thing that keeps it true if a future version changes its default; leaving the older, higher-traffic paths unprotected would make no sense
- [X] T014 Confirm nginx still starts with analytics **off** (empty `JH_ANALYTICS_WEBSITE_ID`): the config location renders as `/jh-insights/api/websites//recorder`, which must be a valid unreachable location, not a start-up failure. Analytics-off stays the default with no conditional config

### Recorder configuration seeding

- [X] T015 Extend `scripts/umami-seed-website.sql` to seed `website.recorder_enabled` and `website.replay_config` per [contracts/recorder-config.md](./contracts/recorder-config.md): `replayEnabled: true`, `heatmapEnabled: false`, `sampleRate: 1`, `maskLevel: "moderate"`, `maxDuration: 300000`, `blockSelector: ""`. Idempotent, re-run on every deploy, as the rest of that script is
- [X] T016 Rewrite the comment in `scripts/umami-seed-website.sql` that currently reads *"Deliberately does NOT touch recorder_enabled: session replay stays off"*. **This feature reverses that decision**, and the surrounding reasoning ("an operator who turns something on in the UI should not have it silently reverted") is now deliberately inverted for the privacy-bearing keys: `maskLevel` and `sampleRate` are re-asserted on every deploy precisely **because** a dashboard edit would weaken what is captured from members with no trace in git. Say that in the file, with a pointer to this spec — the next reader must find a decision, not a contradiction
- [X] T017 Set `sampleRate: 1` explicitly and never rely on the default — **the default is `0.15`**, which records 15% of sessions and looks like data loss rather than misconfiguration

**Checkpoint**: The recorder can be served and configured; nothing records yet because the flag is off everywhere.

---

## Phase 3: User Story 1 — The owner can watch where a flow breaks down (P1) 🎯 MVP

**Goal**: A session can be recorded and replayed in the dashboard.

**Independent Test**: Complete a multi-step flow locally, then find and play that session back in the dashboard (quickstart scenario 1).

- [X] T018 [US1] Add the recorder to the `analytics_head` snippet in `infra/modules/app/analytics.tf`, **inside the existing DNT/GPC guard**, per [contracts/recorder-snippet.md](./contracts/recorder-snippet.md): `r.async = true`, `r.src = "/jh-insights/r.js"`, `data-website-id` from the same variable as the tracker, appended after the tracker element. **No `data-host-url`** — it would defeat the path design. Unconditional, like the tracker: with replay off in the dashboard the script loads, reads `{"enabled":false}` and stops
- [X] T019 [US1] Mirror the same snippet in `docker-compose.yml`'s `JH_ANALYTICS_HEAD`, inside the existing `${UMAMI_WEBSITE_ID:+...}` conditional so the `analytics` profile works with no variable to remember
- [X] T020 [US1] **Double quotes only** in the snippet, in both places. The value is carried into nginx's single-quoted `sub_filter` argument, so one apostrophe stops nginx from starting — taking the application down at the moment recording is enabled, not when the line is written
- [ ] T021 [US1] Enable recording locally, browse a multi-step flow, and confirm the session appears under **Replays** in the dashboard and plays back with pointer movement, clicks, and scrolling visible (quickstart scenario 1)
- [ ] T022 [US1] Verify SC-002 (quickstart scenario 2): navigate between several in-app pages **without a full page reload** and confirm they appear in one continuous replay. **This is unverified** — the same open question 033 carried for page views. If navigations split into separate replays, record it as a spec finding against SC-002 rather than working around it; the feature still functions but is markedly less useful than promised
- [ ] T023 [US1] Confirm `POST /jh-insights/api/record` returns 2xx. A 404 means the endpoint derivation is wrong and recordings are being dropped silently by a fire-and-forget POST

**Checkpoint**: Recording works locally and is demonstrable. **Do not enable in Dev yet** — US2 and US3 gate that.

---

## Phase 4: User Story 2 — Nothing a member types is captured (P1)

**Goal**: Input values never leave the browser.

**Independent Test**: Type marker values into a password field, an email field, and a message composer, then find zero occurrences in both the network payload and the database (quickstart scenario 4).

- [ ] T024 [US2] Verify the live config endpoint returns exactly the response in [contracts/recorder-config.md](./contracts/recorder-config.md), especially `maskLevel: "moderate"` and `sampleRate: 1`. Assert against the **endpoint**, never the seed file — `getRecorderConfig` silently discards invalid keys, so a typo leaves a default in place rather than failing
- [ ] T025 [US2] **RELEASE GATE** — masked-input audit (quickstart scenario 4). Type distinctive markers (`zzmarkerpw1`, `zzmarkeremail@example.com`, `zzmarkermsg`) into a password field, a settings email field, and a message composer in one session
- [ ] T026 [US2] Search the in-flight payloads: DevTools ▸ Network ▸ the POSTs to `/jh-insights/api/record`. Expect zero marker occurrences — masking happens in the browser, so a hit here is the same failure as a hit in the database, found earlier
- [ ] T027 [US2] Search at rest: `SELECT count(*) FROM session_replay WHERE encode(events,'escape') LIKE '%zzmarker%';` against the local `umami` database. Expect `0`
- [ ] T028 [US2] Confirm replay remains useful (FR-007): masked fields are visibly present, positioned correctly, and the member's interaction with them is observable — masking hides the value, not the interaction
- [ ] T029 [US2] Confirm the **known** exposure is present and is what was decided (FR-006a): at `maskLevel: "moderate"`, message history rendered on screen **is** visible in the replay. If it is not, something masks more than intended — also worth knowing before the policy text describes it

**Checkpoint**: The masking guarantee is evidence-backed, and the FR-006a exposure is confirmed as decided rather than assumed.

---

## Phase 5: User Story 3 — The policy is true on the day recording starts (P1)

**Goal**: Everything the published policy says matches the running system — including the 30-day retention promise, which requires building the mechanism that keeps it.

**Independent Test**: Read `/privacy` in all three languages against the running system and find no false statement (quickstart scenario 8); expire a backdated recording and watch it disappear (quickstart scenario 7).

### Retention (FR-012, FR-012a — the promise must be enforceable before it is published)

- [X] T030 [US3] Create `scripts/umami-replay-retention.sql` per [contracts/retention.md](./contracts/retention.md), deleting **by session, not by row**: group `session_replay` by `session_id`, compare `MAX(created_at)` to the cutoff, and delete the matching `session_replay_saved` rows in the same statement. `:retention_days` as a psql variable defaulting to 30, `\set ON_ERROR_STOP on`, matching 033's SQL conventions
- [X] T031 [US3] Add a daily `kubernetes_cron_job_v1` to `infra/modules/app/analytics.tf` running that statement as the existing scoped `umami` role — no new credential, and no reach into the application database. `concurrencyPolicy: Forbid`, `restartPolicy: OnFailure`, bounded history, and a non-zero exit on SQL error so a failure is **visible in Job status**. Retention is the one part of this feature that must fail loudly; everything else fails silently by design
- [X] T032 [US3] Mount the SQL through the existing `kubernetes_config_map_v1.umami_sql` in `infra/modules/app/analytics.tf` rather than adding a second mechanism
- [X] T033 [US3] Provide the same statement under the local `analytics` profile in `docker-compose.yml`, so the mechanism is identical in all three environments (constitution V)
- [ ] T034 [US3] **RELEASE GATE** — verify the statement is correct (quickstart scenario 7, part 1). Seed three cases with backdated `created_at`: a fully expired session with a saved-replay row, a session **straddling** the cutoff, and a recent session. Run the job. Expect: expired session and its saved row gone, straddling session **wholly present**, recent session untouched. The straddling case is the one that catches a row-wise delete, which truncates a recording so it replays from the middle with nothing to indicate anything is missing
- [ ] T035 [US3] Verify the schedule fires in the cluster (quickstart scenario 7, part 2) — a CronJob that exists but has never run is not evidence

### Policy text (FR-016 to FR-020)

- [X] T036 [US3] Add the session-recording section to `frontend/apps/web/public/i18n/legal/de.json` — **German is authoritative, write it first**. Must state: that sessions are recorded, that what is typed is masked, that **content displayed on screen is captured including message content in a conversation being read** (FR-016a), 30-day retention, that only the operator can view recordings, and how to object
- [X] T037 [US3] Correct the one statement recording makes false, in the `analytics` section: *"Nothing in it says who was doing the browsing."* This is the only claim FR-017 requires changing
- [X] T038 [US3] **Do not weaken** *"It sets no cookie and stores nothing on your device"* — it remains true ([research.md](./research.md) §10, verified at T046). Softening a true statement to feel safer misdescribes the system in the other direction, and it is also the sentence holding up the next task
- [X] T039 [US3] Keep the *"Why there's no cookie banner"* (`legalBasis`) section — its premise survives — but update its **balancing paragraph**, which currently reads *"nothing marks you as the viewer, nothing lands on your device … What's left is that a page with your name in it can show up in a list of pages viewed."* Recording makes the first clause and the summary false. The section's conclusion stands; its reasoning must now account for recordings
- [X] T040 [P] [US3] Translate the same facts into `frontend/apps/web/public/i18n/legal/en.json` and `es.json`, and bump `lastUpdated` in all three — under FR-020 the policy page is the **only** notice members get, so the visible date is the sole signal anything changed
- [ ] T041 [US3] Run the UI review checklist at `specs/038-umami-session-recording/checklists/ui-review.md` against the diff. CHK011 is the standing exception: where warm phrasing and accurate phrasing conflict, accuracy wins (FR-016a)
- [X] T042 [US3] Confirm `frontend/apps/web/src/app/core/i18n/legal-catalog.spec.ts` still passes with the new keys. This is 036's guard against a missing `de` key silently rendering **English inside the legally binding German document** (`useFallbackTranslation: true` + `fallbackLang: 'en'`)
- [ ] T043 [US3] **RELEASE GATE** — verify the published policy against the running system in each environment before recording is enabled there (quickstart scenario 8, FR-019). Read the page; do not review the diff

**Checkpoint**: The policy is true, and the retention promise in it is enforced by a mechanism that has been observed working.

---

## Phase 6: User Story 4 — Saying no still means nothing at all is recorded (P1→P2)

**Goal**: An objecting visitor generates no request at all.

**Independent Test**: Browse with GPC, then with DNT, and see zero analytics requests in the network tab (quickstart scenario 6).

- [ ] T044 [US4] Verify with Global Privacy Control enabled: **zero** requests to `/jh-insights.js`, `/jh-insights/r.js`, `/jh-insights/api/record`, and the config endpoint. Absent requests, not blocked ones
- [ ] T045 [US4] Repeat with Do Not Track. Note **why this cannot be inherited from 033**: the recorder implements neither signal — the strings appear nowhere in it, and unlike the tracker it has no `data-do-not-track` attribute. The injected guard is the *entire* objection mechanism, so this verifies the only control the privacy policy offers
- [ ] T046 [US4] **RELEASE GATE** — device-storage audit (quickstart scenario 5, FR-021). With recording active in a fresh profile: no cookie, no Local Storage, no Session Storage, no IndexedDB from analytics or recording; the only cookie is the platform's own sign-in cookie. This single fact holds up the entire "no cookie banner" position (T039), so it is re-verified on every Umami version bump rather than assumed
- [ ] T047 [US4] Confirm the sign-in cookie is not sent to the analytics paths: the POSTs to `/jh-insights/api/record` carry no `Cookie` header (belt-and-braces on T013)

**Checkpoint**: The objection route and the device-storage claim are both evidence-backed.

---

## Phase 7: User Story 5 — Recording can be turned off without losing the numbers (P2)

**Goal**: Recording is retractable on its own.

**Independent Test**: Switch recording off; page views keep flowing, no new replays appear (quickstart scenario 11).

- [ ] T048 [US5] Verify recording off + analytics on: page views still recorded, recorder never requested, no new replays
- [ ] T049 [US5] Verify analytics off entirely: neither tracker nor recorder requested. This holds for free — the recorder waits for the tracker's session and gives up after ~5s without it — but confirm rather than reason about it
- [ ] T050 [US5] Verify a plain `docker compose up` with **no** profile starts no analytics container and no recording, exactly as before this feature existed (FR-026)
- [ ] T051 [US5] Confirm the switch takes effect without rebuilding or re-releasing the frontend image (FR-025) — the snippet is injected at container start

**Checkpoint**: Recording is independently retractable in every environment.

---

## Phase 8: User Story 6 — Recording never becomes the platform's problem (P3)

**Goal**: Failure, slowness, and growth stay invisible to members.

**Independent Test**: Stop Umami, browse the app, see no difference (quickstart scenario 10).

- [ ] T052 [US6] `docker compose stop umami`, then browse: every page loads normally, no visible error, no hang. Confirm in DevTools that failed recording POSTs are **not retried** — the recorder swallows failures by design, and retrying here would be the amplification Principle VII prohibits
- [ ] T053 [US6] Compare first render with recording on and off against the T002 baseline: no measurable difference (SC-007)
- [ ] T054 [US6] Measure storage growth in Dev after real traffic (quickstart scenario 9): `pg_total_relation_size('session_replay')`, chunk count, distinct sessions. Project per-session average to 30 days at Prod volume
- [ ] T055 [US6] Record the resulting ceiling in [plan.md](./plan.md) under "Storage growth", replacing the measurement placeholder. If the figure is unwelcome, the levers are `sampleRate` below 1 or shorter retention — both configuration, neither a redesign

**Checkpoint**: The platform is demonstrably unaffected, and the storage bound is a measured number rather than an estimate.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T056 **Amend 033's reversed release gate**: `specs/033-umami-analytics/quickstart.md` scenario 7 currently asserts session replay is **OFF** and calls it "a gate, not a preference". Rewrite it to assert the intended state — replay on, heatmaps still off, `maskLevel: "moderate"` — with a pointer to `specs/038-umami-session-recording/spec.md`. Leaving it would make 033's own verification fail against a correctly behaving system, and the next person to run it could not tell an accident from a decision
- [X] T057 [P] Update the "Session replay and web vitals stay OFF" bullet and the severe-impact risk row in `specs/033-umami-analytics/plan.md` to point at this feature as the decision that superseded them
- [ ] T058 [P] Confirm `heatmapEnabled` is still `false` and `heatmap_event` is empty. Heatmaps are a separate capture mechanism with their own disclosure obligation and are out of scope — and would not be covered by the retention job (T030)
- [ ] T059 [P] Confirm the existing Playwright suite is unaffected — `index.html` now carries a second injected script
- [X] T060 [P] Add the recording variables to `.github/workflows/deploy.yml` if any deployment plumbing is missing. Note the recording flag and retention days are **not secrets** — they belong in `infra/envs/*.tfvars`, not GitHub Environment secrets, on the same reasoning 033 applied to website ids
- [ ] T061 Deploy to **Dev**. **Blocked by T025–T029 (masking), T034 (retention), and T043 (policy published in Dev).** There is no flag to flip: Dev is already armed server-side, so the deploy that ships the snippet is the moment real member sessions begin being recorded. If it needs stopping, turn replay off in the dashboard — that survives later deploys by design
- [ ] T062 Re-run quickstart scenarios 1, 4, 5, 6, and 8 against Dev — the four release gates plus replay itself. Local verification does not transfer: Dev has different configuration and real traffic
- [ ] T063 Decide Prod separately, on the evidence from T054 and T062, and record the decision. Prod is not a formality here: it is where the FR-006a chat exposure applies to real conversations between real members

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup — **blocks every user story**
- **US1 (Phase 3)**: depends on Foundational
- **US2 (Phase 4)**: depends on US1 — you cannot audit a recording that does not exist
- **US3 (Phase 5)**: retention tasks (T030–T035) depend only on Foundational and can run **parallel to US1/US2**; policy tasks (T036–T043) depend on US2, because the text must describe what T029 confirmed is actually captured
- **US4 (Phase 6)**: depends on US1
- **US5 (Phase 7)**: depends on US1
- **US6 (Phase 8)**: depends on US1; T054–T055 additionally need Dev traffic, so they follow T061
- **Polish (Phase 9)**: T061 depends on US2 + US3 complete

### The deployment gate (non-negotiable)

```text
US1 (works) ──┐
US2 (masking gate) ──┼──> T061 enable in Dev ──> T062 verify ──> T063 Prod decision
US3 (policy + retention) ──┘
```

FR-012a and FR-019 make this a rule, not a preference: recording does not run in an environment whose policy does not describe it, and does not run at all without enforced retention.

### Parallel Opportunities

- T002, T003, T004 — independent baseline checks
- T006, T007, T008 — different files (`.env.sample`, module variables, root variables)
- T030–T035 (retention) run parallel to Phases 3–4 — different files, no shared dependency
- T040 (en/es translation) parallel to T041/T042
- T057, T058, T059, T060 — independent polish tasks

---

## Parallel Example: Phase 2 Foundational

```bash
# Configuration variables — three different files, no ordering between them:
Task: "T006 Document recording in .env.sample"
Task: "T007 Add umami_replay_sample_rate + retention days to infra/modules/app/variables.tf"
Task: "T008 Add root passthrough variables in infra/variables.tf and wire into module app"
```

## Parallel Example: Retention alongside recording

```bash
# Retention touches only scripts/ and analytics.tf's CronJob; recording touches the snippet:
Task: "T030 Create scripts/umami-replay-retention.sql"
Task: "T031 Add the daily CronJob to infra/modules/app/analytics.tf"
# ...can proceed while US1's T021/T022 verification is under way
```

---

## Implementation Strategy

### MVP scope

**Phases 1–3 (T001–T023)** — recording works locally and is demonstrable. That is the MVP in the Spec-Kit sense: a complete, independently testable increment.

**It is deliberately not a shippable increment.** Enabling it anywhere requires US2 and US3, because FR-012a and FR-019 make masking evidence and a truthful published policy preconditions rather than follow-ups. Treat the MVP as "the mechanism is proven", not "this can go out".

### Incremental delivery

1. Setup + Foundational → the recorder can be served and configured, nothing records
2. US1 → recording proven locally (**stop and validate**)
3. US2 → masking gate passed; the FR-006a exposure confirmed as decided
4. US3 → retention enforced, policy true in all three languages
5. **T061** → enable in Dev; real sessions begin being recorded
6. US4, US5, US6 → objection, switchability, and platform-safety verified against Dev
7. T063 → Prod decided on evidence

### Ordering note

US4 (objection) is verified after enabling rather than before, which looks backwards. It is not: T044–T046 need a *running* recorder to prove that an objecting visitor generates **no request**. Absence of a request cannot be demonstrated against a recorder that was never going to load. Run them locally first (T044–T047), then again in Dev (T062).

---

## Notes

- `[P]` = different files, no dependencies
- Commit after each task or logical group, per the project's small-commit convention
- The four release gates are T025 (masking), T034 (retention), T043 (policy), T046 (device storage). None may be waived — each is the sole evidence for a statement the published privacy policy makes
- Two things stay verifiable rather than assumed on every Umami version bump: T046 (no device storage) and T024 (the live config response). Both are cheap; both silently invalidate a published legal claim if they change
