# Data Model: PWA Shell for Web Push (054)

**There is no data model.** This feature adds no entity, no column, no migration, no DTO, no
endpoint, no configuration key, and stores nothing about any player anywhere (spec FR-014).

The only state the feature creates lives in the player's browser and is owned by the browser,
not the product:

| Browser-held state | Created by | Contents | Cleared by |
|---|---|---|---|
| Service worker registration for the origin, scope `/` | `navigator.serviceWorker.register('/sw.js')` on each visit | the worker script as served; no data | the browser (site-data clearing, or when the script is unreachable for long enough); never by the product |

The worker itself keeps nothing (FR-007): no Cache API entries, no IndexedDB, no
`localStorage`/`sessionStorage`, no cookies, no fetches.

The first stored fact arrives with **#308** (`PushSubscription`: user, endpoint, keys, device
label, timestamps) and is modelled there.
