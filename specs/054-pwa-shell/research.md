# Research: PWA Shell for Web Push (054)

Every decision below was settled by reading the repository or by checking the shipped nginx
image. Nothing here is a `NEEDS CLARIFICATION`; the three owner decisions are recorded in the
spec's Clarifications and are inputs, not outputs, of this phase.

## R1 — Which worker: hand-written push-only vs `@angular/service-worker`

- **Decision**: a hand-written `public/sw.js` with two lifecycle listeners and nothing else;
  registered by a small helper, not by `provideServiceWorker`.
- **Rationale**: (owner decision, spec C1) plus two code facts. First, `nginx.conf.template`
  `location /` rewrites `index.html` at request time with `sub_filter '</head>' '${JH_ANALYTICS_HEAD}</head>'`
  (033) — that is how one image carries per-environment analytics config. `ngsw` caches
  `index.html` as part of its app-shell group and serves it from the device; the injected
  snippet would freeze at whatever it was when the worker first cached it, and a Dev→Prod
  difference or an analytics switch-off would not reach devices until `ngsw.json` changed.
  Second, the whole product is sign-in-only live data (026): there is no page worth showing
  offline, so `ngsw`'s entire value proposition is absent while its failure class (stale release,
  `ngsw.json` mismatch, "reload to update" prompts) is fully present.
- **Alternatives considered**: `ngsw` with `index.html` excluded from the asset groups — still
  caches every hashed bundle, still needs `ngsw-config.json` maintenance, still installs a `fetch`
  handler for nothing; a hand-written worker *with* a passthrough `fetch` handler — Chrome
  explicitly warns against no-op fetch handlers (they add a worker hop to every navigation) and
  since Chrome 108 a `fetch` handler is not needed for installability anyway.

## R2 — Installability requirements today

- **Decision**: installability is satisfied by the manifest alone (name/short_name, icons ≥192
  and ≥512 with a maskable variant, `display: standalone`, `start_url`, `id`, served over HTTPS).
  The service worker is required for **push**, not for install.
- **Rationale**: Chrome dropped the service-worker requirement for the install prompt (Chrome 108,
  2022); Safari on iOS has never required one for Add to Home Screen and honours the manifest's
  `display` since iOS 11.3. This makes US1 and US2 independent slices.
- **Alternatives considered**: none needed; recorded so nobody adds a `fetch` handler "to make
  Lighthouse happy".

## R3 — File names, paths and MIME types

- **Decision**: `/sw.js` and `/manifest.webmanifest`, both in `apps/web/public/` (the build's
  `assets` glob copies the folder to the root of `dist/apps/web/browser`, which the Dockerfile
  copies to `/usr/share/nginx/html`). The manifest location sets
  `default_type application/manifest+json`.
- **Rationale**: root placement is what gives the worker root scope without a
  `Service-Worker-Allowed` header. **`mime.types` inside `nginxinc/nginx-unprivileged:1.31.5-alpine`
  has no `webmanifest` entry** (checked with `grep` in the image; it does map `js` →
  `application/javascript`), so without `default_type` the manifest would go out as
  `application/octet-stream`. Browsers are lenient about the manifest's type, but the registered
  type costs one line and makes the contract exact.
- **Alternatives considered**: `manifest.json` (served as `application/json` by the existing
  table — also fine, but `.webmanifest` is the W3C-recommended extension and the exact location
  is needed anyway for the no-cache header, so the type line is free); `ngsw-worker.js` naming —
  misleading, it is not ngsw.

## R4 — Keeping the two files out of the SPA fallback

- **Decision**: two `location = …` blocks with `try_files $uri =404` and
  `add_header Cache-Control "no-cache" always`, placed beside `/i18n/`.
- **Rationale**: `location / { try_files $uri $uri/ /index.html; }` answers *any* path with the
  app page. A missing or mis-copied `sw.js` would then be served as HTML; the browser refuses to
  run it as a worker and registration rejects — silently, because FR-008 swallows the error. 038
  documented the same hazard for the analytics endpoints and solved it the same way. `no-cache`
  mirrors `/i18n/`'s reasoning verbatim: keep the copy, ask first, 304 on the ETag.
- **Alternatives considered**: a regex location `~ ^/(sw\.js|manifest\.webmanifest)$` — one
  block, but the template's convention (and its comment) is exact matches, and the manifest needs
  a `default_type` the worker does not; relying on `location /` + `try_files $uri` finding the
  file — works when the file exists and fails silently when it does not, which is exactly the
  failure to design out.

## R5 — Where and how to register

- **Decision**: `registerServiceWorker()` in `core/pwa/`, called from `main.ts` after
  `bootstrapApplication` resolves; inside, wait for `window` `load` (or run at once if the
  document is already complete), call `navigator.serviceWorker.register('/sw.js', { scope: '/', updateViaCache: 'none' })`,
  swallow the promise rejection, never retry.
- **Rationale**: FR-008 wants registration off the critical path and invisible on failure;
  `load` is the documented moment to register without competing with first paint.
  `updateViaCache: 'none'` tells the browser to bypass its HTTP cache when checking the script
  for updates — belt to the nginx header's braces. A plain function with injectable
  `Navigator`/`Window` is unit-testable under jsdom, which has no `serviceWorker` at all.
- **Alternatives considered**: `provideAppInitializer` — runs *before* bootstrap completes and
  would put the registration ahead of the app; a component (`App`) `inject`-side-effect like
  `LanguageService` — the worker belongs to the origin, not to a view, and `App` is rendered on
  every page anyway so nothing is gained; `provideServiceWorker` from `@angular/service-worker` —
  brings `ngsw` back in by another door.

## R6 — The viewport meta and safe areas in standalone mode

- **Decision**: leave `<meta name="viewport" content="width=device-width, initial-scale=1">`
  exactly as it is. Do **not** add `viewport-fit=cover`.
- **Rationale**: three places already pad with `env(safe-area-inset-bottom)` (`bottom-nav`,
  `chat-shell`, `admin-shell`). Those values are **0** unless the viewport declares
  `viewport-fit=cover`; today they are decorative. In standalone mode with the default viewport,
  iOS lays the page out *inside* the safe areas and paints the bands with the page background —
  the bottom bar clears the home indicator by construction, which is FR-004. Declaring `cover`
  would extend the layout under the status bar and home indicator, activating the bottom paddings
  but leaving the sticky `top-0` header and every full-height view with **no** top-inset handling.
  That is a layout project, not a prerequisite.
- **Alternatives considered**: `viewport-fit=cover` + `env(safe-area-inset-top)` on the header —
  out of scope, touches every shell; recorded as a follow-up if the owner wants edge-to-edge.

## R7 — iOS specifics: the Home Screen icon and the display mode

- **Decision**: `<link rel="apple-touch-icon" href="icons/apple-touch-icon.png">` (180×180,
  **opaque**) in `index.html`; no `apple-mobile-web-app-capable` / `mobile-web-app-capable` meta.
- **Rationale**: iOS takes the Home Screen icon from `apple-touch-icon`, not from the manifest,
  and renders transparent pixels as black — so the render is the full-bleed maskable artwork,
  not the rounded favicon. The `…-capable` metas predate manifest support; Safari honours
  `display: standalone` from the manifest. The `apple-mobile-web-app-capable` meta is deprecated
  in Chrome in favour of `mobile-web-app-capable`; neither should be needed.
- **Contingency**: if the on-device walk shows Safari chrome inside the installed app, add
  `<meta name="mobile-web-app-capable" content="yes">` — one line, no other change. Recorded in
  quickstart.

## R8 — Icon production without a new dependency

- **Decision**: a one-off `frontend/tools/render-pwa-icons.mjs` driving `playwright-core`
  (already a dev dependency; Chromium already installed under `%LOCALAPPDATA%\ms-playwright`) to
  screenshot two SVGs at the target sizes; PNGs committed.
- **Rationale**: nothing else on the machine rasterises SVG (no ImageMagick — Windows' `convert`
  is the NTFS tool — no Inkscape, no `rsvg-convert`, no `sharp`/`resvg` in `node_modules`). The
  Playwright **CLI** `screenshot` has `--viewport-size` but no `--omit-background`, and the `any`
  icons need transparent corners, so the API is used. Precedent for a Node tool script:
  `backend/Data/Seed/regenerate-cities500.mjs` (Principle VI deviation recorded in the plan).
- **Two sources, not one**: the existing `favicon.svg` (rounded `rx=16`, transparent corners) is
  right for `purpose: any`; a **maskable** icon must be full-bleed with the mark inside the central
  80 % or launchers crop it — so `icon-maskable.svg` is a second, committed artwork: the same
  gradient as a square, the cross + centre scaled to ~64 % of the edge. Declaring the two purposes
  on **separate** icons follows Chrome's guidance (a combined `any maskable` icon shows maskable
  padding where no mask is applied).
- **Alternatives considered**: `sharp` (new native dependency for a one-time step); committing
  PNGs with no script (no record of how to regenerate when #304 replaces the mark); an `.ico`-style
  hand edit (not reproducible).

## R9 — Manifest colours from DESIGN.md

- **Decision**: `background_color: "#FBF8F3"` (`surface-page` = `sand-0`, the page ground and
  therefore the splash screen); `theme_color: "#FFFFFF"` (`surface-card`, which both the sticky
  top bar and the fixed bottom bar use, so the OS chrome in standalone mode matches the bars);
  the same `#FFFFFF` in `<meta name="theme-color">`.
- **Rationale**: FR-001 says tokens, never ad hoc. `brand-primary` coral was considered for
  `theme_color` and rejected — the app's bars are white; a coral status bar over a white header
  reads as a mismatch, not as branding.

## R10 — The privacy policy

- **Decision**: one new paragraph in `storage.body` and one extra item in the `legalBasis`
  enumeration, in en/de/es, German authoritative (FR-015).
- **Rationale**: the spec asked planning to confirm the "unchanged" assumption. The policy's
  "Cookies and what's kept in your browser" section is an **exhaustive list** and the
  no-banner argument says "and that's all there is here" before listing three items. A service
  worker registration is persistent per-origin state the reader can see in their browser's
  site-data settings; omitting it makes both sentences false in the one document the owner
  treats as binding. The paragraph follows the owner's rule for legal text: category of data,
  durable description, no "today it does nothing" snapshot (that would go stale with #308).
- **Alternatives considered**: treating the worker as the browser's own cache of the site's
  program (like the hashed bundles, which the policy does not list) — defensible, but the
  worker is *registered* at the site's request and listed by name in browser UIs, which the HTTP
  cache is not; the conservative reading costs one paragraph.

## R11 — Tests that make the decisions structural

- **Decision**: a guard spec that reads `sw.js`, the manifest, `index.html` and the PNG headers
  from disk, beside the unit spec for the helper; an e2e that checks the served files and the
  live registration at both Playwright projects.
- **Rationale**: FR-011's "no one adds caching later by reflex" is a promise about future diffs;
  a test that fails on `addEventListener('fetch'` or `caches.` in `sw.js` is the only thing that
  enforces it after this PR is forgotten. Likewise "no `description`" (FR-005) and "colours are
  the tokens" (FR-001) are one-line assertions that would otherwise depend on review memory. The
  PNG IHDR check (width/height at bytes 16–23) is what stops a regenerated icon of the wrong size
  from shipping with a manifest that claims otherwise. The `cache-control` e2e assertion runs only
  when `BASE_URL` is set, because the local Vite dev server does not send nginx's headers.
