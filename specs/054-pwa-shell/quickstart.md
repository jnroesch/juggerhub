# Quickstart: verifying the PWA shell (054)

How to prove the feature works end to end. Implementation detail lives in `plan.md` and
`tasks.md`; this is the run/validate guide. Every check maps to a spec item in brackets.

## Prerequisites

- Node per `frontend/.nvmrc`, `npm ci` done in `frontend/`.
- Docker Desktop for the compose stack (the nginx checks need the real image, not the dev server).
- For the device walk: an Android phone with Chrome and an iPhone on iOS ≥ 16.4, both able to
  reach the Dev host (or the local stack over the LAN with a trusted certificate — service
  workers need a secure context, and `localhost` counts only on the machine itself).

## 1. Automated — unit + guard (Jest)

```powershell
cd frontend
npx nx test web --testPathPatterns="core/pwa"
```

Expected: green. The guard spec is the one that matters for the future — break it on purpose
once to see it fail: add `self.addEventListener('fetch', () => {});` to `public/sw.js`, run again
[FR-007/FR-011], revert.

Also run the two catalogue guards, which must stay green with **no** interface keys added and
the policy paragraph present in all three locales:

```powershell
npx nx test web --testPathPatterns="catalog-parity|legal-catalog"
```

[FR-005, SC-007, FR-015]

## 2. Automated — served files + live registration (Playwright)

Against the real nginx image (headers are only asserted here):

```powershell
docker compose up -d --build
cd frontend
$env:BASE_URL = "http://localhost:3000"
npx nx e2e web-e2e --grep "pwa"
```

Expected at both projects (desktop-chromium, mobile-chrome):

- `GET /sw.js` → 200, `content-type` contains `javascript`, body has no `<html`,
  `cache-control` contains `no-cache` [FR-009, SC-004].
- `GET /manifest.webmanifest` → 200, parses, `cache-control` `no-cache`; every icon → 200
  `image/png` [FR-001, FR-003].
- After `goto('/sign-in')`, `navigator.serviceWorker.ready` resolves with `scope` =
  `http://localhost:3000/` and `active.scriptURL` ending `/sw.js` [FR-006, SC-003].

Against the dev server (no `BASE_URL`), the same spec runs with the header assertions skipped.

## 3. Manual — nginx shadowing, by hand once

Prove the 404 path is a 404 and not the app page:

```powershell
curl.exe -sI http://localhost:3000/sw.js | Select-String -Pattern "HTTP/|Content-Type|Cache-Control"
curl.exe -sI http://localhost:3000/manifest.webmanifest | Select-String -Pattern "HTTP/|Content-Type|Cache-Control"
curl.exe -sI http://localhost:3000/does-not-exist.js | Select-String -Pattern "HTTP/|Content-Type"
```

Expected: the first two `200` with `application/javascript` / `application/manifest+json` and
`Cache-Control: no-cache`; the third is `200 text/html` (that is `location /` doing its job for
app routes). Then temporarily rename `sw.js` inside the container or rebuild without it and
confirm `/sw.js` is **404**, never `200 text/html` [FR-009, edge case "the worker script is
shadowed"].

## 4. Manual — freshness and no offline (browser dev tools)

1. Open the app, DevTools → Application → Service Workers: one registration, scope `/`, status
   *activated*. Application → Cache Storage: **empty** [FR-007, FR-011].
2. Network tab, reload: `index.html` and every bundle come from the network (or a 304), none
   "(ServiceWorker)" [FR-010, FR-012].
3. Toggle "Offline" in DevTools, reload: Chrome's own offline page, not the app [SC-006].
4. Change `JH_ANALYTICS_HEAD` in `.env`, `docker compose up -d frontend`, reload: the injected
   `<head>` content changes with no rebuild [SC-005, the 033 property].
5. Edit `sw.js` (a comment), rebuild, reload once, then reload again: the new worker is active by
   the second load at the latest [US3 scenario 4].

## 5. Manual — the on-device walk (owner's standing rule; screenshots to the PR)

**Android (Chrome)** [SC-001]
1. Visit the Dev URL. Chrome's install affordance appears (⋮ menu → *Install app*, or the
   mini-infobar). Install.
2. Launch from the home screen: standalone (no address bar), icon and name are JuggerHub, splash
   is the sand background with the mark, status bar is white [FR-001, R9].
3. Open `/chat` and `/admin` (as an admin): the bottom bar sits above the gesture area.

**iPhone (Safari, iOS ≥ 16.4)** [SC-002]
1. Visit the Dev URL, Share → *Add to Home Screen*. The preview shows the coral-gradient icon
   (not a page screenshot) and the name JuggerHub [R7].
2. Launch from the Home Screen: **no Safari chrome**. If Safari chrome IS shown, apply the R7
   contingency (`<meta name="mobile-web-app-capable" content="yes">`) and re-check — record it in
   the PR.
3. You are asked to sign in although you were signed in in Safari — expected (edge case
   "installed on iOS, signed in nowhere"). Sign in.
4. On a notched iPhone, open the dashboard, `/chat`, and `/admin`: the bottom bar clears the
   home indicator, the sticky header sits below the status bar, no content under either
   [FR-004]. Screenshot each.
5. Settings → Safari → Advanced → Website Data (or the installed app's site data): the site is
   listed — this is the fact FR-015 discloses.

**Desktop (Chrome or Edge)** [US1 scenario 5]
1. Visit; the install icon appears in the address bar; install; the window is standalone with
   the mark.

## 6. Manual — the policy text

Open `/privacy` in de, en, es: the "Cookies and what's kept in your browser" section names the
background helper, and the no-banner paragraph's enumeration includes it [FR-015]. German reads
as the authoritative text, not as a translation.

## 7. Full verification before the PR

```powershell
cd frontend
npx nx lint web
npx nx test web
npx nx build web --configuration=production
docker compose -f docker-compose.yml -f docker-compose.test.yml run --rm --build playwright
```

All green, plus the device screenshots (Android + iOS, standalone, `/chat` at the bottom bar)
attached to the PR. `backend/` and `infra/` show **no diff** (`git diff --stat -- backend infra`
is empty) [FR-014].
