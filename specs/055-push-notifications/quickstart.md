# Quickstart: verifying push notifications (055)

How to prove the feature works end to end. Implementation detail lives in `plan.md` and `tasks.md`.
Each check names the spec item it covers.

## Prerequisites

- The compose stack, and a VAPID key pair for local use:

```powershell
pwsh ./scripts/New-VapidKeyPair.ps1
# prints WEBPUSH_PUBLIC_KEY / WEBPUSH_PRIVATE_KEY lines — paste into .env, then:
docker compose up -d --build backend frontend
```

- A browser that can receive push. **Service workers and push need a secure context**, so use
  `http://localhost:3000` on this machine, or Dev over HTTPS. A non-localhost plain-HTTP origin has
  no `PushManager` at all — the same trap feature 054 hit in its containerised e2e.
- For the device walk: an Android phone with Chrome, and an iPhone on iOS 16.4 or later.

## 1. Backend unit and integration tests

```powershell
dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~Push"
```

Expected green, and these are the cases that matter most:

- **The four-way preference matrix.** In-app on/off × push on/off, for both `CreateAsync` and
  `CreateManyAsync`. Three of the four are new behaviour, and the one that would regress silently is
  **in-app off, push on** [FR-015, SC-005].
- **Duplicate suppression.** The same `dedupeKey` twice produces one row and one dispatch [FR-011].
- **`404`/`410` prunes and does not retry**; `429` retries [FR-021, research R4].
- **A failing dispatcher does not fail the producing action** — post team news with the fake
  dispatcher throwing, and assert the post exists and the request succeeded [FR-013, SC-007].
- **Language** comes from the recipient, not the actor [FR-008, SC-003].
- **Account deletion** leaves no subscription row [FR-023, SC-009].
- **No subscriptions ⇒ no outbound call at all** — assert the fake was never invoked [SC-008].

## 2. Frontend unit tests

```powershell
cd frontend
npx nx test web --testPathPatterns="push|notification-settings"
npx nx test web --testPathPatterns="catalog-parity|legal-catalog"
```

The second command must be green with the new settings copy present in **all three** catalogues and
the privacy paragraphs in all three legal files [FR-024, SC-010].

Also confirm the extended 054 guard still forbids what it forbade: `sw.js` has `push` and
`notificationclick` and still has **no** `fetch` handler, no `caches`, no `importScripts`.

## 3. The settings page, state by state

Open `/settings/notifications` and check each state renders its own sentence [FR-002, SC-011]:

| State | How to produce it |
|---|---|
| `off` | A fresh profile in a browser that has never been asked |
| `on` | Press enable and allow |
| `blocked` | Deny the permission prompt, or block the site in browser settings, then reload |
| `unsupported` | A browser without the Push API |
| `needs-install` | Safari on an iPhone, not added to the Home Screen |

The `blocked` state must explain that only the browser can undo it and must **not** show a button
that cannot work [FR-004].

## 4. End to end, by hand

1. Enable notifications on this browser. Confirm a row exists:
   `docker compose exec database psql -U juggerhub -c 'select count(*) from "PushSubscriptions";'`
2. **Close every JuggerHub tab.**
3. From another account, invite the first account to a team.
4. A notification appears naming the team and the inviter, in the recipient's language [SC-002].
5. Click it: the product opens at the team page, asking to sign in first if the session expired
   [FR-010].
6. Turn the category's **in-app** channel off, leave push on, trigger it again: the notification
   still arrives [FR-015]. This is the one that would silently regress.
7. Turn the category's **push** off: nothing arrives, and the in-app entry still appears [SC-004].
8. Turn this device off in settings, trigger again: nothing arrives.
9. Enable again, then **sign out**, then trigger from the other account: nothing arrives, and the
   row is gone [FR-020].

## 5. Failure behaviour

- Point `WebPush:Subject` at a valid value but break connectivity to the push service (block egress
  or set an unreachable proxy). Post team news. The post succeeds, the page shows nothing unusual,
  and the log carries a status code and a subscription id and **no response body** [FR-013, VII].
- Confirm the breaker opens under sustained failure and that the producing action still succeeds
  while it is open.

## 6. The device walk (owner's standing rule — screenshots to the PR)

**Android Chrome**: enable from settings, lock the phone, trigger an event, see the notification on
the lock screen, tap it, land on the right page.

**iPhone**: first in Safari without installing — settings must show the install instructions and no
enable button. Then add to Home Screen, open the installed app, sign in, enable, and repeat the
lock-screen test [FR-003, SC-011].

**Layout, in German, per Gate 7**: the settings matrix at the `md` breakpoint with **four** columns,
and the mobile card at 375px with its **fourth row**. The issue claimed a four-column squeeze at
375px; there is none, because below `md` the matrix is stacked cards.

## 7. Full verification before the PR

```powershell
dotnet test backend/tests/JuggerHub.Api.IntegrationTests
cd frontend; npx nx lint web; npx nx test web; npx nx build web --configuration=production
docker compose -f docker-compose.yml -f docker-compose.test.yml run --rm --build playwright
```

All green, plus the device screenshots. Confirm the migration applies cleanly and that
`git diff --stat main -- infra` shows only the new secret passthrough.
