# Implementation Plan: PWA Shell for Web Push

**Branch**: `054-pwa-shell` | **Date**: 2026-09-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/054-pwa-shell/spec.md` — GH #307

## Summary

The app is a plain SPA: `index.html` carries a title, a viewport meta and two favicon links, and
nothing else — no manifest, no service worker, no registration (verified: no
`@angular/service-worker` in `package.json`, no `ngsw-config.json`, no `serviceWorker` option in
`project.json`, nothing in `main.ts`). It cannot be installed and, the reason #307 exists, it
cannot receive web push: the browser has no worker to hand a push message to. This feature ships
exactly the prerequisite and stops: a **web app manifest**, an **icon set rasterised from the
existing mark**, a **hand-written push-only service worker registered at root scope**, and the
**two nginx exact-match locations** that keep those files from being shadowed by the SPA
fallback. Subscriptions, VAPID keys, permission and sending are #308.

**FRONTEND + WEB TIER ONLY: no backend, no entity, no endpoint, no migration, no new
dependency.** Five static files, one ~30-line TypeScript helper with a unit test, four lines in
`index.html`, two locations in `nginx.conf.template`, one e2e spec, one guard spec, and one
disclosure paragraph in the privacy policy (×3 catalogues). If a task touches `backend/` or
`infra/`, something is wrong.

**THE THREE OWNER DECISIONS SHAPE EVERYTHING** (spec Clarifications): (1) **push-only worker,
not `ngsw`** — the worker has no `fetch` handler and no cache; there is nothing on the device to
go stale, and 033's serve-time `sub_filter` injection of the analytics snippet into `index.html`
survives because the entry page is never stored. (2) **No install affordance, zero copy** — the
manifest carries a name and icons and deliberately **no `description`** (Chrome shows it in the
install dialog, which would make it the feature's only user-facing string). (3) **No offline
mode, ever** — made structural by a guard test that fails if `sw.js` ever gains a `fetch` listener
or touches the Cache API.

**THREE FACTS FOUND BY READING, NOT ASSUMED**: (a) the nginx image's `mime.types` has **no
`webmanifest` entry** (checked inside `nginxinc/nginx-unprivileged:1.31.5-alpine`), so the
manifest location sets `default_type application/manifest+json` itself; (b) the privacy policy's
"what's kept in your browser" section is an **exhaustive list** ("and that's all there is here:
the cookie…, your choice of language, and the half-filled state of a form") — a registered worker
is visible to any reader in their browser's site-data view, so the spec's "policy unchanged"
assumption was wrong and is corrected (FR-015, all three locales, German authoritative); (c) the
existing `env(safe-area-inset-bottom)` padding on the bottom nav, chat shell and admin shell
evaluates to **zero** today because the viewport meta has no `viewport-fit=cover` — and that is
left exactly as it is: with the default viewport, iOS lays the standalone app out *inside* the
safe areas, which is what satisfies FR-004; adding `cover` would push the sticky top bar under
the status bar with no top-inset handling anywhere. Recorded as a residual, not changed.

## Technical Context

**Language/Version**: TypeScript 5.x on Angular 22.1.6 (zoneless, standalone components), Nx
workspace; nginx 1.31.5 (unprivileged image) as the web tier. No backend change.

**Primary Dependencies**: none added. The worker is a hand-written `sw.js` in `apps/web/public/`;
registration is `navigator.serviceWorker.register` behind a guard. Icon rasterisation is a
one-time step using **`playwright-core`, already a dev dependency**, with its Chromium already
installed locally (`%LOCALAPPDATA%\ms-playwright\chromium-*`) — no `sharp`, no `resvg`, no
ImageMagick (none is installed; Windows' `convert` is the NTFS tool).

**Storage**: N/A. Nothing is stored server-side; the worker stores nothing on the device (FR-007);
the only device-side artefact is the browser's own registration of the worker script.

**Testing**: Jest (`nx test web`) for the registration helper and a **guard spec** that reads
`sw.js`, `manifest.webmanifest`, `index.html` and the PNG headers; Playwright (`nx e2e web-e2e`)
for the served files and the live registration, at both projects (desktop + Pixel 5); the
existing `legal-catalog.spec.ts` (key-set parity across en/de/es, walks arrays) guards the policy
paragraph. Manual: on-device walk on iOS + Android per the owner's standing rule.

**Target Platform**: browsers per the spec's baseline — current Chrome/Edge/Firefox/Safari on
desktop, Chrome on Android, Safari on iOS ≥ 16.4 for push (older iOS installs but never receives
push). Served identically in local (compose), Dev and Prod (AKS).

**Project Type**: web application (Angular SPA behind nginx; .NET backend untouched).

**Performance Goals**: registration is off the critical path (after `load`), fire-and-forget,
one attempt; zero added requests on the app's own data paths; SC-003 (registered within 5 s).

**Constraints**: no offline mode (FR-011); no `fetch` handler in the worker (a no-op `fetch`
handler is a documented anti-pattern — it slows every navigation for nothing); no caching of any
kind; entry page never stored (033); worker script never shadowed by the SPA fallback (038
hazard); zero interface copy (FR-005); `.html`/`.css`/`.ts` separate (VI).

**Scale/Scope**: 5 static files (`sw.js`, `manifest.webmanifest`, 3–5 PNGs + 1 SVG source),
1 helper + 2 specs, 4 lines of `index.html`, 2 nginx locations, 1 e2e spec, 1 policy paragraph
× 3, 1 one-off render script.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Security-First / Never Trust the Client** — PASS, with two deliberate points:
  - A root-scope service worker is the most powerful script an origin can install: with a `fetch`
    handler it would sit between every page and the server. This one has none, and the guard
    spec makes that a build-time fact (`sw.js` must contain no `addEventListener('fetch'`, no
    `caches.`, no `importScripts`). The script is a static file baked into the image; nothing
    user-controllable reaches it; the registration path is a fixed relative `'/sw.js'`, never
    composed from anything.
  - The worker cannot read the httpOnly sign-in cookie (workers never see `document.cookie`, and
    this one makes no fetch that would carry it). No secret ships in the manifest or the worker.
- **II. Thin Controllers, Service-Centric Backend** — N/A. No backend change.
- **III. Disciplined Data Access** — N/A. No data.
- **IV. Secure Authentication & Session Management** — PASS. Cookie, JWT and refresh flow are
  untouched. The iOS-installed app having its own cookie jar is a platform property the spec
  records as an edge case; nothing attempts to share the session across contexts.
- **V. Environment Parity** — PASS. The same built image serves the same five files with the same
  headers in local, Dev and Prod; the ingress already routes `/` (Prefix) to the frontend Service,
  so `/sw.js` and `/manifest.webmanifest` need no ingress change. Nothing is per-environment.
- **VI. Conventions & Tooling** — PASS for the app code (`.ts`/`.html`/`.css` separate). One
  recorded deviation: the one-off icon renderer is a Node `.mjs` file, not a `.ps1` — precedent
  `backend/Data/Seed/regenerate-cities500.mjs`; a PowerShell script cannot drive a browser, and
  the rendering needs Chromium's SVG rasteriser with `omitBackground`. See Complexity Tracking.
- **VII. Resilient by Default, Never Amplifying** — **NOT engaged.** No outbound call is added.
  The worker fetch is a same-origin static-file request made by the browser itself, not by the
  app's `HttpClient`; FR-008 mandates exactly one attempt and no retry. Wrapping registration in
  any retry/timeout/breaker is review-rejectable. The `retryInterceptor` chain is untouched.
- **Quality Gate 7 (UI/Design compliance)** — **not instantiated** (045 precedent): no new markup,
  component, control or interface copy. The manifest's two colours are DESIGN.md tokens
  (`surface-page` = sand-0 `#FBF8F3` for the splash background, `surface-card` = white `#FFFFFF`
  for the theme colour, matching the top bar and bottom bar). The privacy-policy paragraph is
  long-form prose under 036's existing section, no layout change. The on-device walk under
  SC-002 (iOS + Android, standalone mode, bottom bar clear of the home indicator) is the
  verification — screenshots attached to the PR per the owner's standing rule.
- **Quality Gate 8 (Resilience)** — not engaged, as under VII.

**Result**: No violations. One recorded convention deviation (the `.mjs` render tool), justified
below.

## Project Structure

### Documentation (this feature)

```text
specs/054-pwa-shell/
├── plan.md              # This file
├── research.md          # Phase 0: decisions with alternatives (worker kind, file names, MIME, icons, viewport, policy)
├── data-model.md        # Phase 1: none — states why
├── quickstart.md        # Phase 1: how to verify, incl. the on-device walk
├── contracts/
│   └── served-files.md  # Phase 1: the two root files, their headers and the manifest's shape
├── checklists/
│   └── requirements.md  # from /speckit-specify
└── tasks.md             # /speckit-tasks — NOT created here
```

### Source Code (repository root)

```text
frontend/
├── apps/web/
│   ├── public/
│   │   ├── sw.js                          # NEW — push-only worker: install→skipWaiting, activate→clients.claim, nothing else
│   │   ├── manifest.webmanifest           # NEW — name, short_name, id, start_url, scope, display, colours, icons; NO description
│   │   ├── icons/
│   │   │   ├── icon-maskable.svg          # NEW — source artwork: full-bleed gradient square, glyph inside the 80% safe zone
│   │   │   ├── icon-192.png               # NEW — purpose "any"  (rendered from favicon.svg, transparent corners)
│   │   │   ├── icon-512.png               # NEW — purpose "any"
│   │   │   ├── icon-maskable-192.png      # NEW — purpose "maskable" (rendered from icon-maskable.svg)
│   │   │   ├── icon-maskable-512.png      # NEW — purpose "maskable"
│   │   │   └── apple-touch-icon.png       # NEW — 180×180, opaque (iOS paints transparency black), from icon-maskable.svg
│   │   ├── favicon.svg                    # unchanged — the source of the "any" icons
│   │   └── i18n/legal/{en,de,es}.json     # EDIT — one paragraph in storage.body + the legalBasis enumeration (FR-015)
│   └── src/
│       ├── index.html                     # EDIT — <link rel="manifest">, <meta name="theme-color">, <link rel="apple-touch-icon">
│       ├── main.ts                        # EDIT — registerServiceWorker() after bootstrap resolves
│       └── app/core/pwa/
│           ├── register-service-worker.ts       # NEW — guard + register('/sw.js', {scope:'/', updateViaCache:'none'}) after load, errors swallowed
│           ├── register-service-worker.spec.ts  # NEW — unsupported browser → no call; supported → one call with those args; rejection → swallowed
│           └── pwa-shell.spec.ts                # NEW — GUARD: sw.js has no fetch/caches/importScripts; manifest valid + no description +
│                                                #        colours are the DESIGN tokens; every icon file exists with the declared PNG size;
│                                                #        index.html links manifest + apple-touch-icon
├── apps/web-e2e/src/
│   └── pwa-shell.spec.ts                  # NEW — /sw.js is JS not HTML (+ no-cache when served by nginx); manifest parses, icons 200;
│                                          #        navigator.serviceWorker.ready resolves with scope origin+'/' and scriptURL …/sw.js
├── tools/
│   └── render-pwa-icons.mjs               # NEW — one-off: playwright-core screenshots favicon.svg / icon-maskable.svg at 192/512/180
└── nginx.conf.template                    # EDIT — `location = /sw.js` and `location = /manifest.webmanifest` (exact, no-cache, try_files =404)
```

**Structure Decision**: everything lives where its neighbours already are. Static files in
`apps/web/public/` (the build copies the folder to the root of `dist/apps/web/browser`, which
nginx serves as `/`); the helper under `core/pwa/` alongside `core/i18n/` and
`core/interceptors/`; the e2e beside `health.spec.ts`, which is the origin-level pattern it
copies; the nginx locations next to `/i18n/`, whose comment is the exact argument for theirs.
Nothing under `backend/` or `infra/` changes.

## Implementation Shape

### 1. The worker — `public/sw.js`

Two lifecycle listeners and a header comment that says why there is nothing else:

- `install` → `self.skipWaiting()`; `activate` → `event.waitUntil(self.clients.claim())`. These are
  *lifecycle*, not behaviour: they make a changed worker (which #308 will ship) take over on the
  visit that fetched it instead of waiting for every tab to close. They have no observable effect
  while the worker does nothing.
- **No `fetch` listener** (FR-007 and, separately, performance: Chrome flags no-op fetch handlers
  because they add a worker round-trip to every navigation). **No `caches`** (FR-011). **No
  `importScripts`** (nothing may be pulled in from anywhere). The guard spec asserts all three by
  reading the file.
- Push and notification-click handlers are **not** stubbed in. A handler with no subscription can
  never fire and cannot be tested; #308 adds them with the code that makes them reachable.

### 2. The manifest — `public/manifest.webmanifest`

`name` + `short_name` "JuggerHub", `id` "/", `start_url` "/", `scope` "/", `display`
"standalone", `background_color` `#FBF8F3` (DESIGN `surface-page` / sand-0), `theme_color`
`#FFFFFF` (DESIGN `surface-card`, the top and bottom bars), `icons` = the four PNGs with
`purpose` "any" / "maskable" declared **separately** (a combined "any maskable" icon renders with
maskable padding wherever masking is not applied, which is why Chrome's guidance is against it).
**No `description`** — Chrome renders it in the richer install dialog and it would be the
feature's only string, violating FR-005. **No `lang`, no `screenshots`, no `shortcuts`** for the
same reason. `start_url` "/" lands on the auth guard's sign-in redirect for a signed-out player
and on the dashboard for a signed-in one — today's behaviour, US1 scenario 4.

### 3. The entry page — `src/index.html`

Three lines inside `<head>`: `<link rel="manifest" href="manifest.webmanifest">`,
`<meta name="theme-color" content="#FFFFFF">`, `<link rel="apple-touch-icon" href="icons/apple-touch-icon.png">`
(iOS takes the Home Screen icon from this link, not from the manifest, and paints a transparent
icon's background black — hence the opaque 180×180 render). The **viewport meta is left exactly
as it is** (see research R6). No `apple-mobile-web-app-capable` / `mobile-web-app-capable` meta:
Safari has honoured the manifest's `display` since iOS 11.3; if the device walk shows browser
chrome in the installed app, adding `mobile-web-app-capable` is the one-line contingency and is
recorded as such in quickstart.

### 4. Registration — `core/pwa/register-service-worker.ts` + `main.ts`

```ts
export function registerServiceWorker(nav: Navigator = navigator, win: Window = window): void
```

- Returns immediately when `!('serviceWorker' in nav)` (FR-008, scenario 3).
- Otherwise registers `'/sw.js'` with `{ scope: '/', updateViaCache: 'none' }` once the window
  has fired `load` (or at once if `document.readyState === 'complete'`), so bootstrap and first
  paint are never behind it. `updateViaCache: 'none'` makes the browser bypass its HTTP cache
  for the script on update checks — the second half of FR-009 beside the nginx header.
- The returned promise gets a single `.catch(() => {})`. **No retry, no timeout, no logging
  above debug** — one attempt per visit, invisible on failure (FR-008; VII not engaged).
- `main.ts`: `bootstrapApplication(App, appConfig).then(() => registerServiceWorker()).catch(err => console.error(err))`.
  The `.then` keeps registration after Angular is up; the existing `.catch` stays for bootstrap
  errors. Not an `APP_INITIALIZER` — those run *before* bootstrap completes, the opposite of what
  FR-008 wants; and not inside a component — the worker belongs to the origin, not to a view.
- The spec injects a fake `Navigator`/`Window`, which is why the two parameters exist.

### 5. nginx — two exact-match locations beside `/i18n/`

```nginx
location = /sw.js {
    add_header Cache-Control "no-cache" always;
    try_files $uri =404;
}
location = /manifest.webmanifest {
    default_type application/manifest+json;   # mime.types in this image has no webmanifest entry
    add_header Cache-Control "no-cache" always;
    try_files $uri =404;
}
```

- **Exact (`=`)** so `location /` can never answer: a missing file is a **404**, never
  `index.html` served as JavaScript (the 038 hazard, which fails silently — a page run as a worker
  simply refuses to register). `try_files $uri =404` is the same line `/i18n/` uses for the same
  reason.
- `no-cache` = "keep it but ask first"; with the ETag nginx sets, the ask is a 304. Browsers cap
  worker-script staleness at 24 h anyway; the header plus `updateViaCache: 'none'` brings a
  changed worker to the next visit (US3 scenario 4).
- These locations carry **no `sub_filter`** and are outside `location /`, so 033's injection never
  touches them (it is `text/html`-only regardless). Nothing else in the template changes; the
  analytics locations, `/api/`, `/hubs/` and the upload limits are untouched.

### 6. Icons — `tools/render-pwa-icons.mjs` + committed PNGs

- Source A: the existing `favicon.svg` (64×64 viewBox, rounded rect `rx=16` on a coral→sage
  gradient, white cross, yellow centre). Rendered with `omitBackground: true` at 192 and 512 →
  the `any` icons keep transparent corners.
- Source B: **new** `icon-maskable.svg` — the same gradient as a **full-bleed square** (no `rx`)
  with the cross + centre scaled to sit inside the central 80 % (the maskable safe zone), so no
  launcher mask crops the mark. Rendered opaque at 192, 512 and **180** (the last as
  `apple-touch-icon.png`).
- The script opens each SVG in headless Chromium via `playwright-core` (already installed, browsers
  present), sets the viewport to the target size with `deviceScaleFactor: 1`, and screenshots.
  Deterministic enough that re-running produces byte-stable output on the same Chromium; the
  PNGs are committed and the script is documentation of how they were made (the
  `regenerate-cities500.mjs` precedent). The guard spec reads each PNG's IHDR (bytes 16–23) and
  asserts the declared size, so a wrong-size regeneration cannot land.

### 7. Privacy policy — one paragraph, three catalogues (FR-015)

- `storage.body` gains one entry, **generic and durable** per the owner's rule (category of data,
  never "what it does today"): the site's own background helper that the browser keeps so the
  site can do things while no tab is open, such as receive a notification the player has asked
  for; part of the site's program, not a record about the player; nothing about them is in it;
  removed by clearing the site's data in the browser.
- The `legalBasis` enumeration ("…and that's all there is here: the cookie…, your choice of
  language, and the half-filled state of a form…") gains the helper as a fourth item, or the
  sentence is no longer true.
- German is authoritative; en/es carry the same paragraph. `legal-catalog.spec.ts` walks arrays,
  so all three ship together or the suite is red. #308 is where the policy gains a recipient
  (Google/Apple/Mozilla) and a stored subscription; not here.

### 8. Tests

- **Unit** (`register-service-worker.spec.ts`): unsupported navigator → `register` never called;
  supported + `readyState==='complete'` → called once with `'/sw.js'` and
  `{scope:'/', updateViaCache:'none'}`; supported + loading → called after `load`; rejected
  promise → no unhandled rejection, no throw.
- **Guard** (`pwa-shell.spec.ts`, reads files from disk): `sw.js` contains none of
  `addEventListener('fetch'`, `caches.`, `importScripts(`; manifest parses, `display` is
  `standalone`, `start_url`/`scope`/`id` are `/`, **no `description`**, colours equal the two
  DESIGN tokens, each icon `src` exists and its PNG IHDR width/height equal `sizes`;
  `index.html` contains the manifest link, the theme-color meta and the apple-touch-icon link.
- **e2e** (`web-e2e/src/pwa-shell.spec.ts`, both projects): `GET /sw.js` → 200,
  `content-type` contains `javascript`, body does not contain `<html`; `GET /manifest.webmanifest`
  → 200 and parses, every icon → 200 `image/png`; **when `BASE_URL` is set** (the nginx container
  — locally the Vite dev server sends its own headers) `cache-control` contains `no-cache` on
  both; `page.goto('/sign-in')` then `navigator.serviceWorker.ready` resolves with
  `scope === origin + '/'` and `active.scriptURL` ending in `/sw.js`.
- **Existing**: `catalog-parity.spec.ts` unchanged and green (no interface keys); `legal-catalog.spec.ts`
  green only with all three policy edits.
- **Device walk** (quickstart): Android Chrome install offer + installed launch; iPhone Add to
  Home Screen + installed launch + bottom bar clear of the home indicator on `/chat` and `/admin`
  + sign-in required inside the installed app; screenshots to the PR.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| A Node `.mjs` tool script (`tools/render-pwa-icons.mjs`) where Principle VI says `.ps1` only | The icons must be rasterised from the SVG mark with transparent corners; nothing on the machine or in the image can do that except Chromium, which is driven through `playwright-core`'s API (`omitBackground` is not exposed by the CLI) | A `.ps1` wrapper would only `node -e` the same code with worse quoting; adding `sharp`/`resvg` is a new dependency for a one-time step; committing PNGs with no script leaves no record of how to regenerate them when #304 replaces the mark. Precedent: `backend/Data/Seed/regenerate-cities500.mjs` |

## Deviations and residuals (recorded)

- **Spec drift, corrected in the spec**: the spec assumed "privacy policy unchanged" and asked
  planning to confirm. Reading the policy showed an exhaustive "that's all there is here" list;
  a registered worker is visible in the browser's site data and belongs in it. **FR-015** added;
  SC-007 narrowed to *interface* catalogues; the assumption rewritten. This is disclosure prose,
  not interface copy — FR-005 (no install copy) stands.
- **Spec drift, corrected**: SC-008 said "exactly two requests more". Browsers that evaluate
  installability also fetch the icons the manifest names (and iOS fetches the apple-touch-icon on
  bookmarking). Reworded to "the description, the worker script, and the icons the browser
  chooses to fetch from the description; nothing else, and nothing to the backend".
- **`viewport-fit=cover` deliberately NOT added.** The existing `env(safe-area-inset-bottom)`
  paddings evaluate to 0 under the current viewport and have been decorative; with the default
  viewport iOS boxes the standalone app inside the safe areas, which satisfies FR-004 as written.
  Going edge-to-edge would need top-inset handling on the sticky header and every full-height view
  — follow-up if the owner wants it, not this feature.
- **`mobile-web-app-capable` meta not added** on the reading that the manifest's `display` is
  honoured by every target platform. If the iOS walk shows browser chrome, add the meta; recorded
  in quickstart as the one contingency.
- **Chrome no longer requires a service worker for installability** (manifest-driven since
  Chrome 108), so US1 and US2 are genuinely independent — an installability bug and a
  registration bug cannot mask each other.
- **Chromium-only e2e for the worker.** Playwright's projects are both Chromium; Firefox/Safari
  registration is covered by the device walk and by the browser-support baseline, not by CI.
- **The cross-context session on iOS** (installed app ≠ Safari cookie jar) is a platform fact the
  spec records; #308's install hint must tell players to expect a sign-in.
- **Icon determinism** is "same Chromium ⇒ same bytes", not guaranteed across Chromium versions;
  the guard checks size, not pixels. A regeneration on a newer Playwright may produce a
  different-but-correct PNG; that is a normal commit, not a failure.
- **No `Service-Worker-Allowed` header** is needed: the script sits at `/`, so root scope is its
  default maximum scope.
