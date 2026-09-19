# Tasks: PWA Shell for Web Push

**Input**: Design documents from `specs/054-pwa-shell/` — GH #307

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[contracts/served-files.md](contracts/served-files.md), [quickstart.md](quickstart.md)

**Tests**: Included. The plan designs them explicitly (§8) because two of the spec's promises
("no one adds caching later by reflex", "the worker script is never shadowed") are only true
while a test enforces them; the constitution's verification gate requires the rest.

**Organization**: by user story. US1 (installable) and US2 (worker registered) are genuinely
independent — Chrome no longer needs a service worker for installability (research R2) — so
either can ship alone. US3 (always the current release) is the guarantee the other two must not
break; its tasks are assertions and manual checks, not new behaviour.

**Scope guard**: no task touches `backend/` or `infra/`. If one seems to need to, stop and
re-read the plan — something is wrong.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 / US2 / US3 from spec.md
- All paths are relative to the repository root

## Path Conventions

- Static files: `frontend/apps/web/public/` (copied to the root of `dist/apps/web/browser` by the
  build's `assets` glob; nginx serves that folder as `/`)
- App code: `frontend/apps/web/src/`
- e2e: `frontend/apps/web-e2e/src/`
- Web tier: `frontend/nginx.conf.template`
- One-off tooling: `frontend/tools/`

---

## Phase 1: Setup

**Purpose**: the two folders this feature introduces, and the baseline every later task assumes.

- [ ] T001 Create the two new folders `frontend/apps/web/public/icons/` and
      `frontend/apps/web/src/app/core/pwa/` and confirm `frontend/tools/` exists (create it if
      not). Re-verify the baseline the plan states before touching anything: no `manifest*` under
      `frontend/apps/web/`, no `@angular/service-worker` in `frontend/package.json`, no
      `serviceWorker` key in `frontend/apps/web/project.json`, no `<link rel="manifest">` in
      `frontend/apps/web/src/index.html`. If any of these has appeared since planning, stop and
      report before continuing.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: the web-tier change both stories need, and the two test files both stories extend.
Doing these first means every later task adds to a file that already exists rather than two
tasks racing to create it.

- [ ] T002 Add two **exact-match** locations to `frontend/nginx.conf.template`, placed directly
      after the existing `location /i18n/ { … }` block and before `location / { … }`, following
      [contracts/served-files.md](contracts/served-files.md):
      `location = /sw.js { add_header Cache-Control "no-cache" always; try_files $uri =404; }` and
      `location = /manifest.webmanifest { default_type application/manifest+json; add_header Cache-Control "no-cache" always; try_files $uri =404; }`.
      Precede them with a comment in the file's own voice stating (a) why exact matches: `location /`
      would otherwise serve `index.html` for a missing worker and the browser's refusal to run a
      page as a worker is swallowed by the registration helper, so the failure is silent — the 038
      hazard; (b) why `default_type`: this image's `mime.types` has no `webmanifest` entry (checked
      in `nginxinc/nginx-unprivileged:1.31.5-alpine`); (c) why `no-cache`: the same reasoning as
      `/i18n/` — keep the copy, ask first, 304 on the ETag — and that these locations carry no
      `sub_filter`, so 033's injection never touches them. Change nothing else in the template.
- [ ] T003 [P] Create the guard spec skeleton `frontend/apps/web/src/app/core/pwa/pwa-shell.spec.ts`:
      a Jest `describe('PWA shell (054)')` that resolves `PUBLIC_DIR = path.resolve(__dirname, '../../../../public')`
      and `INDEX_HTML = path.resolve(__dirname, '../../../index.html')` with Node `fs`/`path`, plus
      two helpers: `readPublic(rel: string): string` and `pngSize(buf: Buffer): { width: number; height: number }`
      that reads the IHDR width at `buf.readUInt32BE(16)` and height at `buf.readUInt32BE(20)`
      after asserting the 8-byte PNG signature. No assertions yet beyond one that the public
      folder exists (so the file is green and the paths are proven before US1/US2 add cases).
      Run `cd frontend && npx nx test web --testPathPatterns="core/pwa"` — green.
- [ ] T004 [P] Create the e2e skeleton `frontend/apps/web-e2e/src/pwa-shell.spec.ts` modelled on
      `frontend/apps/web-e2e/src/health.spec.ts`: a header comment naming feature 054, the two
      Playwright projects it runs at, and the rule that **header assertions run only when
      `process.env['BASE_URL']` is set** (the nginx container; the local Vite dev server sends its
      own headers) — export `const servedByNginx = Boolean(process.env['BASE_URL']);`. No tests yet.

**Checkpoint**: nginx has both locations; both test files exist and are green/empty. US1 and US2
can now proceed in parallel.

---

## Phase 3: User Story 1 — The app can be installed on a phone's home screen (Priority: P1) 🎯 MVP

**Goal**: a manifest and an icon set that make the product installable on Android, iOS and
desktop, opening standalone with the product's name and mark, with zero new copy.

**Independent Test**: on Android, Chrome offers to install and the installed app launches
standalone with the JuggerHub mark; on iPhone, Add to Home Screen shows the coral-gradient icon
and opens without Safari chrome; the guard spec and the e2e manifest/icon cases are green.

### Icons (FR-002)

- [ ] T005 [P] [US1] Author `frontend/apps/web/public/icons/icon-maskable.svg` — the maskable
      source artwork: a 64×64 viewBox, a **full-bleed** `<rect width="64" height="64">` (no `rx`)
      filled with the same linear gradient as `frontend/apps/web/public/favicon.svg`
      (`#F5623A` → `#7A9B87`, same `x1/y1/x2/y2`), and the favicon's cross (two white stroked
      paths, `stroke-linecap="round"`) plus the yellow `#FFE066` centre circle scaled and centred
      so the whole mark sits inside the central 80 % (i.e. all glyph geometry within x,y ∈ [6.4, 57.6]
      — scale the favicon's 18→46 cross to about 20→44 and the circle radius from 6.5 to about 6).
      Add an XML comment at the top: source only, never referenced by the manifest; regenerate
      PNGs with `node frontend/tools/render-pwa-icons.mjs`.
- [ ] T006 [P] [US1] Write `frontend/tools/render-pwa-icons.mjs` — a one-off renderer with a
      header comment in the style of `backend/Data/Seed/regenerate-cities500.mjs` (what it does,
      how to run it, that the output is committed, why `.mjs` and not `.ps1`: only Chromium can
      rasterise the SVG with transparent corners and the Playwright CLI has no `--omit-background`).
      Import `{ chromium }` from `playwright-core`; for each job in
      `[['favicon.svg',192,'icons/icon-192.png',true],['favicon.svg',512,'icons/icon-512.png',true],['icons/icon-maskable.svg',192,'icons/icon-maskable-192.png',false],['icons/icon-maskable.svg',512,'icons/icon-maskable-512.png',false],['icons/icon-maskable.svg',180,'icons/apple-touch-icon.png',false]]`
      (paths relative to `frontend/apps/web/public/`): `page.setViewportSize({width,height})` at
      `deviceScaleFactor: 1`, `page.setContent()` with `html,body{margin:0;background:transparent}`
      and an `<img>` of exactly `size×size` whose `src` is the SVG read from disk and inlined as a
      `data:image/svg+xml;base64,…` URL (so no `file://` permissions matter), wait for the image to
      load, then `page.screenshot({ path, omitBackground })`. Exit non-zero on any failure.
- [ ] T007 [US1] Run `cd frontend && node tools/render-pwa-icons.mjs` (depends on T005, T006) and
      commit the five PNGs under `frontend/apps/web/public/icons/`. Open each once: the `any`
      icons have transparent corners outside the rounded square; the maskable and apple-touch
      icons are fully opaque squares with the mark centred and not touching the edges.

### Manifest and entry page (FR-001, FR-003, FR-005)

- [ ] T008 [P] [US1] Create `frontend/apps/web/public/manifest.webmanifest` with **exactly** the
      JSON in [contracts/served-files.md](contracts/served-files.md) § Manifest shape: `name` and
      `short_name` "JuggerHub", `id`/`start_url`/`scope` "/", `display` "standalone",
      `background_color` "#FBF8F3", `theme_color` "#FFFFFF", four icons with `purpose` "any" or
      "maskable" on separate entries. **No `description`, `lang`, `screenshots`, `shortcuts`** or
      any other string a browser would show (FR-005). JSON has no comments, so record the two
      colours' token names (`surface-page`/sand-0, `surface-card`) in the guard spec instead (T010).
- [ ] T009 [US1] Edit `frontend/apps/web/src/index.html`: inside `<head>`, after the two favicon
      links, add `<link rel="manifest" href="manifest.webmanifest" />`,
      `<meta name="theme-color" content="#FFFFFF" />` and
      `<link rel="apple-touch-icon" href="icons/apple-touch-icon.png" />`, with an HTML comment in
      the file's existing voice: iOS takes the Home Screen icon from `apple-touch-icon`, not the
      manifest, and paints transparent pixels black — hence the opaque render; the theme colour is
      DESIGN `surface-card`, the bars' colour. **Leave the viewport meta untouched** (no
      `viewport-fit=cover` — research R6) and add no `mobile-web-app-capable` meta (R7).

### Guard and e2e for US1

- [ ] T010 [US1] Extend `frontend/apps/web/src/app/core/pwa/pwa-shell.spec.ts` (depends on T003,
      T007, T008, T009) with a `describe('manifest')`: parses `manifest.webmanifest`; asserts
      `display === 'standalone'`, `start_url === '/'`, `scope === '/'`, `id === '/'`;
      `background_color === '#FBF8F3'` and `theme_color === '#FFFFFF'` with a comment naming the
      DESIGN tokens and saying a token change in DESIGN.md means a change here; asserts
      `'description' in manifest === false` and likewise for `lang`, `screenshots`, `shortcuts`
      (FR-005, with a comment explaining Chrome renders them in the install dialog); for every
      `icons[]` entry asserts the file exists under `public/`, `type === 'image/png'`, `purpose`
      is exactly `'any'` or `'maskable'` (never combined), and `pngSize()` equals the parsed
      `sizes`. Add a `describe('index.html')` asserting the manifest link, the `theme-color` meta
      with `#FFFFFF`, and the `apple-touch-icon` link are present, and that `apple-touch-icon.png`
      exists at 180×180. Run `npx nx test web --testPathPatterns="core/pwa"` — green.
- [ ] T011 [US1] Extend `frontend/apps/web-e2e/src/pwa-shell.spec.ts` (depends on T004, T008)
      with `test('the installable description and its icons are served')`: `request.get('/manifest.webmanifest')`
      → `ok()`, `headers()['content-type']` contains `manifest+json` **only when `servedByNginx`**
      (the dev server may send `application/json` or octet-stream), body parses as JSON with
      `display === 'standalone'`; for each `icons[].src` a `request.get('/' + src)` → `ok()` and
      `content-type` contains `image/png`. Run against the dev server
      (`cd frontend && npx nx e2e web-e2e --grep "installable"`) — green at both projects.

### On-device walk for US1 (manual, owner's standing rule — screenshots to the PR)

- [ ] T012 [US1] Build and run the stack (`docker compose up -d --build`) or deploy the branch to
      Dev, then walk [quickstart.md](quickstart.md) § 5 **Android**: install offer appears
      (SC-001), installed launch is standalone with the mark, splash is sand with a white status
      bar, `/chat` bottom bar sits above the gesture area. Screenshot the install dialog and the
      standalone dashboard.
- [ ] T013 [US1] Walk [quickstart.md](quickstart.md) § 5 **iPhone** (iOS ≥ 16.4): Add to Home
      Screen shows the gradient icon and the name (SC-002); launch is standalone with **no Safari
      chrome** — if chrome IS shown, apply the R7 contingency by adding
      `<meta name="mobile-web-app-capable" content="yes" />` to `frontend/apps/web/src/index.html`
      and record it in the PR; sign-in is required inside the installed app (expected, separate
      cookie jar — do not "fix"); on a notched device the bottom bar clears the home indicator on
      the dashboard, `/chat` and `/admin` and the header sits below the status bar (FR-004).
      Screenshot each.
- [ ] T014 [US1] Walk [quickstart.md](quickstart.md) § 5 **Desktop** (Chrome or Edge): the
      address-bar install icon appears; installed window is standalone with the mark (US1
      scenario 5). Screenshot.

**Checkpoint**: the product is installable everywhere with zero new copy. Ship-able alone.

---

## Phase 4: User Story 2 — The browser holds a background worker for the product (Priority: P1)

**Goal**: a push-only worker registered at root scope on every visit, invisible to the player,
failing silently, with no behaviour of its own.

**Independent Test**: DevTools → Application → Service Workers shows one activated registration
with scope `/` on any route including `/privacy`; the unit spec, the guard cases and the e2e
registration case are green; the app behaves identically with the worker present.

### The worker (FR-006, FR-007)

- [ ] T015 [P] [US2] Create `frontend/apps/web/public/sw.js` — a header comment in the repo's
      voice stating what it is (JuggerHub's push-only service worker, feature 054), what it
      deliberately does NOT do (no `fetch` listener — a no-op one adds a worker hop to every
      navigation and the app has nothing to serve offline; no Cache API; no `importScripts`; no
      storage; no network), that `pwa-shell.spec.ts` fails the build if any of those appear, that
      `push`/`notificationclick` arrive with #308, and that 033's serve-time `index.html`
      injection is the second reason nothing is cached. Then exactly two listeners:
      `self.addEventListener('install', () => self.skipWaiting());` and
      `self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));`
      with a one-line comment each: lifecycle only, so a changed worker takes over on the visit
      that fetched it instead of waiting for every tab to close. Run `cd frontend && npx nx lint web`;
      if the linter picks up `public/sw.js` and fails on `self`/`event` globals, add a scoped
      override for `apps/web/public/sw.js` in `frontend/apps/web/eslint.config.mjs` declaring
      `languageOptions.globals = { self: 'readonly' }` — never disable rules file-wide.

### Registration (FR-006, FR-008, FR-010)

- [ ] T016 [P] [US2] Create `frontend/apps/web/src/app/core/pwa/register-service-worker.ts`
      exporting `registerServiceWorker(nav: Navigator = navigator, win: Window = window): void`:
      return immediately unless `'serviceWorker' in nav`; define `const register = () => { void nav.serviceWorker.register('/sw.js', { scope: '/', updateViaCache: 'none' }).catch(() => undefined); }`;
      if `win.document.readyState === 'complete'` call `register()` now, else
      `win.addEventListener('load', register, { once: true })`. Doc comment: registered after
      bootstrap and after `load` so first paint never waits (FR-008); **one attempt per visit, no
      retry, no timeout, no logging** — Principle VII is not engaged, the browser's own static
      fetch is not an app network call, and a wrapper here is review-rejectable; the failure is
      invisible by requirement; `updateViaCache: 'none'` is the browser-side half of FR-009 (the
      nginx `no-cache` header is the other); the two parameters exist for the spec's fakes.
- [ ] T017 [P] [US2] Create `frontend/apps/web/src/app/core/pwa/register-service-worker.spec.ts`
      (no `TestBed` — a plain function): (1) navigator without `serviceWorker` → a spy container's
      `register` is never called; (2) `readyState 'complete'` → `register` called exactly once
      with `'/sw.js'` and `{ scope: '/', updateViaCache: 'none' }`; (3) `readyState 'loading'` →
      not called until the fake window's `load` listener is invoked, then once; (4) `register`
      returning a rejected promise → no throw, and `await` a microtask turn to confirm no
      unhandled rejection (attach `process.on('unhandledRejection')` guard for the test). Run
      `npx nx test web --testPathPatterns="core/pwa"` — green.
- [ ] T018 [US2] Edit `frontend/apps/web/src/main.ts` (depends on T016): import
      `registerServiceWorker` from `./app/core/pwa/register-service-worker` and change the
      bootstrap line to
      `bootstrapApplication(App, appConfig).then(() => registerServiceWorker()).catch((err) => console.error(err));`
      with a two-line comment: registered only after Angular is up (FR-008), not as an app
      initializer (those run *before* bootstrap completes) and not in a component (the worker
      belongs to the origin, not a view). Confirm `npx nx build web --configuration=production`
      still passes the initial-bundle budget.

### Guard and e2e for US2

- [ ] T019 [US2] Extend `frontend/apps/web/src/app/core/pwa/pwa-shell.spec.ts` (depends on T003,
      T015) with `describe('sw.js')`: reads `public/sw.js`; asserts it does **not** contain
      `addEventListener('fetch'` (also the double-quoted form), does **not** contain
      `importScripts(`; asserts it **does** contain `addEventListener('install'` and
      `addEventListener('activate'`. Each negative assertion carries a one-line comment naming
      the FR (FR-007) and why (a worker with a fetch handler sits between every page and the
      server; nothing may be pulled in from elsewhere). Green.
- [ ] T020 [US2] Extend `frontend/apps/web-e2e/src/pwa-shell.spec.ts` (depends on T004, T015,
      T018) with two tests: `test('the worker script is served as a script, never as the app page')`
      — `request.get('/sw.js')` → `ok()`, `content-type` contains `javascript`, body does not
      contain `<html` and does contain `addEventListener`; and
      `test('the worker is registered for the whole site after the app starts')` — `page.goto('/sign-in')`
      (an off-shell public route, so it proves FR-006 on the pages that render outside the shell)
      then `await page.evaluate(() => navigator.serviceWorker.ready.then((r) => ({ scope: r.scope, script: r.active?.scriptURL })))`
      and assert `scope === new URL(page.url()).origin + '/'` and `script` ends with `/sw.js`.
      Run against the dev server — green at both projects.

**Checkpoint**: a worker is registered on every visit; the app is byte-identical to the player.
Ship-able alone or with US1.

---

## Phase 5: User Story 3 — Every visit is the current release, always (Priority: P2)

**Goal**: prove, and keep proving, that nothing this feature adds can serve a stale release,
freeze 033's injected configuration, or present the app offline.

**Independent Test**: DevTools shows an empty Cache Storage and every reload fetching from the
network; toggling offline shows the browser's own page; a changed `JH_ANALYTICS_HEAD` is visible
on the next reload without a rebuild; the header e2e cases are green against the nginx image.

- [ ] T021 [US3] Extend the `describe('sw.js')` in `frontend/apps/web/src/app/core/pwa/pwa-shell.spec.ts`
      (depends on T019) with the FR-011 assertions: `sw.js` does **not** contain `caches.`,
      `caches.open`, `indexedDB`, `localStorage` or `sessionStorage`. Comment: "No offline mode,
      ever (spec Clarification 3, FR-011). This test is the rule; delete it only with a new owner
      decision recorded in a spec." Break it once on purpose (add a `caches.open('x')` line to
      `sw.js`), watch it fail, revert.
- [ ] T022 [US3] Extend `frontend/apps/web-e2e/src/pwa-shell.spec.ts` (depends on T004, T002)
      with `test('the worker and the manifest are served fresh')` guarded by
      `test.skip(!servedByNginx, 'Cache-Control is nginx\'s, not the dev server\'s')`: both
      `request.get('/sw.js')` and `request.get('/manifest.webmanifest')` return a `cache-control`
      header containing `no-cache`. Run against the compose stack:
      `docker compose up -d --build` then `cd frontend; $env:BASE_URL='http://localhost:3000'; npx nx e2e web-e2e --grep "pwa"`
      — all pwa cases green at both projects (this is also the first run of T011/T020 against
      real nginx, which is where the shadowing hazard would show).
- [ ] T023 [US3] Manual, against the compose stack — [quickstart.md](quickstart.md) § 3 and § 4:
      `curl.exe -sI` the two files and confirm `200` + correct `Content-Type` +
      `Cache-Control: no-cache`; confirm a missing worker is a **404** (rename it inside the
      container or build without it), never `200 text/html`; in DevTools confirm one activated
      registration at scope `/`, an **empty** Cache Storage, no request served "(ServiceWorker)" on
      reload, the browser's own offline page when offline (SC-006); change `JH_ANALYTICS_HEAD` in
      `.env`, `docker compose up -d frontend`, reload — the injected `<head>` changes with no
      rebuild (SC-005, the 033 property); edit a comment in `sw.js`, rebuild, reload twice — the
      new worker is active by the second load (US3 scenario 4). Note the results in the PR.

**Checkpoint**: all three stories hold together; the freshness guarantee is enforced by tests.

---

## Phase 6: Disclosure, verification and hand-off

**Purpose**: the privacy-policy paragraph the spec requires (FR-015), the full verification run,
and the PR.

- [ ] T024 Edit the three legal catalogues **together** —
      `frontend/apps/web/public/i18n/legal/de.json`, `…/en.json`, `…/es.json` — German first and
      authoritative: (a) append one paragraph to `privacy.storage.body` (the array under the
      heading "Cookies and what's kept in your browser") naming the site's own background helper
      that the browser keeps so the site can act while no tab is open — for example receive a
      notification the player has asked for — as part of the site's program, not a record about
      the player, with nothing about them in it, removed by clearing the site's data in the
      browser; (b) extend the enumeration in the `legalBasis` body sentence ("…and that's all
      there is here: the cookie that keeps you signed in, your choice of language, and the
      half-filled state of a form…" / "…und mehr liegt hier nicht: …") with that helper as a fourth
      item so the sentence stays true. Follow the owner's legal-text rules: category of data,
      durable wording, **no statement of what it does not do today**, German `–` not `—`. Then run
      `cd frontend && npx nx test web --testPathPatterns="legal-catalog|catalog-parity"` — both
      green (the legal guard walks arrays, so one missing locale is red; the interface guard proves
      zero interface keys were added, SC-007).
- [ ] T025 Read the three new paragraphs on `/privacy` in de, en and es on the running stack
      ([quickstart.md](quickstart.md) § 6) at 375 px and desktop: they sit inside the existing
      section with 036's long-form measure, German reads as the original. Screenshot the German
      one for the PR.
- [ ] T026 Full verification per [quickstart.md](quickstart.md) § 7:
      `cd frontend; npx nx lint web; npx nx test web; npx nx build web --configuration=production`,
      then the compose e2e overlay
      `docker compose -f docker-compose.yml -f docker-compose.test.yml up --build --abort-on-container-exit e2e`.
      All green. Confirm `git diff --stat main -- backend infra` is **empty** (FR-014) and that
      `git status` shows no change to `frontend/package.json` or `package-lock.json` (no new
      dependency).
- [ ] T027 Commit in small logical groups with `#307` in each message and the required
      attribution line — suggested: (1) nginx locations, (2) icons + render tool, (3) manifest +
      index.html + guard/e2e for US1, (4) worker + registration + unit/guard/e2e for US2, (5) US3
      assertions, (6) privacy policy ×3. Open the PR with `Closes #307`, the device screenshots
      from T012–T014 and T025, the note on whether the R7 contingency was needed, and the plan's
      recorded residuals (viewport left without `cover`; Chromium-only e2e; `.mjs` tool with the
      cities500 precedent). Add a comment on #308 that its blocker is resolved once merged and
      that its install hint must tell iPhone players to expect a sign-in inside the installed app.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)**: T002 (nginx) is independent of T003/T004;
  all three can run in parallel after T001.
- **US1 (Phase 3)** and **US2 (Phase 4)** both depend only on Phase 2 and are **independent of
  each other** (research R2). Either is a valid first increment.
- **US3 (Phase 5)** depends on US2 (T019 → T021; T015/T018 → T022) and on T002.
- **Phase 6** depends on everything above; T024/T025 (policy) can actually start any time after
  T001 — they touch only the legal catalogues — but they are placed last so the spec's SC-007
  check runs once, at the end.

### Within US1

- T005 ∥ T006 → T007 (render) ; T008 ∥ T009 (manifest, index) ; T010 needs T007+T008+T009 ;
  T011 needs T008 ; T012–T014 need a built image (T007+T008+T009+T002).

### Within US2

- T015 ∥ T016 ∥ T017 → T018 (main.ts needs the helper) ; T019 needs T015 ; T020 needs T015+T018.

### Parallel Opportunities

- After T001: **T002, T003, T004** together.
- US1 start: **T005, T006, T008, T009** together (four different files).
- US2 start: **T015, T016, T017** together — and US2 can run alongside US1 entirely.
- Device walks T012/T013/T014 are three devices; run them side by side once one image is built.

---

## Parallel Example: kicking off both stories at once

```text
# Foundational, in parallel after T001:
Task: T002 nginx exact-match locations in frontend/nginx.conf.template
Task: T003 guard spec skeleton in frontend/apps/web/src/app/core/pwa/pwa-shell.spec.ts
Task: T004 e2e skeleton in frontend/apps/web-e2e/src/pwa-shell.spec.ts

# Then US1 and US2 side by side:
Task: T005 icon-maskable.svg          | Task: T015 public/sw.js
Task: T006 tools/render-pwa-icons.mjs | Task: T016 register-service-worker.ts
Task: T008 manifest.webmanifest       | Task: T017 register-service-worker.spec.ts
Task: T009 index.html links           |
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. T001 → T002/T003/T004.
2. T005–T011 → installable, guard + e2e green.
3. T012–T014 device walks → screenshots.
4. Stop and validate: the app installs on all three platforms with zero new copy. That alone
   closes half of #307 and can merge.

### Incremental Delivery

1. Add US2 (T015–T020): the worker exists — #308's hard blocker is gone.
2. Add US3 (T021–T023): the freshness guarantee is enforced, not assumed.
3. T024–T027: disclosure, full verification, PR.

### Notes

- Nothing here touches `backend/` or `infra/`; the ingress already routes `/` (Prefix) to the
  frontend Service.
- Principle VII is not engaged. If a task tempts you to add a retry, timeout or breaker around
  registration, that is the review-rejectable case the plan names.
- Gate 7 is not instantiated (no markup, no interface copy); the device walks are the UI
  verification, screenshots are mandatory.
- Commit after each logical group; do not amend.
